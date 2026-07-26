using System.Collections.Concurrent;

namespace EDV.Framework.Quota;

/// <summary>
/// Хранилище-одиночка для <see cref="InMemoryQuotaService"/>, чтобы счётчики сохранялись между областями запросов.
/// Ключи имеют формат <c>quota:{tenantId}:{resource}:{period}</c>, как и в бэкенде Redis.
/// </summary>
public sealed class InMemoryQuotaStore
{
    public ConcurrentDictionary<string, long> Counters { get; } = new();
}