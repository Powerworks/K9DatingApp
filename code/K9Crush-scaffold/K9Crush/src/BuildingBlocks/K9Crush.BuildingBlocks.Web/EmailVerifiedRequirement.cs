using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace K9Crush.BuildingBlocks.Web;

/// <summary>
/// ASP.NET Core authorization requirement backing the "VerifiedOwner"
/// policy. Originally implemented as a plain `RequireClaim("email_verified",
/// "true")` - never verified against a real Supabase-issued token before
/// a live project existed to test against (2026-07-23). A real token has
/// no top-level "email_verified" claim at all; it's nested inside the
/// "user_metadata" claim as a JSON object (`{"email_verified":true}`),
/// so the plain RequireClaim check silently rejected every genuinely
/// verified caller. Same class of gap as the JWT signing algorithm
/// assumption (ADR-005) - both were speculative until this session's
/// live verification.
/// </summary>
public sealed class EmailVerifiedRequirement : IAuthorizationRequirement;

/// <summary>
/// Parses the "user_metadata" claim's JSON and succeeds only if its
/// "email_verified" field is true. Registered as Scoped alongside
/// RoleAuthorizationHandler for consistency, though this one has no
/// scoped dependencies itself.
/// </summary>
public sealed class EmailVerifiedAuthorizationHandler : AuthorizationHandler<EmailVerifiedRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EmailVerifiedRequirement requirement)
    {
        var userMetadataClaim = context.User.FindFirst("user_metadata")?.Value;
        if (userMetadataClaim is null)
            return Task.CompletedTask;

        try
        {
            using var document = JsonDocument.Parse(userMetadataClaim);
            if (document.RootElement.TryGetProperty("email_verified", out var emailVerified) &&
                emailVerified.ValueKind == JsonValueKind.True)
            {
                context.Succeed(requirement);
            }
        }
        catch (JsonException)
        {
            // Malformed user_metadata - treat as not verified rather than throwing.
        }

        return Task.CompletedTask;
    }
}
