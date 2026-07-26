using EDV.Modules.Auditing.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Auditing.Contracts.v1.GetAuditById;

public sealed record GetAuditByIdQuery(Guid Id) : IQuery<AuditDetailDto>;