using Microsoft.EntityFrameworkCore;
using StockNewsNotifier.Data.Entities;

namespace StockNewsNotifier.Data;

/// <summary>
/// Database context for StockNewsNotifier application
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<WatchItem> WatchItems => Set<WatchItem>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<WatchItemSource> WatchItemSources => Set<WatchItemSource>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<CrawlState> CrawlStates => Set<CrawlState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // WatchItem configuration
        modelBuilder.Entity<WatchItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Exchange)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Ticker)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.CompanyName)
                .HasMaxLength(200);

            entity.Property(e => e.IconUrl)
                .HasMaxLength(500);

            // Unique index on Exchange + Ticker
            entity.HasIndex(e => new { e.Exchange, e.Ticker })
                .IsUnique()
                .HasDatabaseName("IX_WatchItem_Exchange_Ticker");

            entity.HasIndex(e => e.CreatedUtc);
        });

        // Source configuration
        modelBuilder.Entity<Source>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.BaseUrl)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.DisplayName)
                .HasMaxLength(100);

            entity.HasIndex(e => e.Name)
                .IsUnique();
        });

        // WatchItemSource configuration (Many-to-Many junction table)
        modelBuilder.Entity<WatchItemSource>(entity =>
        {
            entity.HasKey(e => new { e.WatchItemId, e.SourceId });

            entity.Property(e => e.CustomQuery)
                .HasMaxLength(1000);

            entity.HasOne(e => e.WatchItem)
                .WithMany(w => w.WatchItemSources)
                .HasForeignKey(e => e.WatchItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Source)
                .WithMany(s => s.WatchItemSources)
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // NewsItem configuration
        modelBuilder.Entity<NewsItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Url)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.CanonicalUrl)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.Summary)
                .HasMaxLength(2000);

            entity.Property(e => e.TitleHash)
                .IsRequired()
                .HasMaxLength(64);

            // Unique index on CanonicalUrl for deduplication
            entity.HasIndex(e => e.CanonicalUrl)
                .IsUnique()
                .HasDatabaseName("IX_NewsItem_CanonicalUrl");

            // Composite index for efficient querying by WatchItem and time
            entity.HasIndex(e => new { e.WatchItemId, e.FetchedUtc })
                .HasDatabaseName("IX_NewsItem_WatchItemId_FetchedUtc");

            // Index for finding unread items
            entity.HasIndex(e => new { e.WatchItemId, e.IsRead, e.FetchedUtc });

            // Index on TitleHash for duplicate detection
            entity.HasIndex(e => e.TitleHash);

            entity.HasOne(e => e.WatchItem)
                .WithMany(w => w.NewsItems)
                .HasForeignKey(e => e.WatchItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Source)
                .WithMany(s => s.NewsItems)
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // CrawlState configuration
        modelBuilder.Entity<CrawlState>(entity =>
        {
            entity.HasKey(e => e.SourceId);

            entity.Property(e => e.RobotsTxt)
                .HasMaxLength(10000);

            entity.Property(e => e.LastError)
                .HasMaxLength(2000);

            entity.HasOne(e => e.Source)
                .WithOne(s => s.CrawlState)
                .HasForeignKey<CrawlState>(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.LastCrawlUtc);
        });
    }
}
