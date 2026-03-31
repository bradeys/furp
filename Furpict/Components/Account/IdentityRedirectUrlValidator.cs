using Microsoft.Extensions.Options;

namespace Furpict.Components.Account;

internal sealed class IdentityRedirectUrlValidator(IOptions<ClientApplicationOptions> options)
{
    private readonly Uri[] allowedOrigins = options.Value.AllowedOrigins
        .Select(origin => Uri.TryCreate(origin, UriKind.Absolute, out var parsedOrigin) ? parsedOrigin : null)
        .Where(parsedOrigin => parsedOrigin is not null)
        .Cast<Uri>()
        .ToArray();

    public string GetSafeRedirectUri(string? uri, string fallback = "/")
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return fallback;
        }

        if (Uri.TryCreate(uri, UriKind.Relative, out _))
        {
            return uri;
        }

        return IsAllowedExternalUri(uri) ? uri : fallback;
    }

    public bool IsAllowedExternalUri(string? uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) &&
        allowedOrigins.Any(allowedOrigin =>
            Uri.Compare(
                allowedOrigin,
                parsedUri,
                UriComponents.SchemeAndServer,
                UriFormat.Unescaped,
                StringComparison.OrdinalIgnoreCase) == 0);
}
