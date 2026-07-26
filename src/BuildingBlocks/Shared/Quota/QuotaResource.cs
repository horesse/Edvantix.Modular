namespace EDV.Framework.Shared.Quota;

/// <summary>
/// Ресурсы, которые могут учитываться для каждого арендатора. Ресурсы на основе счётчиков (ApiCalls) отслеживаются
/// в рамках биллингового периода; ресурсы на основе датчиков (StorageBytes, Users, ActiveFeatureFlags)
/// отражают состояние на текущий момент времени и определяются по запросу зарегистрированными провайдерами датчиков.
/// </summary>
public enum QuotaResource
{
    ApiCalls,
    StorageBytes,
    Users,
    ActiveFeatureFlags
}