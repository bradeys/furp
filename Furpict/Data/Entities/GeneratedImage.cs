namespace Furpict.Data.Entities;

public sealed class GeneratedImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PetModelId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string ImageBlobUrl { get; set; } = string.Empty;
    public string? ThumbnailBlobUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsPublic { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public PetModel PetModel { get; set; } = null!;
}
