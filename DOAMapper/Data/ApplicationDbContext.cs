using DOAMapper.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DOAMapper.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
        // Optimize for batch operations - disable expensive tracking and logging
        ChangeTracker.AutoDetectChangesEnabled = false;
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.LazyLoadingEnabled = false;

        // Disable all database logging for performance during bulk operations
        Database.SetCommandTimeout(300); // 5 minute timeout
    }

    public DbSet<ImportSession> ImportSessions { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<Alliance> Alliances { get; set; }
    public DbSet<Tile> Tiles { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Enable change detection only when saving
        ChangeTracker.AutoDetectChangesEnabled = true;
        ChangeTracker.DetectChanges();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            // Disable change detection again for performance
            ChangeTracker.AutoDetectChangesEnabled = false;
        }
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Disable foreign key constraints for temporal entities to support multiple records with same IDs
        modelBuilder.Model.SetDefaultSchema(null);
        
        ConfigureImportSession(modelBuilder);
        ConfigurePlayer(modelBuilder);
        ConfigureAlliance(modelBuilder);
        ConfigureTile(modelBuilder);
    }
    
    private static void ConfigureImportSession(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.CurrentPhase).HasMaxLength(100);
            entity.Property(e => e.StatusMessage).HasMaxLength(500);
            entity.Property(e => e.PhaseDetailsJson).HasColumnType("TEXT");

            // Indexes for performance
            entity.HasIndex(e => e.ImportDate);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
    
    private static void ConfigurePlayer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CityName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AllianceId).HasMaxLength(50);

            // Indexes for performance (non-unique to support temporal versioning)
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.AllianceId);
            entity.HasIndex(e => new { e.PlayerId, e.ValidFrom });
            entity.HasIndex(e => new { e.IsActive, e.ValidFrom });

            // Relationships
            entity.HasOne(e => e.ImportSession)
                  .WithMany()
                  .HasForeignKey(e => e.ImportSessionId);

            // Ignore navigation properties to avoid FK constraints for temporal versioning
            entity.Ignore(e => e.Alliance);
            entity.Ignore(e => e.Tiles);
        });
    }
    
    private static void ConfigureAlliance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alliance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AllianceId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.OverlordName).HasMaxLength(100).IsRequired();
            
            // Indexes
            entity.HasIndex(e => e.AllianceId);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => new { e.AllianceId, e.ValidFrom });
            entity.HasIndex(e => new { e.IsActive, e.ValidFrom });
            
            // Relationships
            entity.HasOne(e => e.ImportSession)
                  .WithMany()
                  .HasForeignKey(e => e.ImportSessionId);

            // Ignore navigation properties to avoid FK constraints for temporal versioning
            entity.Ignore(e => e.Members);
            entity.Ignore(e => e.Tiles);
        });
    }
    
    private static void ConfigureTile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PlayerId).HasMaxLength(50);
            entity.Property(e => e.AllianceId).HasMaxLength(50);
            
            // Spatial and temporal indexes
            entity.HasIndex(e => new { e.X, e.Y });
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => e.AllianceId);
            entity.HasIndex(e => new { e.X, e.Y, e.ValidFrom });
            entity.HasIndex(e => new { e.PlayerId, e.ValidFrom });
            entity.HasIndex(e => new { e.IsActive, e.ValidFrom });
            
            // Relationships
            entity.HasOne(e => e.ImportSession)
                  .WithMany()
                  .HasForeignKey(e => e.ImportSessionId);
                  
            // Ignore navigation properties to avoid FK constraints for temporal versioning
            entity.Ignore(e => e.Player);
            entity.Ignore(e => e.Alliance);
        });
    }
}
