using System.Security.Claims;
using Furpict.Data;
using Furpict.Data.Entities;
using Furpict.Services.Payment;
using Furpict.Services.Storage;
using Furpict.Services.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Furpict.Endpoints;

internal static class ApiEndpoints
{
    internal static IEndpointRouteBuilder MapFurpictApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        MapAuthEndpoints(api);
        MapPetEndpoints(api);
        MapModelEndpoints(api);
        MapGenerationEndpoints(api);
        MapGalleryEndpoints(app); // gallery has mixed auth
        MapPaymentEndpoints(app); // stripe webhook is unauthenticated

        return app;
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    private static void MapAuthEndpoints(IEndpointRouteBuilder api)
    {
        api.MapGet("/auth/me", async (ClaimsPrincipal user, UserManager<ApplicationUser> userManager) =>
        {
            var applicationUser = await userManager.GetUserAsync(user);
            if (applicationUser is null)
                return Results.NotFound();

            return Results.Ok(new AuthMeResponse(
                applicationUser.Id,
                applicationUser.UserName,
                applicationUser.Email));
        });
    }

    // ── Pets ─────────────────────────────────────────────────────────────────

    private static void MapPetEndpoints(IEndpointRouteBuilder api)
    {
        api.MapGet("/pets", async (ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var pets = await db.Pets
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PetResponse(p.Id, p.Name, p.Species, p.Breed, p.CreatedAt))
                .ToListAsync();
            return Results.Ok(pets);
        });

        api.MapGet("/pets/{id:guid}", async (Guid id, ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var pet = await db.Pets
                .Include(p => p.Models)
                .Where(p => p.Id == id && p.UserId == userId)
                .FirstOrDefaultAsync();

            if (pet is null) return Results.NotFound();

            var response = new PetDetailResponse(
                pet.Id, pet.Name, pet.Species, pet.Breed, pet.CreatedAt,
                pet.Models.Select(m => new PetModelSummary(
                    m.Id, m.Status.ToString(), m.CreatedAt, m.TrainingCompletedAt)).ToList());

            return Results.Ok(response);
        });

        api.MapPost("/pets", async (CreatePetRequest request, ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Species))
                return Results.BadRequest("Name and species are required.");

            var pet = new Pet
            {
                UserId = userId,
                Name = request.Name.Trim(),
                Species = request.Species.Trim(),
                Breed = request.Breed?.Trim()
            };

            db.Pets.Add(pet);
            await db.SaveChangesAsync();

