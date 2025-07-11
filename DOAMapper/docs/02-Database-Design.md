# Database Design & Architecture

## Database Schema Overview

The database uses a temporal design pattern to track historical changes over time. Each entity maintains version history through `ValidFrom` and `ValidTo` timestamps, allowing queries for data as it existed on any specific date.

## Core Entities

### ImportSession
Tracks each data import operation for auditing and temporal reference.

```sql
CREATE TABLE ImportSessions (
    Id UUID PRIMARY KEY,
    ImportDate TIMESTAMP NOT NULL,
    FileName VARCHAR(255) NOT NULL,
    Status VARCHAR(20) NOT NULL, -- Processing, Completed, Failed, Cancelled
    RecordsProcessed INTEGER DEFAULT 0,
    RecordsChanged INTEGER DEFAULT 0,
    CreatedAt TIMESTAMP NOT NULL,
    CompletedAt TIMESTAMP NULL,
    ErrorMessage TEXT NULL
);

CREATE INDEX IX_ImportSessions_ImportDate ON ImportSessions(ImportDate);
```

### Players (Temporal)
Stores player information with historical tracking.

```sql
CREATE TABLE Players (
    Id UUID PRIMARY KEY,
    PlayerId VARCHAR(50) NOT NULL,
    ImportSessionId UUID NOT NULL REFERENCES ImportSessions(Id),
    Name VARCHAR(100) NOT NULL,
    CityName VARCHAR(100) NOT NULL,
    Might BIGINT NOT NULL,
    AllianceId VARCHAR(50) NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    ValidFrom TIMESTAMP NOT NULL,
    ValidTo TIMESTAMP NULL
);

-- Performance indexes
CREATE INDEX IX_Players_PlayerId ON Players(PlayerId);
CREATE INDEX IX_Players_Name ON Players(Name);
CREATE INDEX IX_Players_AllianceId ON Players(AllianceId);
CREATE INDEX IX_Players_PlayerId_ValidFrom ON Players(PlayerId, ValidFrom);
CREATE INDEX IX_Players_Active_ValidFrom ON Players(IsActive, ValidFrom);

-- Full-text search index
CREATE INDEX IX_Players_Name_Search ON Players USING gin(to_tsvector('english', Name));
```

### Alliances (Temporal)
Stores alliance information with historical tracking.

```sql
CREATE TABLE Alliances (
    Id UUID PRIMARY KEY,
    AllianceId VARCHAR(50) NOT NULL,
    ImportSessionId UUID NOT NULL REFERENCES ImportSessions(Id),
    Name VARCHAR(100) NOT NULL,
    OverlordName VARCHAR(100) NOT NULL,
    Power BIGINT NOT NULL,
    FortressLevel INTEGER NOT NULL,
    FortressX INTEGER NOT NULL,
    FortressY INTEGER NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    ValidFrom TIMESTAMP NOT NULL,
    ValidTo TIMESTAMP NULL
);

-- Performance indexes
CREATE INDEX IX_Alliances_AllianceId ON Alliances(AllianceId);
CREATE INDEX IX_Alliances_Name ON Alliances(Name);
CREATE INDEX IX_Alliances_AllianceId_ValidFrom ON Alliances(AllianceId, ValidFrom);
CREATE INDEX IX_Alliances_Active_ValidFrom ON Alliances(IsActive, ValidFrom);

-- Full-text search index
CREATE INDEX IX_Alliances_Name_Search ON Alliances USING gin(to_tsvector('english', Name));
```

### Tiles (Temporal)
Stores map tile information with spatial and temporal indexing.

```sql
CREATE TABLE Tiles (
    Id UUID PRIMARY KEY,
    X INTEGER NOT NULL,
    Y INTEGER NOT NULL,
    ImportSessionId UUID NOT NULL REFERENCES ImportSessions(Id),
    Type VARCHAR(50) NOT NULL,
    Level INTEGER NOT NULL,
    PlayerId VARCHAR(50) NULL,
    AllianceId VARCHAR(50) NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    ValidFrom TIMESTAMP NOT NULL,
    ValidTo TIMESTAMP NULL
);

-- Spatial and temporal indexes
CREATE INDEX IX_Tiles_Coordinates ON Tiles(X, Y);
CREATE INDEX IX_Tiles_PlayerId ON Tiles(PlayerId);
CREATE INDEX IX_Tiles_AllianceId ON Tiles(AllianceId);
CREATE INDEX IX_Tiles_Type ON Tiles(Type);
CREATE INDEX IX_Tiles_Coordinates_ValidFrom ON Tiles(X, Y, ValidFrom);
CREATE INDEX IX_Tiles_PlayerId_ValidFrom ON Tiles(PlayerId, ValidFrom);
CREATE INDEX IX_Tiles_Active_ValidFrom ON Tiles(IsActive, ValidFrom);
```

