# Copilot Instructions

## Project Overview

Furpict is a full-stack Blazor application targeting .NET 10. It allows users to build AI image models of their pets using Black Forest Labs Flux.2. It consists of two projects:

- **`Furpict/`** — ASP.NET Core server hosting Blazor Server components, minimal API endpoints, EF Core + SQLite, ASP.NET Core Identity, Stripe payments, and Azure Blob Storage
- **`Furpict.Client/`** — Blazor WebAssembly client that communicates with the server over HTTP (cookies for auth)

## Build & Run

```bash
# Build everything
dotnet build furpict.sln

# Run the server (serves both SSR and WASM client)
dotnet run --project Furpict/Furpict.csproj

# Watch mode (hot reload)
dotnet watch --project Furpict/Furpict.csproj
```

Server runs on `http://localhost:5001` / `https://localhost:7001`. The database (`Furpict/Data/app.db`) is created automatically on first run via EF Core.

## Architecture

The server hosts everything. The WASM client (`Furpict.Client`) is served as static assets and calls back to the server for data and auth state.

**Authentication flow:** The WASM client has no session of its own — it calls `GET /Account/ClientAuthenticationState` to get the user's claims from the server's cookie session. `IncludeCredentialsHandler` ensures cookies are included in all WASM `HttpClient` requests.

**Core user flow:**
1. User registers/logs in
2. Creates a pet profile (`POST /api/pets`)
3. Creates an AI model — triggers Stripe Checkout payment
4. After payment, uploads 10 photos (zipped client-side in WASM) to server
5. Server starts BFL Flux.2 training job
6. WASM polls `/api/pets/{petId}/models/{modelId}/status` until Ready
7. User submits prompts to generate images

**API endpoints (server):**
- `GET/POST /api/pets` — pet management
- `GET /api/pets/{id}` — pet detail
- `POST /api/pets/{petId}/models` — create model + Stripe checkout
- `POST /api/pets/{petId}/models/{modelId}/upload` — upload training zip
- `GET /api/pets/{petId}/models/{modelId}/status` — poll status
- `POST /api/models/{modelId}/generate` — generate image from prompt
- `GET /api/models/{modelId}/images` — list generated images
- `GET /api/gallery/featured` — public featured gallery
- `GET /api/gallery` — authenticated community gallery
- `POST /api/webhooks/stripe` — Stripe webhook
- `GET /api/auth/me` — current user info

**Data layer:** EF Core with SQLite. `ApplicationUser` extends `IdentityUser`. Entities: `Pet`, `PetModel`, `GeneratedImage`, `Order`. Migrations live in `Furpict/Data/Migrations/`.

## Key Conventions

- **Render modes:** Server components use `@rendermode="InteractiveServer"`. The WASM client uses `@rendermode="InteractiveWebAssembly"`. Don't mix these up between projects.
- **Auth in WASM:** Use `ServerAuthenticationStateProvider` (not a local provider). Navigation to login/logout goes through `ClientAuthenticationNavigator` which redirects to server-side Identity endpoints.
- **Records for DTOs:** Immutable data passed between layers (e.g. `PetResponse`, `GeneratedImageResponse`) are C# `record` types.
- **Internal sealed classes:** Non-public implementation types are marked `internal sealed` — maintain this pattern.
- **UI components:** MudBlazor v9 is used throughout. Use MudBlazor components rather than plain HTML for UI elements.
- **Nullable enabled:** Both projects have `<Nullable>enable</Nullable>`. Treat all nullable warnings as real issues.
- **Implicit usings:** Common namespaces are auto-imported. Don't add redundant `using` statements.
- **Client config:** The WASM client reads its server base URL from `Furpict.Client/wwwroot/appsettings.Development.json` → `FurpictServer:BaseUrl`.
- **String interpolation in Razor HTML attributes:** Do NOT use `$"..."` inside double-quoted HTML attributes. Use string concatenation (`"/path/" + id`) or extract navigation to a helper method.

## Service Abstractions

- `IImageModelTrainingProvider` → `BflFluxTrainingProvider` (calls BFL Flux.2 API)
- `IImageStorageProvider` → `AzureBlobImageStorageProvider` (Azure Blob Storage)
- `IPaymentProvider` → `StripePaymentProvider` (Stripe Checkout)
- `ModelTrainingStatusPoller` — background service that polls BFL API every 2 min for Training models

## Configuration Required (use user secrets or appsettings.Development.json)

```json
{
  "Stripe": { "SecretKey": "", "WebhookSecret": "", "PriceAmountCents": 2999 },
  "BflFlux": { "ApiKey": "", "BaseUrl": "https://api.bfl.ml/v1" },
  "AzureBlobStorage": { "ConnectionString": "", "TrainingContainer": "training-zips", "GeneratedImagesContainer": "generated-images" }
}
```

## MCP Servers

This repo has Playwright and SQLite MCP servers configured in `.vscode/mcp.json`:
- **Playwright** — for browser automation / end-to-end testing
- **SQLite** — points to `Furpict/Data/app.db` for direct database inspection
