using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Furpict.Services.Storage;

public sealed class AzureBlobStorageOptions
{
    public const string SectionName = "AzureBlobStorage";
    public string ConnectionString { get; set; } = string.Empty;
    public string TrainingContainer { get; set; } = "training-zips";
    public string GeneratedImagesContainer { get; set; } = "generated-images";
}

internal sealed class AzureBlobImageStorageProvider(IOptions<AzureBlobStorageOptions> options) : IImageStorageProvider
{
    private readonly AzureBlobStorageOptions _options = options.Value;

    public async Task<string> UploadTrainingZipAsync(Guid modelId, Stream zipStream, CancellationToken ct = default)
    {
        var blobName = $"{modelId}/training.zip";
        return await UploadAsync(_options.TrainingContainer, blobName, zipStream, "application/zip", ct);
    }

    public async Task<string> UploadGeneratedImageAsync(Guid imageId, Stream imageStream, string contentType = "image/jpeg", CancellationToken ct = default)
    {
        var extension = contentType == "image/png" ? "png" : "jpg";
        var blobName = $"{imageId}.{extension}";
        return await UploadAsync(_options.GeneratedImagesContainer, blobName, imageStream, contentType, ct);
    }

    private async Task<string> UploadAsync(string containerName, string blobName, Stream stream, string contentType, CancellationToken ct)
    {
        var serviceClient = new BlobServiceClient(_options.ConnectionString);
        var containerClient = serviceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);

        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);

        return blobClient.Uri.ToString();
    }
}