## Entity Framework Configuration

### DbContext Setup

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    
    public DbSet<ImportSession> ImportSessions { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<Alliance> Alliances { get; set; }
    public DbSet<Tile> Tiles { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        ConfigureImportSession(modelBuilder);
        ConfigurePlayer(modelBuilder);
        ConfigureAlliance(modelBuilder);
        ConfigureTile(modelBuilder);
    }
}
```

### Entity Models

```csharp
// Base interface for temporal entities
public interface ITemporalEntity
{
    Guid Id { get; set; }
    Guid ImportSessionId { get; set; }
    bool IsActive { get; set; }
    DateTime ValidFrom { get; set; }
    DateTime? ValidTo { get; set; }
}

// Player entity
public class Player : ITemporalEntity
{
    public Guid Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public Guid ImportSessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public long Might { get; set; }
    public string? AllianceId { get; set; }
    public bool IsActive { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    
    // Navigation properties
    public ImportSession ImportSession { get; set; } = null!;
    public Alliance? Alliance { get; set; }
    public ICollection<Tile> Tiles { get; set; } = new List<Tile>();
}
```

## Temporal Query Patterns

### Current Data Queries
```sql
-- Get current active players
SELECT * FROM Players 
WHERE IsActive = TRUE;

-- Get current player by ID
SELECT * FROM Players 
WHERE PlayerId = @playerId AND IsActive = TRUE;
```

### Historical Data Queries
```sql
-- Get player data as of specific date
SELECT * FROM Players 
WHERE PlayerId = @playerId 
  AND ValidFrom <= @date 
  AND (ValidTo IS NULL OR ValidTo > @date);

-- Get player history
SELECT * FROM Players 
WHERE PlayerId = @playerId 
ORDER BY ValidFrom;
```

### Change Detection Queries
```sql
-- Find players that changed between imports
SELECT p1.PlayerId, p1.Name as OldName, p2.Name as NewName
FROM Players p1
JOIN Players p2 ON p1.PlayerId = p2.PlayerId
WHERE p1.ImportSessionId = @oldSessionId
  AND p2.ImportSessionId = @newSessionId
  AND (p1.Name != p2.Name OR p1.Might != p2.Might OR p1.CityName != p2.CityName);
```

## Performance Optimization

### Indexing Strategy
1. **Primary Keys**: UUID with clustered indexes
2. **Foreign Keys**: Non-clustered indexes on all foreign key columns
3. **Temporal Queries**: Composite indexes on entity ID + ValidFrom
4. **Spatial Queries**: Composite indexes on X,Y coordinates
5. **Search Queries**: Full-text search indexes on name fields

### Query Optimization
1. **Pagination**: Use OFFSET/LIMIT with proper ordering
2. **Filtering**: Apply filters before joins when possible
3. **Projections**: Select only required columns
4. **Caching**: Cache frequently accessed reference data

### Database Maintenance
1. **Statistics**: Regular statistics updates for query optimization
2. **Fragmentation**: Monitor and rebuild fragmented indexes
3. **Archival**: Archive old historical data based on retention policies
4. **Partitioning**: Consider table partitioning for very large datasets

## Data Integrity

### Constraints
```sql
-- Ensure valid coordinate ranges
ALTER TABLE Tiles ADD CONSTRAINT CK_Tiles_ValidCoordinates 
CHECK (X >= 0 AND X <= 749 AND Y >= 0 AND Y <= 749);

-- Ensure valid temporal ranges
ALTER TABLE Players ADD CONSTRAINT CK_Players_ValidTemporal 
CHECK (ValidTo IS NULL OR ValidTo > ValidFrom);
```

### Validation Rules
1. **Player IDs**: Must be non-empty strings
2. **Coordinates**: Must be within 0-749 range
3. **Temporal Data**: ValidTo must be greater than ValidFrom
4. **Alliance References**: AllianceId must exist in Alliances table
5. **Import Sessions**: Must reference valid ImportSession
