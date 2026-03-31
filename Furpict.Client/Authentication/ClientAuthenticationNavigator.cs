using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace Furpict.Client.Authentication;

internal sealed class ClientAuthenticationNavigator(IOptions<FurpictServerOptions> options, NavigationManager navigationManager)
{
    private readonly string serverBaseUrl = EnsureTrailingSlash(options.Value.BaseUrl);

    public void NavigateToLogin(string? returnUrl = null) =>
        navigationManager.NavigateTo(BuildServerUrl("Account/Login", returnUrl), forceLoad: true);

    public void NavigateToLogout(string? returnUrl = null) =>
        navigationManager.NavigateTo(BuildServerUrl("Account/ClientLogout", returnUrl), forceLoad: true);

    private string BuildServerUrl(string path, string? returnUrl)
    {
        var absolutePath = new Uri(new Uri(serverBaseUrl), path);
        var targetReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? navigationManager.Uri : returnUrl;
        return $"{absolutePath}?returnUrl={Uri.EscapeDataString(targetReturnUrl)}";
    }

    private static string EnsureTrailingSlash(string baseUrl) =>
        baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
}
