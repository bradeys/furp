# Furpict — AI Pet Image Model Builder

## Problem Statement

Transform the existing Furp Blazor scaffold into **Furpict**, a full-stack app where users can:

1. Create an account
2. Upload 10 photos of their pet (various angles: front, left, right, top, back, face, left profile, right profile, etc.)
3. Pay a one-time fee (Stripe) per pet model
4. Have those photos zipped **client-side in WASM** and uploaded to the server
5. The server initiates an AI model training job via Black Forest Labs Flux.2 API (behind an abstraction)
6. Poll for training status ("come back and check when your model is done")
7. Once trained, send text prompts ("show my dog surfing at the beach") to generate images
8. Browse a public gallery of featured/curated images; full gallery requires login

## Architecture Decisions

| Decision | Choice |
|---|---|
| Payment | Stripe — one-time purchase per model |
| Image storage (generated) | Azure Blob Storage |
| AI API | Abstract interface, BFL Flux.2 as default implementation |
| Zip/upload flow | Client-side zip in WASM → upload zip to Furpict server → server calls BFL API |
| Training status | WASM polls server endpoint; server stores status in DB |
| Gallery visibility | Featured images public; full gallery requires auth |
| Branding | Rename both projects from Furp → Furpict |
| Model training status | Basic polling from WASM client |

## High-Level Architecture

```
┌─────────────────────────────────────────┐
│            Furpict.Client (WASM)         │
│  - Photo upload UI (10 angles)          │
│  - Client-side zip (System.IO.Compression) │
│  - Stripe Checkout redirect             │
│  - Model status polling                 │
│  - Prompt UI for image generation       │
│  - Gallery browsing                     │
├─────────────────────────────────────────┤
│         HTTP (cookies for auth)         │
├─────────────────────────────────────────┤
│            Furpict Server (ASP.NET)     │
│  - ASP.NET Core Identity (existing)     │
│  - Minimal API endpoints                │
│  - Stripe webhook + checkout session    │
│  - AI training orchestration            │
│  - Azure Blob Storage integration       │
│  - Background status polling of BFL     │
│  - EF Core + SQLite                     │
├─────────────────────────────────────────┤
│         External Services               │
│  - Stripe (payments)                    │
│  - Black Forest Labs Flux.2 (AI)        │
│  - Azure Blob Storage (images)          │
└─────────────────────────────────────────┘
```

## Data Model (EF Core Additions)

### Pet
- `Id` (Guid, PK)
- `UserId` (FK → ApplicationUser)
- `Name` (string)
- `Species` (string — dog, cat, etc.)
- `Breed` (string, nullable)
- `CreatedAt` (DateTimeOffset)

