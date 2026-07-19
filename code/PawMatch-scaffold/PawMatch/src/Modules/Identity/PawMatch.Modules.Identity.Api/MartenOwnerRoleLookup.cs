using Marten;
using PawMatch.BuildingBlocks.Domain;
using PawMatch.BuildingBlocks.Web;
using PawMatch.Modules.Identity.Domain;

namespace PawMatch.Modules.Identity.Api;

/// <summary>
/// Identity's implementation of the BuildingBlocks.Web port - registered
/// in DI by IdentityModule.RegisterServices below. A direct document read
/// against OwnerAccount.Role, same shape as any other read-model query in
/// this codebase (GetDogProfileHandler, OwnerAccountViewHandler), just
/// consumed by an authorization handler instead of an HTTP endpoint.
/// </summary>
public sealed class MartenOwnerRoleLookup(IQuerySession session) : IOwnerRoleLookup
{
    public async Task<OwnerRole?> GetRoleAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var ownerAccount = await session.LoadAsync<OwnerAccount>(ownerId, cancellationToken);
        return ownerAccount?.Role;
    }
}
