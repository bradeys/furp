using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Furpict.Services.Training;

public sealed class BflFluxOptions
{
    public const string SectionName = "BflFlux";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.bfl.ml/v1";
}

internal sealed class BflFluxTrainingProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<BflFluxTrainingProvider> logger)
    : IImageModelTrainingProvider
{
    private const string HttpClientName = "BflFlux";

    public async Task<string> StartTrainingAsync(Stream trainingZip, TrainingOptions trainingOptions, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(trainingZip), "training_data", "training.zip");
        content.Add(new StringContent(trainingOptions.ModelName), "model_name");
        content.Add(new StringContent(trainingOptions.TriggerWord), "trigger_word");

        var response = await client.PostAsync("fine-tune", content, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BflTrainingResponse>(ct)
            ?? throw new InvalidOperationException("BFL API returned an empty training response.");

        logger.LogInformation("BFL training job started: {ModelId}", result.Id);
        return result.Id;
    }

    public async Task<TrainingStatusResult> GetTrainingStatusAsync(string externalModelId, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var response = await client.GetAsync($"fine-tune/{externalModelId}", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BflStatusResponse>(ct)
            ?? throw new InvalidOperationException("BFL API returned an empty status response.");

        return result.Status switch
        {
            "pending" or "processing" => new TrainingStatusResult(TrainingStatus.Processing),
            "ready" => new TrainingStatusResult(TrainingStatus.Ready),
            "failed" => new TrainingStatusResult(TrainingStatus.Failed, result.Error),
            _ => new TrainingStatusResult(TrainingStatus.Pending)
        };
    }

    public async Task<GeneratedImageResult> GenerateImageAsync(string externalModelId, string prompt, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        var requestBody = new { model = externalModelId, prompt, width = 1024, height = 1024 };
        var response = await client.PostAsJsonAsync("generate", requestBody, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BflGenerateResponse>(ct)
            ?? throw new InvalidOperationException("BFL API returned an empty generation response.");

        return new GeneratedImageResult(result.ImageUrl);
    }

    private sealed record BflTrainingResponse(string Id);
    private sealed record BflStatusResponse(string Status, string? Error);
    private sealed record BflGenerateResponse(string ImageUrl);
}
