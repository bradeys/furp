using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Furp.Components.Account;
using Furp.Components.Account.Pages;
using Furp.Components.Account.Pages.Manage;
using Furp.Data;

namespace Microsoft.AspNetCore.Routing;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    // These endpoints are required by the Identity Razor components defined in the /Components/Account/Pages directory of this project.
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var accountGroup = endpoints.MapGroup("/Account");

        accountGroup.MapPost("/PerformExternalLogin", (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string provider,
            [FromForm] string returnUrl) =>
        {
            IEnumerable<KeyValuePair<string, StringValues>> query = [
                new("ReturnUrl", returnUrl),
                new("Action", ExternalLogin.LoginCallbackAction)];

            var redirectUrl = UriHelper.BuildRelative(
                context.Request.PathBase,
                "/Account/ExternalLogin",
                QueryString.Create(query));

            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return TypedResults.Challenge(properties, [provider]);
        });

        accountGroup.MapPost("/Logout", async (
            ClaimsPrincipal user,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IdentityRedirectUrlValidator redirectUrlValidator,
            [FromForm] string returnUrl) =>
        {
            await signInManager.SignOutAsync();
            var safeReturnUrl = redirectUrlValidator.GetSafeRedirectUri(returnUrl);
            return TypedResults.Redirect(safeReturnUrl);
        });

        accountGroup.MapGet("/ClientAuthenticationState", (ClaimsPrincipal user) =>
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return TypedResults.Ok(new ClientAuthenticationStateResponse(false, []));
            }

            var claims = user.Claims
                .Select(claim => new ClientAuthenticationClaim(claim.Type, claim.Value))
                .ToArray();

            return TypedResults.Ok(new ClientAuthenticationStateResponse(true, claims));
        });

        accountGroup.MapGet("/ClientLogout", (
            HttpContext context,
            [FromServices] IAntiforgery antiforgery,
            [FromServices] IdentityRedirectUrlValidator redirectUrlValidator,
            [FromQuery] string? returnUrl) =>
        {
            var safeReturnUrl = redirectUrlValidator.GetSafeRedirectUri(returnUrl);
            var antiforgeryTokens = antiforgery.GetAndStoreTokens(context);

            if (string.IsNullOrEmpty(antiforgeryTokens.FormFieldName) || string.IsNullOrEmpty(antiforgeryTokens.RequestToken))
            {
                throw new InvalidOperationException("Unable to generate an antiforgery token for client logout.");
            }

            var encodedAction = HtmlEncoder.Default.Encode($"{context.Request.PathBase}/Account/Logout");
            var encodedFieldName = HtmlEncoder.Default.Encode(antiforgeryTokens.FormFieldName);
            var encodedRequestToken = HtmlEncoder.Default.Encode(antiforgeryTokens.RequestToken);
            var encodedReturnUrl = HtmlEncoder.Default.Encode(safeReturnUrl);

            var html = $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8" />
                    <title>Signing out...</title>
                </head>
                <body>
                    <form id="logout-form" method="post" action="{encodedAction}">
                        <input type="hidden" name="{encodedFieldName}" value="{encodedRequestToken}" />
                        <input type="hidden" name="ReturnUrl" value="{encodedReturnUrl}" />
                        <noscript>
                            <button type="submit">Continue</button>
                        </noscript>
                    </form>
                    <script>document.getElementById('logout-form').submit();</script>
                </body>
                </html>
                """;

            return Results.Content(html, "text/html");
        });

        accountGroup.MapPost("/PasskeyCreationOptions", async (
            HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(context);

            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
            }

            var userId = await userManager.GetUserIdAsync(user);
            var userName = await userManager.GetUserNameAsync(user) ?? "User";
            var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
            {
                Id = userId,
                Name = userName,
                DisplayName = userName
            });
            return TypedResults.Content(optionsJson, contentType: "application/json");
        });

        accountGroup.MapPost("/PasskeyRequestOptions", async (
            HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IAntiforgery antiforgery,
            [FromQuery] string? username) =>
        {
            await antiforgery.ValidateRequestAsync(context);

            var user = string.IsNullOrEmpty(username) ? null : await userManager.FindByNameAsync(username);
            var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
            return TypedResults.Content(optionsJson, contentType: "application/json");
        });

        var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

        manageGroup.MapPost("/LinkExternalLogin", async (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string provider) =>
        {
            // Clear the existing external cookie to ensure a clean login process
            await context.SignOutAsync(IdentityConstants.ExternalScheme);

            var redirectUrl = UriHelper.BuildRelative(
                context.Request.PathBase,
                "/Account/Manage/ExternalLogins",
                QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
            return TypedResults.Challenge(properties, [provider]);
        });

        var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var downloadLogger = loggerFactory.CreateLogger("DownloadPersonalData");

        manageGroup.MapPost("/DownloadPersonalData", async (
            HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] AuthenticationStateProvider authenticationStateProvider) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
            }

            var userId = await userManager.GetUserIdAsync(user);
            downloadLogger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

            // Only include personal data for download
            var personalData = new Dictionary<string, string>();
            var personalDataProps = typeof(ApplicationUser).GetProperties().Where(
                prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
            foreach (var p in personalDataProps)
            {
                personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
            }

            var logins = await userManager.GetLoginsAsync(user);
            foreach (var l in logins)
            {
                personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
            }

            personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);
            var fileBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);

            context.Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
            return TypedResults.File(fileBytes, contentType: "application/json", fileDownloadName: "PersonalData.json");
        });

        return accountGroup;
    }
}

internal sealed record ClientAuthenticationStateResponse(bool IsAuthenticated, IReadOnlyList<ClientAuthenticationClaim> Claims);

internal sealed record ClientAuthenticationClaim(string Type, string Value);
