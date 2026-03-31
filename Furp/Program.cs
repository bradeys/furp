using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Furp.Components;
using Furp.Components.Account;
using Furp.Data;
using Furp.Services;

var builder = WebApplication.CreateBuilder(args);
var clientApplicationOptions = builder.Configuration
    .GetSection(ClientApplicationOptions.SectionName)
    .Get<ClientApplicationOptions>() ?? new();

// Add MudBlazor services
builder.Services.AddMudServices();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.Configure<ClientApplicationOptions>(
    builder.Configuration.GetSection(ClientApplicationOptions.SectionName));
builder.Services.AddSingleton<IdentityRedirectUrlValidator>();

if (clientApplicationOptions.AllowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ClientApplications", policy =>
        {
            policy.WithOrigins(clientApplicationOptions.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

if (clientApplicationOptions.AllowedOrigins.Length > 0)
{
    app.UseCors("ClientApplications");
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapGet("/api/weather", (WeatherForecastService weatherForecastService) =>
    Results.Ok(weatherForecastService.GetForecasts())).RequireAuthorization();

app.MapGet("/api/auth/me", async (ClaimsPrincipal user, UserManager<ApplicationUser> userManager) =>
{
    var applicationUser = await userManager.GetUserAsync(user);
    if (applicationUser is null)
    {
        return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(user)}'.");
    }

    var claims = user.Claims
        .Select(claim => new AuthenticatedUserClaim(claim.Type, claim.Value))
        .ToArray();

    return Results.Ok(new AuthenticatedUserResponse(
        applicationUser.Id,
        await userManager.GetUserNameAsync(applicationUser),
        await userManager.GetEmailAsync(applicationUser),
        claims));
})
.RequireAuthorization();

app.Run();

internal sealed record AuthenticatedUserResponse(
    string UserId,
    string? UserName,
    string? Email,
    IReadOnlyList<AuthenticatedUserClaim> Claims);

internal sealed record AuthenticatedUserClaim(string Type, string Value);
