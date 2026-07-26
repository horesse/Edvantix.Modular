using Mediator;

namespace EDV.Modules.Multitenancy.Contracts.v1.AdjustTenantValidity;

/// <summary>
/// Переопределение оператором, устанавливающее срок действия тенанта на явно заданную дату без побочных
/// эффектов на биллинг (без подписки, счёта, события продления). Предназначено для бесплатных периодов,
/// продления в рамках поддержки или немедленного истечения срока. В отличие от продления, может сдвигать дату назад.
/// </summary>
public sealed record AdjustTenantValidityCommand(string TenantId, DateTime ValidUpto)
    : ICommand<AdjustTenantValidityCommandResponse>;