            return Results.Created($"/api/pets/{pet.Id}",
                new PetResponse(pet.Id, pet.Name, pet.Species, pet.Breed, pet.CreatedAt));
        });
    }

    // ── Models ────────────────────────────────────────────────────────────────

    private static void MapModelEndpoints(IEndpointRouteBuilder api)
    {
        api.MapGet("/models", async (ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var models = await db.PetModels
                .Include(m => m.Pet)
                .Where(m => m.Pet.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new PetModelDetailResponse(
                    m.Id, m.PetId, m.Pet.Name, m.Status.ToString(),
                    m.CreatedAt, m.PaidAt, m.TrainingStartedAt, m.TrainingCompletedAt, m.FailureReason))
                .ToListAsync();
            return Results.Ok(models);
        });

        api.MapPost("/pets/{petId:guid}/models", async (
            Guid petId,
            CreateModelRequest request,
            ClaimsPrincipal user,
            ApplicationDbContext db,
            IPaymentProvider paymentProvider,
            IHttpContextAccessor httpContextAccessor) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var pet = await db.Pets.FirstOrDefaultAsync(p => p.Id == petId && p.UserId == userId);
            if (pet is null) return Results.NotFound();

            var model = new PetModel { PetId = petId };
            db.PetModels.Add(model);
            await db.SaveChangesAsync();

            var ctx = httpContextAccessor.HttpContext!;
            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var checkoutResult = await paymentProvider.CreateCheckoutSessionAsync(
                new CreateCheckoutRequest(
                    model.Id,
                    userId,
                    SuccessUrl: $"{request.ClientBaseUrl}/pets/{petId}/models/{model.Id}?payment=success",
                    CancelUrl: $"{request.ClientBaseUrl}/pets/{petId}/models/{model.Id}?payment=cancelled"),
                ct: default);

            model.StripeCheckoutSessionId = checkoutResult.SessionId;
            await db.SaveChangesAsync();

            return Results.Created($"/api/pets/{petId}/models/{model.Id}",
                new CreateModelResponse(model.Id, checkoutResult.CheckoutUrl));
        });

        api.MapPost("/pets/{petId:guid}/models/{modelId:guid}/upload", async (
            Guid petId,
            Guid modelId,
            IFormFile file,
            ClaimsPrincipal user,
            ApplicationDbContext db,
            IImageStorageProvider storage,
            IImageModelTrainingProvider trainingProvider) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var model = await db.PetModels
                .Include(m => m.Pet)
                .FirstOrDefaultAsync(m => m.Id == modelId && m.PetId == petId && m.Pet.UserId == userId);

            if (model is null) return Results.NotFound();
            if (model.Status != ModelStatus.Paid) return Results.BadRequest("Payment required before uploading.");

            if (file.Length > 150 * 1024 * 1024)
                return Results.BadRequest("File size exceeds 150MB limit.");

            model.Status = ModelStatus.Uploading;
            model.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            await using var stream = file.OpenReadStream();
            var blobUrl = await storage.UploadTrainingZipAsync(modelId, stream);

            model.TrainingZipBlobUrl = blobUrl;

            var pet = model.Pet;
            var externalModelId = await trainingProvider.StartTrainingAsync(
                stream,
                new TrainingOptions($"furpict-{pet.Name.ToLower().Replace(" ", "-")}-{modelId}", pet.Name));

            model.ExternalModelId = externalModelId;
            model.Status = ModelStatus.Training;
            model.TrainingStartedAt = DateTimeOffset.UtcNow;
            model.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { modelId = model.Id, status = model.Status.ToString() });
        }).DisableAntiforgery();

        api.MapGet("/pets/{petId:guid}/models/{modelId:guid}/status", async (
            Guid petId, Guid modelId, ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var model = await db.PetModels
                .Include(m => m.Pet)
                .FirstOrDefaultAsync(m => m.Id == modelId && m.PetId == petId && m.Pet.UserId == userId);

            if (model is null) return Results.NotFound();

            return Results.Ok(new ModelStatusResponse(
                model.Id, model.Status.ToString(),
                model.TrainingStartedAt, model.TrainingCompletedAt, model.FailureReason));
        });
    }

    // ── Image Generation ──────────────────────────────────────────────────────

    private static void MapGenerationEndpoints(IEndpointRouteBuilder api)
    {
        api.MapPost("/models/{modelId:guid}/generate", async (
            Guid modelId,
            GenerateImageRequest request,
            ClaimsPrincipal user,
            ApplicationDbContext db,
            IImageModelTrainingProvider trainingProvider,
            IImageStorageProvider storage) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var model = await db.PetModels
                .Include(m => m.Pet)
                .FirstOrDefaultAsync(m => m.Id == modelId && m.Pet.UserId == userId);

            if (model is null) return Results.NotFound();
            if (model.Status != ModelStatus.Ready)
                return Results.BadRequest("Model is not ready for generation.");
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return Results.BadRequest("Prompt is required.");

            var generated = await trainingProvider.GenerateImageAsync(model.ExternalModelId!, request.Prompt);

            // Download and store in our blob storage
            using var httpClient = new HttpClient();
            await using var imageStream = await httpClient.GetStreamAsync(generated.ImageUrl);

            var imageId = Guid.NewGuid();
            var blobUrl = await storage.UploadGeneratedImageAsync(imageId, imageStream);

            var image = new GeneratedImage
            {
                Id = imageId,
                PetModelId = modelId,
                Prompt = request.Prompt,
                ImageBlobUrl = blobUrl,
                IsPublic = true
            };
            db.GeneratedImages.Add(image);
            await db.SaveChangesAsync();

            return Results.Created($"/api/models/{modelId}/images/{image.Id}",
                new GeneratedImageResponse(image.Id, image.Prompt, image.ImageBlobUrl, image.CreatedAt));
        });

        api.MapGet("/models/{modelId:guid}/images", async (
            Guid modelId, ClaimsPrincipal user, ApplicationDbContext db) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var model = await db.PetModels
                .Include(m => m.Pet)
                .FirstOrDefaultAsync(m => m.Id == modelId && m.Pet.UserId == userId);

            if (model is null) return Results.NotFound();

            var images = await db.GeneratedImages
                .Where(i => i.PetModelId == modelId)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new GeneratedImageResponse(i.Id, i.Prompt, i.ImageBlobUrl, i.CreatedAt))
                .ToListAsync();

            return Results.Ok(images);
        });
    }

    // ── Gallery ───────────────────────────────────────────────────────────────

    private static void MapGalleryEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gallery/featured", async (ApplicationDbContext db) =>
        {
            var images = await db.GeneratedImages
                .Where(i => i.IsFeatured)
                .OrderByDescending(i => i.CreatedAt)
                .Take(24)
                .Select(i => new GalleryImageResponse(i.Id, i.Prompt, i.ImageBlobUrl, i.ThumbnailBlobUrl, i.CreatedAt))
                .ToListAsync();
            return Results.Ok(images);
        });

        app.MapGet("/api/gallery", async (ApplicationDbContext db, int page = 1, int pageSize = 24) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 48);
            page = Math.Max(1, page);

            var images = await db.GeneratedImages
                .Where(i => i.IsPublic)
                .OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new GalleryImageResponse(i.Id, i.Prompt, i.ImageBlobUrl, i.ThumbnailBlobUrl, i.CreatedAt))
                .ToListAsync();
            return Results.Ok(images);
        }).RequireAuthorization();
    }

    // ── Payments ──────────────────────────────────────────────────────────────

    private static void MapPaymentEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/stripe", async (
            HttpContext ctx,
            ApplicationDbContext db,
            IPaymentProvider paymentProvider) =>
        {
            var payload = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            var signature = ctx.Request.Headers["Stripe-Signature"].ToString();

            var result = await paymentProvider.VerifyWebhookAsync(payload, signature);

            if (!result.IsValid)
                return Results.BadRequest("Invalid webhook signature.");

            if (result.CheckoutSessionId is null)
                return Results.Ok();

            var model = await db.PetModels
                .FirstOrDefaultAsync(m => m.StripeCheckoutSessionId == result.CheckoutSessionId);

            if (model is null)
                return Results.Ok();

            if (model.Status == ModelStatus.Pending)
            {
                model.Status = ModelStatus.Paid;
                model.StripePaymentIntentId = result.PaymentIntentId;
                model.PaidAt = DateTimeOffset.UtcNow;
                model.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }

            return Results.Ok();
        }).DisableAntiforgery();
    }
}

