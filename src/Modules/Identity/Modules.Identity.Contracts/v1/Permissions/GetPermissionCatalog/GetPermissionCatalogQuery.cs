using EDV.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Permissions.GetPermissionCatalog;

/// <summary>
/// Возвращает все разрешения, зарегистрированные в реестре <c>PermissionConstants</c> хоста,
/// отфильтрованные по контексту арендатора вызывающего: некорневые арендаторы получают набор Admin;
/// корневой арендатор дополнительно получает набор Root платформы. Соответствует правилу,
/// применяемому <c>RolePermissionSyncer</c>, чтобы каталог редактора и синхронизируемые цели
/// оставались согласованными.
/// </summary>
public sealed record GetPermissionCatalogQuery() : IQuery<IReadOnlyList<PermissionCatalogEntryDto>>;