using LanCloud.Models;
using Microsoft.EntityFrameworkCore;

namespace LanCloud.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<StorageItem> Items => Set<StorageItem>();
    public DbSet<ItemShare> Shares => Set<ItemShare>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(40).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
        });

        b.Entity<StorageItem>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.StoredName).HasMaxLength(64);

            e.HasIndex(x => new { x.OwnerId, x.ParentId });
            e.HasIndex(x => x.IsPublic);

            // حذف کاربر => حذف همه فایل‌ها و پوشه‌هایش
            e.HasOne(x => x.Owner)
                .WithMany(u => u.Items)
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            // حذف پوشه => حذف همه محتویاتش
            e.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ItemShare>(e =>
        {
            e.HasIndex(x => new { x.ItemId, x.UserId }).IsUnique();

            e.HasOne(x => x.Item)
                .WithMany(i => i.Shares)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
