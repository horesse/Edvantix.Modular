using EDV.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace EDV.Modules.Identity.Contracts.v1.Sessions.GetUserSessions;

public sealed record GetUserSessionsQuery(Guid UserId) : IQuery<List<UserSessionDto>>;