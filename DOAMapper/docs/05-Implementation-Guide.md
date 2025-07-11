# Implementation Guide

## Development Phases

### Phase 1: Foundation & Data Layer (Week 1-2)

#### 1.1. Project Setup & Dependencies

**Install Required NuGet Packages:**
```bash
# Entity Framework Core with PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

# For development - SQLite
dotnet add package Microsoft.EntityFrameworkCore.Sqlite

# Additional packages
dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection
dotnet add package FluentValidation.AspNetCore
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```

**Update appsettings.json:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=doamapper.db",
    "PostgreSQL": "Host=localhost;Database=doamapper;Username=postgres;Password=password"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ImportSettings": {
    "MaxFileSizeMB": 100,
    "AllowedFileTypes": [".json"],
    "ImportDirectory": "Data/Imports"
  }
}
```

#### 1.2. Database Schema Implementation

**Create Entity Models:**
```csharp
// Models/Entities/ImportSession.cs
public class ImportSession
{
    public Guid Id { get; set; }
    public DateTime ImportDate { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ImportStatus Status { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsChanged { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

// Models/Entities/Player.cs
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

// Models/Interfaces/ITemporalEntity.cs
public interface ITemporalEntity
{
    Guid Id { get; set; }
    Guid ImportSessionId { get; set; }
    bool IsActive { get; set; }
    DateTime ValidFrom { get; set; }
    DateTime? ValidTo { get; set; }
}

// Models/Enums/ImportStatus.cs
public enum ImportStatus
{
    Processing,
    Completed,
    Failed,
    Cancelled
}
```

#### 1.3. Entity Framework Configuration

**Create DbContext:**
```csharp
// Data/ApplicationDbContext.cs
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
        
        // ImportSession configuration
        modelBuilder.Entity<ImportSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.ImportDate);
        });
        
        // Player configuration
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CityName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AllianceId).HasMaxLength(50);
            
            // Indexes for performance
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.AllianceId);
            entity.HasIndex(e => new { e.PlayerId, e.ValidFrom });
            entity.HasIndex(e => new { e.IsActive, e.ValidFrom });
            
            // Relationships
            entity.HasOne(e => e.ImportSession)
                  .WithMany()
                  .HasForeignKey(e => e.ImportSessionId);
                  
            entity.HasOne(e => e.Alliance)
                  .WithMany(a => a.Members)
                  .HasForeignKey(e => e.AllianceId)
                  .HasPrincipalKey(a => a.AllianceId);
        });
    }
}
```

**Configure Services in Program.cs:**
```csharp
// Program.cs updates
using DOAMapper.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Database configuration
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));
}

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Validation
builder.Services.AddFluentValidationAutoValidation();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
}

app.Run();
```

### Phase 2: Core Backend Services (Week 2-3)

#### 2.1. Import Service Implementation

**Create Import Service Interface:**
```csharp
// Services/Interfaces/IImportService.cs
public interface IImportService
{
    Task<ImportSession> StartImportAsync(Stream jsonStream, string fileName);
    Task<ImportSessionDto> GetImportStatusAsync(Guid sessionId);
    Task<List<ImportSessionDto>> GetImportHistoryAsync();
    Task<List<DateTime>> GetAvailableImportDatesAsync();
}
```

#### 2.2. Change Detection Algorithm

**Create Change Detection Service:**
```csharp
// Services/Interfaces/IChangeDetectionService.cs
public interface IChangeDetectionService
{
    Task<ChangeSet<Tile>> DetectTileChangesAsync(List<Tile> incoming, List<Tile> current);
    Task<ChangeSet<Player>> DetectPlayerChangesAsync(List<Player> incoming, List<Player> current);
    Task<ChangeSet<Alliance>> DetectAllianceChangesAsync(List<Alliance> incoming, List<Alliance> current);
}

