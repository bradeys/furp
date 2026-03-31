using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Furpict.Components;
using Furpict.Components.Account;
using Furpict.Data;
using Furpict.Endpoints;
using Furpict.Services;
using Furpict.Services.Payment;
using Furpict.Services.Storage;
using Furpict.Services.Training;

var builder = WebApplication.CreateBuilder(args);
var clientApplicationOptions = builder.Configuration
    .GetSection(ClientApplicationOptions.SectionName)
    .Get<ClientApplicationOptions>() ?? new();

builder.Services.AddMudServices();
builder.Services.Configure<ClientApplicationOptions>(
    builder.Configuration.GetSection(ClientApplicationOptions.SectionName));
builder.Services.AddSingleton<IdentityRedirectUrlValidator>();

// External service options
builder.Services.Configure<BflFluxOptions>(builder.Configuration.GetSection(BflFluxOptions.SectionName));
builder.Services.Configure<AzureBlobStorageOptions>(builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName));
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));

// AI training provider
var bflOptions = builder.Configuration.GetSection(BflFluxOptions.SectionName).Get<BflFluxOptions>() ?? new();
builder.Services.AddHttpClient("BflFlux", client =>
{
    client.BaseAddress = new Uri(bflOptions.BaseUrl.TrimEnd('/') + '/');
    client.DefaultRequestHeaders.Add("x-key", bflOptions.ApiKey);
});
builder.Services.AddScoped<IImageModelTrainingProvider, BflFluxTrainingProvider>();

// Storage
builder.Services.AddScoped<IImageStorageProvider, AzureBlobImageStorageProvider>();

// Payment
builder.Services.AddScoped<IPaymentProvider, StripePaymentProvider>();

// Background services
builder.Services.AddHostedService<ModelTrainingStatusPoller>();

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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
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
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
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

app.MapAdditionalIdentityEndpoints();

// API endpoints are registered in separate endpoint files
app.MapFurpictApiEndpoints();

app.Run();

internal sealed record AuthenticatedUserResponse(
    string UserId,
    string? UserName,
    string? Email,
    IReadOnlyList<AuthenticatedUserClaim> Claims);

internal sealed record AuthenticatedUserClaim(string Type, string Value);
