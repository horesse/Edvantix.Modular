var builder = DistributedApplication.CreateBuilder(args);
#pragma warning disable S125

// Префикс для каждого приложения из имени сборки AppHost (EDV.Starter.AppHost -> edv-starter);
// пространства имён для томов Docker + имён ресурсов, чтобы несколько приложений EDV не конфликтовали.
#pragma warning disable CA1308 // имена ресурсов + томов по соглашению в нижнем регистре
var appPrefix = builder.Environment.ApplicationName
    .Replace(".AppHost", string.Empty, StringComparison.OrdinalIgnoreCase)
    .Replace('.', '-')
    .ToLowerInvariant();
#pragma warning restore CA1308

// Postgres + pgAdmin (обнаруживает зарегистрированные базы данных автоматически); постоянные тома и сохранённое состояние переживают перезапуски.
var postgresServer = builder.AddPostgres("postgres")
    .WithDataVolume($"{appPrefix}-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(pa => pa
        .WithHostPort(5050)
        .WithLifetime(ContainerLifetime.Persistent));

var postgres = postgresServer.AddDatabase("edv-db");

// Прогрев пула соединений для долго работающего API — Npgsql по умолчанию Minimum Pool Size = 0 позволяет пулу остыть,
// поэтому ~10 параллельных проверок DbContext в /health/ready одновременно открывают холодные соединения и периодически
// тормозят зонд; минимальный размер пула поддерживает соединения тёплыми для повторного использования.
var apiPgConnection = ReferenceExpression.Create(
    $"{postgres.Resource.ConnectionStringExpression};Minimum Pool Size=5");

// Valkey (форк Redis под BSD-3) как обычный контейнер: Aspire 13.4.0 AddRedis() принудительно включает TLS в режиме run
// и никогда не материализует контейнер, поэтому мы используем обычный RESP через TCP. Имя остаётся "redis", чтобы ключи конфигурации не менялись.
var redis = builder.AddContainer("redis", "valkey/valkey", "9.1.0")
    .WithEndpoint(targetPort: 6379, scheme: "tcp", name: "tcp")
    .WithVolume($"{appPrefix}-redis-data", "/data")
    .WithLifetime(ContainerLifetime.Persistent);

var redisEndpoint = redis.GetEndpoint("tcp");
var redisConnectionString = ReferenceExpression.Create(
    $"{redisEndpoint.Property(EndpointProperty.HostAndPort)}");

// RedisInsight — браузер кэша (только для разработки); RI_REDIS_* предварительно регистрирует подключение к Valkey
// через псевдоним "redis" в сети контейнеров.
builder.AddContainer("redis-insight", "redis/redisinsight", "latest")
    .WithHttpEndpoint(port: 5540, targetPort: 5540, name: "http")
    .WithEnvironment("RI_REDIS_HOST0", "redis")
    .WithEnvironment("RI_REDIS_PORT0", "6379")
    .WithEnvironment("RI_REDIS_ALIAS0", "edv-cache")
    .WithEnvironment("RI_ACCEPT_TERMS_AND_CONDITIONS", "true")
    .WithLifetime(ContainerLifetime.Persistent)
    .WaitFor(redis);

// Объектное хранилище (MinIO, совместимое с S3). CORS через MINIO_API_CORS_ALLOW_ORIGIN для браузерных предварительно подписанных PUT
// из административной панели (:5173)/дашборда (:5174) в среде разработки работает без прокси через API.
const string MinioBucket = "edv-uploads";
const string AdminOrigin = "http://localhost:5173";
const string DashboardOrigin = "http://localhost:5174";

var minioUser = builder.AddParameter("minio-user", "minioadmin");
var minioPassword = builder.AddParameter("minio-password", "minioadmin", secret: true);

var minio = builder.AddContainer("minio", "minio/minio")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "api")
    .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "console")
    .WithEnvironment("MINIO_ROOT_USER", minioUser)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioPassword)
    .WithEnvironment("MINIO_API_CORS_ALLOW_ORIGIN", $"{AdminOrigin},{DashboardOrigin}")
    .WithVolume($"{appPrefix}-minio-data", "/data")
    .WithLifetime(ContainerLifetime.Persistent);

// Init-контейнер: создание бакета (create + public-read). Скрипт нормализован к LF, чтобы /bin/sh в minio/mc не спотыкался о CRLF Windows.
var minioInitScript = ($$"""
until mc alias set local http://minio:9000 "$MC_USER" "$MC_PASS"; do
  echo "waiting for minio...";
  sleep 2;
done;
mc mb --ignore-existing local/{{MinioBucket}};
mc anonymous set download local/{{MinioBucket}};
""").ReplaceLineEndings("\n");

var minioInit = builder.AddContainer("minio-init", "minio/mc")
    .WithEntrypoint("/bin/sh")
    .WithArgs("-c", minioInitScript)
    .WithEnvironment("MC_USER", minioUser)
    .WithEnvironment("MC_PASS", minioPassword)
    .WaitFor(minio);

var minioApiEndpoint = minio.GetEndpoint("api");

