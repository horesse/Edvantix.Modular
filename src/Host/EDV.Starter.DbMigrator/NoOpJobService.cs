using EDV.Framework.Jobs.Services;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace EDV.Starter.DbMigrator;

/// <summary>
/// Удовлетворяет <see cref="IJobService"/> в графе DI мигратора без
/// подключения сервера фоновых заданий Hangfire (которому нужны собственная схема БД
/// и рабочие потоки — избыточно для однократной консольной утилиты).
///
/// Все операции выбрасывают исключения — код мигратора
/// (<c>ITenantService.MigrateTenantAsync</c> / <c>SeedTenantAsync</c>)
/// никогда не ставит задания в очередь. Если регрессия начнёт ставить задания в очередь
/// во время миграции, исключение сделает ошибку очевидной.
/// </summary>
[SuppressMessage("Performance", "CA1812",
    Justification = "Активируется контейнером DI во время выполнения через AddSingleton<IJobService, NoOpJobService>; анализатор не видит этот путь.")]
internal sealed class NoOpJobService : IJobService
{
    private static InvalidOperationException Reject(string method) =>
        new($"IJobService.{method} вызван из DbMigrator — задания не поддерживаются в однократном миграторе. " +
            "Если этот путь кода теперь требуется во время миграции, включите Hangfire в опциях AddPlatform DbMigrator.");

    public bool Delete(string jobId) => throw Reject(nameof(Delete));
    public bool Delete(string jobId, string fromState) => throw Reject(nameof(Delete));
    public string Enqueue(Expression<Action> methodCall) => throw Reject(nameof(Enqueue));
    public string Enqueue(string queue, Expression<Func<Task>> methodCall) => throw Reject(nameof(Enqueue));
    public string Enqueue(Expression<Func<Task>> methodCall) => throw Reject(nameof(Enqueue));
    public string Enqueue<T>(Expression<Action<T>> methodCall) => throw Reject(nameof(Enqueue));
    public string Enqueue<T>(Expression<Func<T, Task>> methodCall) => throw Reject(nameof(Enqueue));
    public bool Requeue(string jobId) => throw Reject(nameof(Requeue));
    public bool Requeue(string jobId, string fromState) => throw Reject(nameof(Requeue));
    public string Schedule(Expression<Action> methodCall, TimeSpan delay) => throw Reject(nameof(Schedule));
    public string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay) => throw Reject(nameof(Schedule));
    public string Schedule(Expression<Action> methodCall, DateTimeOffset enqueueAt) => throw Reject(nameof(Schedule));
    public string Schedule(Expression<Func<Task>> methodCall, DateTimeOffset enqueueAt) => throw Reject(nameof(Schedule));
    public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) => throw Reject(nameof(Schedule));
    public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay) => throw Reject(nameof(Schedule));
    public string Schedule<T>(Expression<Action<T>> methodCall, DateTimeOffset enqueueAt) => throw Reject(nameof(Schedule));
    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt) => throw Reject(nameof(Schedule));
}