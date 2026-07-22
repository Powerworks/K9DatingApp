namespace K9Crush.Modules.Identity.Api.Commands.BootstrapAdmin;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record BootstrapAdminResponse(Guid OwnerId, string Role);