// Мигратор БД: применяет ожидающие миграции + создаёт корневого администратора (admin@root.com), затем завершается;
// API ожидает его завершения, поэтому никогда не запускается против немигрированной БД.
// Пароль для заполнения — только для разработки.
var migrator = builder.AddProject<Projects.EDV_Starter_DbMigrator>($"{appPrefix}-db-migrator")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithEnvironment("DatabaseOptions__Provider", "POSTGRESQL")
    .WithEnvironment("DatabaseOptions__ConnectionString", postgres.Resource.ConnectionStringExpression)
    .WithEnvironment("DatabaseOptions__MigrationsAssembly", "EDV.Starter.Migrations.PostgreSQL")
    .WithEnvironment("Seed__DefaultAdminPassword", "123Pa$$word!")
    .WithArgs("apply", "--seed");

// Демо-заполнение (только для разработки): создаёт арендаторов acme/globex + демо-пользователей через seed-demo.
// Требуется DOTNET_ENVIRONMENT=Development (консольный хост игнорирует ASPNETCORE_ENVIRONMENT),
// иначе seed-demo отказывается выполняться.
var demoSeeder = builder.AddProject<Projects.EDV_Starter_DbMigrator>($"{appPrefix}-demo-seeder")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitForCompletion(migrator)
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithEnvironment("DatabaseOptions__Provider", "POSTGRESQL")
    .WithEnvironment("DatabaseOptions__ConnectionString", postgres.Resource.ConnectionStringExpression)
    .WithEnvironment("DatabaseOptions__MigrationsAssembly", "EDV.Starter.Migrations.PostgreSQL")
    .WithEnvironment("Seed__DemoPassword", "Password123!")
    .WithArgs("seed-demo");

// API-сервис
var api = builder.AddProject<Projects.EDV_Starter_Api>($"{appPrefix}-api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitForCompletion(minioInit)
    .WaitForCompletion(migrator)
    .WaitForCompletion(demoSeeder)
    .WithExternalHttpEndpoints()
    .WithEnvironment("DatabaseOptions__Provider", "POSTGRESQL")
    .WithEnvironment("DatabaseOptions__ConnectionString", apiPgConnection)
    .WithEnvironment("DatabaseOptions__MigrationsAssembly", "EDV.Starter.Migrations.PostgreSQL")
    .WithEnvironment("CachingOptions__Redis", redisConnectionString)
    .WithEnvironment("CachingOptions__EnableSsl", "false")
    // Учётные данные панели Hangfire (/jobs) — [Required], Password [MinLength(12)], ValidateOnStart;
    // API не запустится без них. Только для разработки, соответствует appsettings.Development.json.
    .WithEnvironment("HangfireOptions__UserName", "admin")
    .WithEnvironment("HangfireOptions__Password", "Password123!")
    // SMTP через Ethereal (https://ethereal.email) — фиктивный catch-all ящик для локальной разработки
    // (ничего не доставляется); соответствует appsettings.Development.json. Безопасно для коммита: одноразовые тестовые учётные данные.
    .WithEnvironment("MailOptions__UseSendGrid", "false")
    .WithEnvironment("MailOptions__From", "nicole.lueilwitz0@ethereal.email")
    .WithEnvironment("MailOptions__DisplayName", "Mukesh Murugan")
    .WithEnvironment("MailOptions__Smtp__Host", "smtp.ethereal.email")
    .WithEnvironment("MailOptions__Smtp__Port", "587")
    .WithEnvironment("MailOptions__Smtp__UserName", "nicole.lueilwitz0@ethereal.email")
    .WithEnvironment("MailOptions__Smtp__Password", "x4VJz2r9x2NDss9KpC")
    .WithEnvironment("Storage__Provider", "s3")
    .WithEnvironment("Storage__S3__Bucket", MinioBucket)
    .WithEnvironment("Storage__S3__Region", "us-east-1")
    .WithEnvironment("Storage__S3__ServiceUrl", minioApiEndpoint)
    .WithEnvironment("Storage__S3__AccessKey", minioUser)
    .WithEnvironment("Storage__S3__SecretKey", minioPassword)
    .WithEnvironment("Storage__S3__ForcePathStyle", "true")
    .WithEnvironment("Storage__S3__PublicBaseUrl", ReferenceExpression.Create($"{minioApiEndpoint}/{MinioBucket}"));

//#if (frontend)
// Административная консоль (React + Vite). Нацелена напрямую на HTTPS-конечную точку API —
// перенаправление 307 UseHttpsRedirection на HTTPS является кросс-доменным и удаляет заголовок Authorization.
builder.AddJavaScriptApp($"{appPrefix}-admin", "../../../clients/admin", "dev")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 5173, targetPort: 5173, isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("https"));

// Клиентская панель для арендаторов (React + Vite, с SSE-лентой в реальном времени)
builder.AddJavaScriptApp($"{appPrefix}-dashboard", "../../../clients/dashboard", "dev")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 5174, targetPort: 5174, isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("https"));
//#else
// React-приложения исключены: игнорируем неиспользуемый api, чтобы каркас без фронтенда не вызывал предупреждений (S1481 при TreatWarningsAsErrors).
_ = api;
//#endif

await builder.Build().RunAsync();

#pragma warning restore S125
