using EDV.Framework.Core.Context;
using EDV.Framework.Core.Exceptions;
using EDV.Modules.Identity.Contracts.Services;
using EDV.Modules.Identity.Contracts.v1.Users.SetProfileImage;
using Mediator;

namespace EDV.Modules.Identity.Features.v1.Users.SetProfileImage;

public sealed class SetProfileImageCommandHandler(
    IUserProfileService profileService,
    ICurrentUser currentUser)
    : ICommandHandler<SetProfileImageCommand, Unit>
{
    public async ValueTask<Unit> Handle(SetProfileImageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var userId = currentUser.GetUserId();
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedException("no current user");
        }

        await profileService
            .SetImageUrlAsync(userId.ToString(), command.ImageUrl, cancellationToken)
            .ConfigureAwait(false);

        return Unit.Value;
    }
}
