using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace K9Crush.Blazor.App.Services;

/// <summary>
/// Thin wrapper over Supabase's own /auth/v1 REST API. Supabase owns the
/// entire signup/login/confirmation lifecycle (ADR-005) - this app never
/// issues or stores credentials itself, only relays to Supabase and keeps
/// the resulting session (see Login.razor/Register.razor).
/// </summary>
public sealed class SupabaseAuthService(IHttpClientFactory httpClientFactory)
{
    public async Task<SupabaseAuthResult> SignInWithPasswordAsync(
        string email, string password, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("SupabaseAuth");
        var response = await client.PostAsJsonAsync(
            "token?grant_type=password", new SupabaseCredentials(email, password), cancellationToken);

        return await ReadResultAsync(response, cancellationToken);
    }

    public async Task<SupabaseAuthResult> SignUpAsync(
        string email, string password, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("SupabaseAuth");
        var response = await client.PostAsJsonAsync(
            "signup", new SupabaseCredentials(email, password), cancellationToken);

        // Confirm-email is on (Identity's ConfirmProfile flow, ADR-005) so a
        // fresh signup has no session yet - AccessToken stays null until the
        // confirmation email is clicked and Supabase's own webhook fires
        // VerifyOwnerOnSupabaseConfirmationHandler. Register.razor branches
        // on that rather than assuming a session always comes back.
        return await ReadResultAsync(response, cancellationToken);
    }

    private static async Task<SupabaseAuthResult> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<SupabaseAuthError>(cancellationToken: cancellationToken);
            return SupabaseAuthResult.Failed(error?.ErrorDescription ?? error?.Msg ?? "Something went wrong - please try again.");
        }

        var session = await response.Content.ReadFromJsonAsync<SupabaseSession>(cancellationToken: cancellationToken);
        return SupabaseAuthResult.Succeeded(session);
    }

    private sealed record SupabaseCredentials(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("password")] string Password);

    private sealed record SupabaseAuthError(
        [property: JsonPropertyName("msg")] string? Msg,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);
}

public sealed record SupabaseAuthResult(bool IsSuccess, string? ErrorMessage, SupabaseSession? Session)
{
    public static SupabaseAuthResult Succeeded(SupabaseSession? session) => new(true, null, session);
    public static SupabaseAuthResult Failed(string errorMessage) => new(false, errorMessage, null);
}

public sealed record SupabaseSession(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("user")] SupabaseUser? User);

public sealed record SupabaseUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("email")] string? Email);
