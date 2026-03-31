using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Options;
using Furp.Client;
using Furp.Client.Authentication;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

var furpServerOptions = builder.Configuration
    .GetSection(FurpServerOptions.SectionName)
    .Get<FurpServerOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{FurpServerOptions.SectionName}' is missing.");

if (!Uri.TryCreate(furpServerOptions.BaseUrl, UriKind.Absolute, out var furpServerBaseUri))
{
    throw new InvalidOperationException($"Configuration value '{FurpServerOptions.SectionName}:BaseUrl' must be an absolute URL.");
}

builder.Services.AddSingleton(Options.Create(furpServerOptions));
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
builder.Services.AddScoped<ClientAuthenticationNavigator>();
builder.Services.AddTransient<IncludeCredentialsHandler>();

builder.Services.AddHttpClient(FurpServerOptions.HttpClientName, client => client.BaseAddress = furpServerBaseUri)
    .AddHttpMessageHandler<IncludeCredentialsHandler>();

await builder.Build().RunAsync();
