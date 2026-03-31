namespace Furpict.Services.Storage;

public interface IImageStorageProvider
{
    Task<string> UploadTrainingZipAsync(Guid modelId, Stream zipStream, CancellationToken ct = default);
    Task<string> UploadGeneratedImageAsync(Guid imageId, Stream imageStream, string contentType = "image/jpeg", CancellationToken ct = default);
}
