using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Furp.Client.Authentication;

internal sealed class ServerAuthenticationStateProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<ServerAuthenticationStateProvider> logger)
    : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var client = httpClientFactory.CreateClient(FurpServerOptions.HttpClientName);

        ClientAuthenticationStateResponse? response;
        try
        {
            response = await client.GetFromJsonAsync<ClientAuthenticationStateResponse>("Account/ClientAuthenticationState");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Unable to reach the Furp server to determine authentication state.");
            return Anonymous;
        }

        if (response is null)
        {
            throw new InvalidOperationException("The Furp server returned an empty authentication state response.");
        }

        if (!response.IsAuthenticated)
        {
            return Anonymous;
        }

        var identity = new ClaimsIdentity(
            response.Claims.Select(claim => new Claim(claim.Type, claim.Value)),
            authenticationType: "Identity.Application",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}

internal sealed record ClientAuthenticationStateResponse(bool IsAuthenticated, IReadOnlyList<ClientAuthenticationClaim> Claims);

internal sealed record ClientAuthenticationClaim(string Type, string Value);