// Models/ChangeSet.cs
public class ChangeSet<T>
{
    public List<T> Added { get; set; } = new();
    public List<T> Modified { get; set; } = new();
    public List<T> Removed { get; set; } = new();
    public List<(T Old, T New)> Changes { get; set; } = new();
}
```

### Phase 3: API Layer Development (Week 3-4)

#### 3.1. API Controllers Implementation

**Create Player Controller:**
```csharp
// Controllers/PlayersController.cs
[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly IPlayerService _playerService;
    private readonly IMapper _mapper;

    public PlayersController(IPlayerService playerService, IMapper mapper)
    {
        _playerService = playerService;
        _mapper = mapper;
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<PlayerDto>>> SearchPlayers(
        [FromQuery] string query,
        [FromQuery] DateTime date,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var result = await _playerService.SearchPlayersAsync(query, date, page, size);
        return Ok(result);
    }

    [HttpGet("{playerId}")]
    public async Task<ActionResult<PlayerDetailDto>> GetPlayer(
        string playerId,
        [FromQuery] DateTime date)
    {
        var player = await _playerService.GetPlayerAsync(playerId, date);
        if (player == null)
            return NotFound();
            
        return Ok(player);
    }

    [HttpGet("{playerId}/tiles")]
    public async Task<ActionResult<List<TileDto>>> GetPlayerTiles(
        string playerId,
        [FromQuery] DateTime date)
    {
        var tiles = await _playerService.GetPlayerTilesAsync(playerId, date);
        return Ok(tiles);
    }

    [HttpGet("{playerId}/history")]
    public async Task<ActionResult<List<PlayerHistoryDto>>> GetPlayerHistory(string playerId)
    {
        var history = await _playerService.GetPlayerHistoryAsync(playerId);
        return Ok(history);
    }
}
```

### Phase 4: Frontend Core Components (Week 4-5)

#### 4.1. Shared Components Implementation

**Update Navigation Menu:**
```razor
<!-- Components/Layout/NavMenu.razor -->
<div class="nav-scrollable" onclick="document.querySelector('.navbar-toggler').click()">
    <nav class="nav flex-column">
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="" Match="NavLinkMatch.All">
                <span class="bi bi-house-door-fill-nav-menu" aria-hidden="true"></span> Home
            </NavLink>
        </div>

        <div class="nav-item px-3">
            <NavLink class="nav-link" href="players">
                <span class="bi bi-people-fill-nav-menu" aria-hidden="true"></span> Players
            </NavLink>
        </div>

        <div class="nav-item px-3">
            <NavLink class="nav-link" href="alliances">
                <span class="bi bi-shield-fill-nav-menu" aria-hidden="true"></span> Alliances
            </NavLink>
        </div>

        <div class="nav-item px-3">
            <NavLink class="nav-link" href="import">
                <span class="bi bi-upload-nav-menu" aria-hidden="true"></span> Import Data
            </NavLink>
        </div>
    </nav>
</div>
```

### Phase 5: Data Import & Historical Features (Week 5-6)

#### 5.1. Data Import UI Implementation

**File Upload Validation:**
```csharp
// Services/FileValidationService.cs
public class FileValidationService
{
    private readonly string[] _allowedExtensions = { ".json" };
    private readonly long _maxFileSize = 100 * 1024 * 1024; // 100MB

    public bool ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return false;

        if (file.Length > _maxFileSize)
            return false;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension))
            return false;

        // Validate JSON structure
        try
        {
            using var stream = file.OpenReadStream();
            using var document = JsonDocument.Parse(stream);
            
            // Basic structure validation
            var root = document.RootElement;
            return root.TryGetProperty("tiles", out _) &&
                   root.TryGetProperty("players", out _) &&
                   root.TryGetProperty("allianceBases", out _);
        }
        catch
        {
            return false;
        }
    }
}
```

### Phase 6: Testing & Polish (Week 6-7)

#### 6.1. Unit Testing Implementation

**Create Test Project:**
```bash
dotnet new xunit -n DOAMapper.Tests
dotnet add DOAMapper.Tests reference DOAMapper
dotnet add DOAMapper.Tests package Microsoft.EntityFrameworkCore.InMemory
dotnet add DOAMapper.Tests package Moq
```

**Example Unit Test:**
```csharp
// Tests/Services/PlayerServiceTests.cs
public class PlayerServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly PlayerService _playerService;

    public PlayerServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _context = new ApplicationDbContext(options);
        
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        
        _playerService = new PlayerService(_context, _mapper);
    }

    [Fact]
    public async Task SearchPlayersAsync_ShouldReturnMatchingPlayers()
    {
        // Arrange
        var testDate = DateTime.UtcNow;
        var player = new Player
        {
            PlayerId = "123",
            Name = "TestPlayer",
            CityName = "TestCity",
            Might = 1000,
            IsActive = true,
            ValidFrom = testDate
        };
        
        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        // Act
        var result = await _playerService.SearchPlayersAsync("Test", testDate, 1, 10);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TestPlayer", result.Items[0].Name);
    }
}
```

## Development Best Practices

### Code Organization
1. **Separation of Concerns**: Keep controllers thin, business logic in services
2. **Dependency Injection**: Use DI for all service dependencies
3. **Error Handling**: Implement global exception handling
4. **Logging**: Use structured logging throughout the application
5. **Validation**: Validate all inputs at API boundaries

### Performance Considerations
1. **Async/Await**: Use async patterns for all I/O operations
2. **Pagination**: Implement pagination for all list endpoints
3. **Caching**: Cache frequently accessed data
4. **Database Queries**: Optimize queries and use proper indexing
5. **Memory Management**: Dispose of resources properly

### Security Best Practices
1. **Input Validation**: Validate and sanitize all user inputs
2. **SQL Injection**: Use parameterized queries (EF Core handles this)
3. **File Upload**: Validate file types and sizes
4. **Error Messages**: Don't expose sensitive information in error messages
5. **HTTPS**: Use HTTPS in production
