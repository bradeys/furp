using Furpict.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Furpict.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<PetModel> PetModels => Set<PetModel>();
    public DbSet<GeneratedImage> GeneratedImages => Set<GeneratedImage>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Pet>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(100);
            e.Property(p => p.Species).IsRequired().HasMaxLength(50);
            e.Property(p => p.Breed).HasMaxLength(100);
            e.HasOne(p => p.User)
                .WithMany(u => u.Pets)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PetModel>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Status).HasConversion<int>();
            e.Property(m => m.ExternalModelId).HasMaxLength(255);
            e.Property(m => m.StripePaymentIntentId).HasMaxLength(255);
            e.Property(m => m.StripeCheckoutSessionId).HasMaxLength(255);
            e.HasOne(m => m.Pet)
                .WithMany(p => p.Models)
                .HasForeignKey(m => m.PetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => m.StripeCheckoutSessionId);
        });

        builder.Entity<GeneratedImage>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Prompt).IsRequired().HasMaxLength(1000);
            e.Property(i => i.ImageBlobUrl).IsRequired().HasMaxLength(2048);
            e.Property(i => i.ThumbnailBlobUrl).HasMaxLength(2048);
            e.HasOne(i => i.PetModel)
                .WithMany(m => m.GeneratedImages)
                .HasForeignKey(i => i.PetModelId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(i => i.IsFeatured);
            e.HasIndex(i => i.IsPublic);
        });

        builder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Status).HasConversion<int>();
            e.Property(o => o.Currency).IsRequired().HasMaxLength(10);
            e.Property(o => o.StripeCheckoutSessionId).IsRequired().HasMaxLength(255);
            e.Property(o => o.StripePaymentIntentId).HasMaxLength(255);
            e.HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.PetModel)
                .WithOne(m => m.Order)
                .HasForeignKey<Order>(o => o.PetModelId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(o => o.StripeCheckoutSessionId).IsUnique();
        });
    }
}
