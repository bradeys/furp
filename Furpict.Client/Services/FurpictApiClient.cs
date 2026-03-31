using System.Net.Http.Json;
using Furpict.Client.Authentication;
using Furpict.Client.Models;

namespace Furpict.Client.Services;

internal sealed class FurpictApiClient(IHttpClientFactory httpClientFactory)
{
    private HttpClient Client => httpClientFactory.CreateClient(FurpictServerOptions.HttpClientName);

    // ── Pets ──────────────────────────────────────────────────────────────────

    public Task<List<PetResponse>?> GetPetsAsync(CancellationToken ct = default) =>
        Client.GetFromJsonAsync<List<PetResponse>>("api/pets", ct);

    public Task<PetDetailResponse?> GetPetAsync(Guid petId, CancellationToken ct = default) =>
        Client.GetFromJsonAsync<PetDetailResponse>($"api/pets/{petId}", ct);

    public async Task<PetResponse?> CreatePetAsync(string name, string species, string? breed, CancellationToken ct = default)
    {
        var response = await Client.PostAsJsonAsync("api/pets", new { name, species, breed }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PetResponse>(ct);
    }

    // ── Models ────────────────────────────────────────────────────────────────

    public Task<List<PetModelDetailResponse>?> GetAllModelsAsync(CancellationToken ct = default) =>
        Client.GetFromJsonAsync<List<PetModelDetailResponse>>("api/models", ct);

    public async Task<CreateModelResponse?> CreateModelAsync(Guid petId, string clientBaseUrl, CancellationToken ct = default)
    {
        var response = await Client.PostAsJsonAsync($"api/pets/{petId}/models", new { clientBaseUrl }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateModelResponse>(ct);
    }

    public Task<ModelStatusResponse?> GetModelStatusAsync(Guid petId, Guid modelId, CancellationToken ct = default) =>
        Client.GetFromJsonAsync<ModelStatusResponse>($"api/pets/{petId}/models/{modelId}/status", ct);

    // ── Images ────────────────────────────────────────────────────────────────

    public async Task<GeneratedImageResponse?> GenerateImageAsync(Guid modelId, string prompt, CancellationToken ct = default)
    {
        var response = await Client.PostAsJsonAsync($"api/models/{modelId}/generate", new { prompt }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GeneratedImageResponse>(ct);
    }

    public Task<List<GeneratedImageResponse>?> GetGeneratedImagesAsync(Guid modelId, CancellationToken ct = default) =>
        Client.GetFromJsonAsync<List<GeneratedImageResponse>>($"api/models/{modelId}/images", ct);

    // ── Gallery ───────────────────────────────────────────────────────────────

    public Task<List<GalleryImageResponse>?> GetFeaturedImagesAsync(CancellationToken ct = default) =>
        Client.GetFromJsonAsync<List<GalleryImageResponse>>("api/gallery/featured", ct);

    public Task<List<GalleryImageResponse>?> GetGalleryImagesAsync(int page = 1, CancellationToken ct = default) =>
        Client.GetFromJsonAsync<List<GalleryImageResponse>>($"api/gallery?page={page}", ct);

    // ── Upload ────────────────────────────────────────────────────────────────

    public async Task UploadTrainingZipAsync(Guid petId, Guid modelId, byte[] zipBytes, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        using var byteContent = new ByteArrayContent(zipBytes);
        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        content.Add(byteContent, "file", "training.zip");

        var response = await Client.PostAsync($"api/pets/{petId}/models/{modelId}/upload", content, ct);
        response.EnsureSuccessStatusCode();
    }
}
