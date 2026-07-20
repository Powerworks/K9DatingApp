using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.BuildingBlocks.Web;

/// <summary>
/// Port for resolving a caller's ADR-017 role without any module (or
/// Api.Host itself) needing a direct reference to Identity's Domain
/// project - Identity.Api implements this and registers it in DI via
/// IModule.RegisterServices; Api.Host's role-based authorization handler
/// depends on this interface only, resolved from the container. Same
/// dependency-inversion shape as IModule itself.
///
/// Null return means no OwnerAccount exists yet for that id - shouldn't
/// happen for a caller with a valid JWT (ProvisionOwnerOnSupabaseSignup
/// runs on every Supabase signup), but a webhook race or an unprovisioned
/// test token can still hit it, so callers must treat this as "deny",
/// never "assume Owner."
/// </summary>
public interface IOwnerRoleLookup
{
    Task<OwnerRole?> GetRoleAsync(Guid ownerId, CancellationToken cancellationToken);
}