### PetModel (the AI training model)
- `Id` (Guid, PK)
- `PetId` (FK → Pet)
- `Status` (enum: Pending, Paid, Uploading, Training, Ready, Failed)
- `ExternalModelId` (string, nullable — BFL's model ID)
- `TrainingZipBlobUrl` (string — Azure Blob URL of the uploaded zip)
- `StripePaymentIntentId` (string, nullable)
- `StripeCheckoutSessionId` (string, nullable)
- `PaidAt` (DateTimeOffset, nullable)
- `TrainingStartedAt` (DateTimeOffset, nullable)
- `TrainingCompletedAt` (DateTimeOffset, nullable)
- `FailureReason` (string, nullable)
- `CreatedAt` (DateTimeOffset)
- `UpdatedAt` (DateTimeOffset)

### GeneratedImage
- `Id` (Guid, PK)
- `PetModelId` (FK → PetModel)
- `Prompt` (string)
- `ImageBlobUrl` (string — Azure Blob URL)
- `ThumbnailBlobUrl` (string, nullable)
- `IsFeatured` (bool — shown in public gallery)
- `IsPublic` (bool — shown in authenticated gallery)
- `CreatedAt` (DateTimeOffset)

### Order (Stripe tracking)
- `Id` (Guid, PK)
- `UserId` (FK → ApplicationUser)
- `PetModelId` (FK → PetModel)
- `AmountCents` (int)
- `Currency` (string)
- `StripeCheckoutSessionId` (string)
- `StripePaymentIntentId` (string, nullable)
- `Status` (enum: Pending, Completed, Failed, Refunded)
- `CreatedAt` (DateTimeOffset)
- `CompletedAt` (DateTimeOffset, nullable)

## API Endpoints (Server — Minimal API)

### Pets
- `POST /api/pets` — create a pet profile
- `GET /api/pets` — list user's pets
- `GET /api/pets/{id}` — get pet details

### Pet Models (Training)
- `POST /api/pets/{petId}/models` — create a new model (returns model ID + Stripe checkout URL)
- `POST /api/pets/{petId}/models/{modelId}/upload` — upload the zipped training photos
- `GET /api/pets/{petId}/models/{modelId}/status` — poll training status
- `GET /api/models` — list all user's models across pets

### Image Generation
- `POST /api/models/{modelId}/generate` — submit a prompt
- `GET /api/models/{modelId}/images` — list generated images for a model

### Gallery
- `GET /api/gallery/featured` — public, no auth — featured/curated images
- `GET /api/gallery` — requires auth — full community gallery

### Payments (Stripe)
- `POST /api/checkout/create-session` — create Stripe Checkout session
- `POST /api/webhooks/stripe` — Stripe webhook endpoint (payment confirmation)

### Auth (existing, keep as-is)
- Existing Identity endpoints remain

## Server Services (Abstractions)

### `IImageModelTrainingProvider`
```csharp
public interface IImageModelTrainingProvider
{
    Task<string> StartTrainingAsync(Stream trainingZip, TrainingOptions options, CancellationToken ct);
    Task<TrainingStatus> GetTrainingStatusAsync(string externalModelId, CancellationToken ct);
    Task<GeneratedImageResult> GenerateImageAsync(string externalModelId, string prompt, CancellationToken ct);
}
```
Default implementation: `BflFluxTrainingProvider`

### `IImageStorageProvider`
```csharp
public interface IImageStorageProvider
{
    Task<string> UploadTrainingZipAsync(Guid modelId, Stream zipStream, CancellationToken ct);
    Task<string> UploadGeneratedImageAsync(Guid imageId, Stream imageStream, CancellationToken ct);
    Task<Stream> DownloadAsync(string blobUrl, CancellationToken ct);
}
```
Default implementation: `AzureBlobImageStorageProvider`

### `IPaymentProvider`
```csharp
public interface IPaymentProvider
{
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct);
    Task<PaymentVerificationResult> VerifyWebhookAsync(string payload, string signature, CancellationToken ct);
}
```
Default implementation: `StripePaymentProvider`

### Background Service: `ModelTrainingStatusPoller`
A hosted background service that periodically checks BFL API for training status updates on models that are in `Training` state, and updates the DB when complete.

## Client-Side WASM Pages & Components

### Pages
1. **`/` (Home)** — Landing page with hero, featured gallery preview, CTA to sign up
2. **`/gallery`** — Public featured gallery + full gallery (auth-gated)
3. **`/pets`** — List user's pets (auth required)
4. **`/pets/new`** — Create a new pet profile
5. **`/pets/{petId}`** — Pet detail with models list
6. **`/pets/{petId}/models/new`** — Upload 10 photos flow + payment
7. **`/pets/{petId}/models/{modelId}`** — Model status + prompt UI (when ready)
8. **`/pets/{petId}/models/{modelId}/generate`** — Prompt input + generated images

### Key Components
- `PhotoUploadGrid` — 10-slot upload grid with labeled angle positions
- `ModelStatusBadge` — shows training status with color coding
- `PromptInput` — text input for image generation prompts
- `ImageGalleryGrid` — responsive grid of generated images
- `FeaturedImageCard` — card for gallery display
- `PaymentButton` — Stripe checkout redirect
- `PetCard` — pet summary card

### Client-Side Zip Flow
1. User selects/drops 10 images into the `PhotoUploadGrid`
2. Images are validated client-side (file type, size limits)
3. On submit, `System.IO.Compression.ZipArchive` creates a zip in-memory
4. Zip is uploaded via `HttpClient` to `POST /api/pets/{petId}/models/{modelId}/upload`
5. Server receives zip, stores in Azure Blob, kicks off training

## Implementation Phases

### Phase 1: Project Rename & Scaffold
- Rename Furp → Furpict (solution, projects, namespaces, config)
- Update solution file, csproj references, namespaces
- Clean up demo pages (Counter, Weather) — remove or replace
- Update branding in layouts (app name, theme)
- Verify build succeeds after rename

### Phase 2: Data Model & Migrations
- Add `Pet`, `PetModel`, `GeneratedImage`, `Order` entities
- Add status enums (`ModelStatus`, `OrderStatus`)
- Update `ApplicationDbContext` with new DbSets
- Update `ApplicationUser` with navigation properties
- Create EF Core migration
- Seed any reference data if needed

### Phase 3: Server Service Abstractions
- Define `IImageModelTrainingProvider` interface
- Define `IImageStorageProvider` interface
- Define `IPaymentProvider` interface
- Create stub/mock implementations for local development
- Create `BflFluxTrainingProvider` (calls BFL API)
- Create `AzureBlobImageStorageProvider`
- Create `StripePaymentProvider`
- Register services in DI

### Phase 4: Server API Endpoints
- Pet CRUD endpoints
- Model creation + upload endpoint
- Model status polling endpoint
- Image generation endpoint
- Gallery endpoints (public featured + auth full)
- Stripe checkout session creation
- Stripe webhook handler
- Background service for model training status polling

### Phase 5: Client — Pet Management Pages
- Pet list page (`/pets`)
- Create pet page (`/pets/new`)
- Pet detail page (`/pets/{petId}`)
- Navigation updates
- `PetCard` component

### Phase 6: Client — Photo Upload & Payment Flow
- `PhotoUploadGrid` component (10 labeled slots)
- Client-side image validation (type, size, dimensions)
- Client-side zip creation using `System.IO.Compression`
- New model page with upload + Stripe payment flow
- Payment success/cancel redirect handling

### Phase 7: Client — Model Status & Prompt UI
- Model detail page with status polling
- `ModelStatusBadge` component
- Prompt input + submission when model is ready
- Generated images display
- `PromptInput` component

### Phase 8: Client — Gallery & Landing Page
- Landing page redesign with hero + featured images
- Public featured gallery page
- Authenticated full gallery page
- `FeaturedImageCard` and `ImageGalleryGrid` components

### Phase 9: Polish & Integration
- Error handling across all flows
- Loading states and skeletons
- Mobile responsiveness
- Theme refinement (pet-friendly branding)
- Configuration for all external services (BFL, Stripe, Azure Blob)
- Security review (auth on all endpoints, input validation)

## Configuration Required

```json
// Server appsettings.json additions
{
  "Stripe": {
    "SecretKey": "",
    "WebhookSecret": "",
    "PriceAmountCents": 2999,
    "Currency": "usd"
  },
  "BflFlux": {
    "ApiKey": "",
    "BaseUrl": "https://api.bfl.ml/v1",
    "TrainingCallbackUrl": ""
  },
  "AzureBlobStorage": {
    "ConnectionString": "",
    "TrainingContainer": "training-zips",
    "GeneratedImagesContainer": "generated-images"
  }
}
```

## NuGet Packages to Add

### Server (Furpict.csproj)
- `Azure.Storage.Blobs` — Azure Blob Storage SDK
- `Stripe.net` — Stripe .NET SDK

### Client (Furpict.Client.csproj)
- (No additional packages — `System.IO.Compression` is already available in .NET WASM)

## Notes & Considerations

- **File size limits:** Should enforce max ~10MB per photo, ~100MB total zip. Configure in both client validation and server.
- **BFL API specifics:** The BFL Flux.2 fine-tuning API may have specific requirements for image format/resolution. Plan to validate and resize on the client if needed.
- **Stripe Checkout:** Use Stripe Checkout (hosted page redirect) rather than Stripe Elements for simplicity. User is redirected to Stripe, then back.
- **Gallery moderation:** `IsFeatured` flag allows manual curation. Could add admin page later.
- **WASM bundle size:** Monitor bundle size — `System.IO.Compression` adds some weight but is acceptable.
- **Rate limiting:** Add rate limiting on generation endpoints to prevent abuse.
