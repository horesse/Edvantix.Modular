using System.Collections.Immutable;
using System.ComponentModel;

namespace EDV.Modules.Identity.Caching;

/// <summary>
/// Неизменяемый контейнер для набора разрешений пользователя. Используется как тип значения кэша в
/// <see cref="Services.UserPermissionService"/>, чтобы HybridCache мог повторно использовать
/// внутрипроцессный экземпляр между запросами без повторной десериализации при каждом попадании в L1.
/// </summary>
/// <remarks>
/// Должен оставаться <c>sealed</c> + <see cref="ImmutableObjectAttribute"/> — удаление любого из них
/// молча ухудшает чтение L1 HybridCache до десериализации JSON при каждом вызове.
/// </remarks>
[ImmutableObject(true)]
internal sealed record PermissionSet(ImmutableArray<string> Values)
{
    public static PermissionSet Empty { get; } = new(ImmutableArray<string>.Empty);

    public bool Contains(string permission) => Values.Contains(permission);
}