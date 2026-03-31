using Furpict.Data;
using Furpict.Data.Entities;
using Furpict.Services.Training;
using Microsoft.EntityFrameworkCore;

namespace Furpict.Services;

internal sealed class ModelTrainingStatusPoller(
    IServiceScopeFactory scopeFactory,
    ILogger<ModelTrainingStatusPoller> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Model training status poller started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollTrainingStatusAsync(stoppingToken);
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task PollTrainingStatusAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var trainingProvider = scope.ServiceProvider.GetRequiredService<IImageModelTrainingProvider>();

        var trainingModels = await db.PetModels
            .Where(m => m.Status == ModelStatus.Training && m.ExternalModelId != null)
            .ToListAsync(ct);

        if (trainingModels.Count == 0)
            return;

        logger.LogInformation("Polling status for {Count} model(s) in training.", trainingModels.Count);

        foreach (var model in trainingModels)
        {
            try
            {
                var result = await trainingProvider.GetTrainingStatusAsync(model.ExternalModelId!, ct);

                switch (result.Status)
                {
                    case TrainingStatus.Ready:
                        model.Status = ModelStatus.Ready;
                        model.TrainingCompletedAt = DateTimeOffset.UtcNow;
                        model.UpdatedAt = DateTimeOffset.UtcNow;
                        logger.LogInformation("Model {ModelId} training complete.", model.Id);
                        break;

                    case TrainingStatus.Failed:
                        model.Status = ModelStatus.Failed;
                        model.FailureReason = result.FailureReason;
                        model.UpdatedAt = DateTimeOffset.UtcNow;
                        logger.LogWarning("Model {ModelId} training failed: {Reason}", model.Id, result.FailureReason);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error polling status for model {ModelId}.", model.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
