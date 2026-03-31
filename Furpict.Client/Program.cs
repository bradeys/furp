using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Options;
using Furpict.Client;
using Furpict.Client.Authentication;
using Furpict.Client.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

var furpServerOptions = builder.Configuration
    .GetSection(FurpictServerOptions.SectionName)
    .Get<FurpictServerOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{FurpictServerOptions.SectionName}' is missing.");

if (!Uri.TryCreate(furpServerOptions.BaseUrl, UriKind.Absolute, out var furpServerBaseUri))
{
    throw new InvalidOperationException($"Configuration value '{FurpictServerOptions.SectionName}:BaseUrl' must be an absolute URL.");
}

builder.Services.AddSingleton(Options.Create(furpServerOptions));
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
builder.Services.AddScoped<ClientAuthenticationNavigator>();
builder.Services.AddTransient<IncludeCredentialsHandler>();

builder.Services.AddHttpClient(FurpictServerOptions.HttpClientName, client => client.BaseAddress = furpServerBaseUri)
    .AddHttpMessageHandler<IncludeCredentialsHandler>();

builder.Services.AddScoped<FurpictApiClient>();

await builder.Build().RunAsync();
