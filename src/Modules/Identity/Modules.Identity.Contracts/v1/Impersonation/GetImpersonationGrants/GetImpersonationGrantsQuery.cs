using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Impersonation.GetImpersonationGrants;

// Статус (null = все) и фильтры ActorUserId. ImpersonatedTenantId фильтрует по целевому арендатору,
// но администраторы арендаторов принудительно ограничены своим собственным арендатором на стороне сервера.
public sealed record GetImpersonationGrantsQuery(
    ImpersonationGrantStatus? Status = null,
    string? ImpersonatedTenantId = null,
    string? ActorUserId = null,
    int Take = 100)
    : IQuery<IReadOnlyList<ImpersonationGrantDto>>;