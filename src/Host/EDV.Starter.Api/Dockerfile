# syntax=docker/dockerfile:1.7

# ── Этап сборки ─────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем всё дерево src/ целиком, чтобы dotnet restore + publish имели всё
# необходимое (Directory.Build.props / Directory.Packages.props / global.json
# централизуют версии, поэтому мы не можем легко ограничиться одним проектом).
# Примечание: src/Tests/ исключается через .dockerignore — restore нацелен
# напрямую на проект API (не на решение), чтобы избежать ссылок на отсутствующие тестовые проекты.
COPY src/ ./src/

RUN dotnet restore src/Host/EDV.Starter.Api/EDV.Starter.Api.csproj
RUN dotnet publish src/Host/EDV.Starter.Api/EDV.Starter.Api.csproj \
    -c Release \
    --no-restore \
    -o /publish

# ── Этап выполнения ──────────────────────────────────────────────────
# Chiseled = без оболочки, без apt, без отладчика. Меньший размер образа + меньшая
# поверхность CVE. APP_UID (1654) — это не-root пользователь, встроенный в образ.
FROM mcr.microsoft.com/dotnet/nightly/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /publish .

USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "EDV.Starter.Api.dll"]