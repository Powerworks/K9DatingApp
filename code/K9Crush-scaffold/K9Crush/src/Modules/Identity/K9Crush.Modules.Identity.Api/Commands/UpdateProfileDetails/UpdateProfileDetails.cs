using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Identity.Api.Commands.UpdateProfileDetails;

/// <summary>
/// The request/command for this slice. DisplayName is the only field -
/// see OwnerAccount.DisplayName's doc comment for why (the yaml lists no
/// props for "Update Profile Details").
/// </summary>
public sealed record UpdateProfileDetailsRequest(
    [property: Required, MaxLength(200)] string DisplayName);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record UpdateProfileDetailsResponse(Guid OwnerId, string DisplayName);