// ── Request / Response DTOs ──────────────────────────────────────────────────

internal sealed record AuthMeResponse(string UserId, string? UserName, string? Email);
internal sealed record PetResponse(Guid Id, string Name, string Species, string? Breed, DateTimeOffset CreatedAt);
internal sealed record PetDetailResponse(Guid Id, string Name, string Species, string? Breed, DateTimeOffset CreatedAt, IReadOnlyList<PetModelSummary> Models);
internal sealed record PetModelSummary(Guid Id, string Status, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);
internal sealed record PetModelDetailResponse(Guid Id, Guid PetId, string PetName, string Status, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt, DateTimeOffset? TrainingStartedAt, DateTimeOffset? CompletedAt, string? FailureReason);
internal sealed record ModelStatusResponse(Guid Id, string Status, DateTimeOffset? TrainingStartedAt, DateTimeOffset? CompletedAt, string? FailureReason);
internal sealed record CreatePetRequest(string Name, string Species, string? Breed);
internal sealed record CreateModelRequest(string ClientBaseUrl);
internal sealed record CreateModelResponse(Guid ModelId, string CheckoutUrl);
internal sealed record GenerateImageRequest(string Prompt);
internal sealed record GeneratedImageResponse(Guid Id, string Prompt, string ImageUrl, DateTimeOffset CreatedAt);
internal sealed record GalleryImageResponse(Guid Id, string Prompt, string ImageUrl, string? ThumbnailUrl, DateTimeOffset CreatedAt);
