using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Authorization;

namespace K9Crush.Blazor.App.Services;

/// <summary>
/// Wraps the "K9CrushApi" typed HttpClient with the current session's
/// Supabase access token (set at login, see Login.razor/Register.razor's
/// "access_token" claim) attached as a Bearer token - for calling
/// Api.Host's [Authorize]-gated endpoints. Reads the token via
/// AuthenticationStateProvider rather than IHttpContextAccessor - the
/// latter is unreliable inside an interactive Server circuit, while the
/// cascading AuthenticationState survives for the circuit's lifetime.
/// </summary>
public sealed class AuthorizedApiClient(
    IHttpClientFactory httpClientFactory, AuthenticationStateProvider authenticationStateProvider)
{
    public async Task<HttpClient> CreateAsync()
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var accessToken = authState.User.FindFirst("access_token")?.Value;

        var client = httpClientFactory.CreateClient("K9CrushApi");
        if (accessToken is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }
}
