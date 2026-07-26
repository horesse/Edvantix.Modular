using EDV.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Groups.GetGroupById;

public sealed record GetGroupByIdQuery(Guid Id) : IQuery<GroupDto>;