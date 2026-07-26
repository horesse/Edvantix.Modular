namespace EDV.Modules.Identity.Contracts.DTOs;

/// <summary>
/// Одна запись в общесистемном каталоге разрешений, возвращаемая SPA, чтобы редактор ролей
/// мог отображать все существующие разрешения, а не только те, которые запомнил локальный TypeScript-файл.
/// Отражает <c>EDV.Framework.Shared.Identity.PermissionConstants</c>; API-поверхность является
/// авторитетным источником — модули добавляют разрешения через <c>PermissionConstants.Register</c>
/// при запуске, а редактор считывает их через этот DTO.
/// </summary>
public sealed record PermissionCatalogEntryDto(
    string Name,
    string Description,
    string Resource,
    string Action,
    bool IsBasic,
    bool IsRoot);