namespace Furpict.Services.Training;

public sealed record TrainingOptions(string ModelName, string TriggerWord);

public enum TrainingStatus
{
    Pending,
    Processing,
    Ready,
    Failed
}

public sealed record TrainingStatusResult(TrainingStatus Status, string? FailureReason = null);

public sealed record GeneratedImageResult(string ImageUrl);

public interface IImageModelTrainingProvider
{
    Task<string> StartTrainingAsync(Stream trainingZip, TrainingOptions options, CancellationToken ct = default);
    Task<TrainingStatusResult> GetTrainingStatusAsync(string externalModelId, CancellationToken ct = default);
    Task<GeneratedImageResult> GenerateImageAsync(string externalModelId, string prompt, CancellationToken ct = default);
}
