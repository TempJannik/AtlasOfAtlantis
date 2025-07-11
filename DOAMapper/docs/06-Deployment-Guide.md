# Deployment Guide

## Development Environment Setup

### Local Development Configuration

**appsettings.Development.json**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=doamapper_dev.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Information",
      "DOAMapper": "Debug"
    }
  },
  "ImportSettings": {
    "MaxFileSizeMB": 100,
    "AllowedFileTypes": [".json"],
    "ImportDirectory": "Data/Imports/Dev"
  }
}
```

### Docker Configuration for Development

**Dockerfile**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DOAMapper/DOAMapper.csproj", "DOAMapper/"]
COPY ["DOAMapper.Client/DOAMapper.Client.csproj", "DOAMapper.Client/"]
RUN dotnet restore "./DOAMapper/DOAMapper.csproj"
COPY . .
WORKDIR "/src/DOAMapper"
RUN dotnet build "./DOAMapper.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DOAMapper.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DOAMapper.dll"]
```

**docker-compose.yml**
```yaml
version: '3.8'

services:
  doamapper:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__PostgreSQL=Host=postgres;Database=doamapper;Username=postgres;Password=password123
    depends_on:
      - postgres
    volumes:
      - ./Data/Imports:/app/Data/Imports

  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: doamapper
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: password123
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./Scripts/init.sql:/docker-entrypoint-initdb.d/init.sql

volumes:
  postgres_data:
```

## Production Deployment Strategy

### Cloud Platform Options

#### 1. Azure App Service + Azure Database for PostgreSQL
- **Pros**: Managed services, easy scaling, integrated monitoring
- **Cons**: Higher cost, vendor lock-in
- **Best for**: Enterprise deployments with budget

#### 2. DigitalOcean Droplet + Managed PostgreSQL
- **Pros**: Cost-effective, good performance, simple setup
- **Cons**: More manual configuration required
- **Best for**: Small to medium deployments

#### 3. Self-hosted VPS + Docker
- **Pros**: Maximum control, lowest cost
- **Cons**: Requires more maintenance, manual backups
- **Best for**: Personal projects, learning

### Recommended: DigitalOcean Deployment

**docker-compose.prod.yml**
```yaml
version: '3.8'

services:
  doamapper:
    image: doamapper:latest
    restart: unless-stopped
    ports:
      - "80:8080"
      - "443:8081"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080;https://+:8081
      - ASPNETCORE_Kestrel__Certificates__Default__Password=your_cert_password
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/app/certificates/cert.pfx
      - ConnectionStrings__PostgreSQL=${DATABASE_URL}
    volumes:
      - ./Data/Imports:/app/Data/Imports
      - ./certificates:/app/certificates:ro
      - ./logs:/app/logs
    labels:
      - "com.centurylinklabs.watchtower.enable=true"

  watchtower:
    image: containrrr/watchtower
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    command: --interval 300 --label-enable
    restart: unless-stopped
```

**Production Configuration**
```json
// appsettings.Production.json
{
  "ConnectionStrings": {
    "PostgreSQL": "${DATABASE_URL}"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "DOAMapper": "Information"
    }
  },
  "ImportSettings": {
    "MaxFileSizeMB": 100,
    "AllowedFileTypes": [".json"],
    "ImportDirectory": "/app/Data/Imports"
  },
  "AllowedHosts": ["yourdomain.com", "www.yourdomain.com"]
}
```

## Monitoring and Logging Setup

### Serilog Configuration for Production

**Enhanced Logging in Program.cs**
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "DOAMapper")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "/app/logs/doamapper-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();
```

### Health Checks Implementation

```csharp
// Program.cs - Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddCheck("import_directory", () =>
    {
        var importDir = builder.Configuration["ImportSettings:ImportDirectory"];
        return Directory.Exists(importDir) 
            ? HealthCheckResult.Healthy("Import directory accessible")
            : HealthCheckResult.Unhealthy("Import directory not accessible");
    });

// Configure health check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

## Backup Strategies for Historical Data

### Database Backup Script

**backup-database.sh**
```bash
#!/bin/bash

DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/backups/database"
DB_NAME="doamapper"
DB_USER="postgres"
DB_HOST="localhost"

# Create backup directory if it doesn't exist
mkdir -p $BACKUP_DIR

# Create database backup
pg_dump -h $DB_HOST -U $DB_USER -d $DB_NAME -f "$BACKUP_DIR/doamapper_backup_$DATE.sql"

# Compress the backup
gzip "$BACKUP_DIR/doamapper_backup_$DATE.sql"

# Remove backups older than 30 days
find $BACKUP_DIR -name "doamapper_backup_*.sql.gz" -mtime +30 -delete

echo "Database backup completed: doamapper_backup_$DATE.sql.gz"
```

### Automated Backup with Cron

```bash
# Add to crontab (crontab -e)
# Daily backup at 2 AM
0 2 * * * /path/to/backup-database.sh

# Weekly full backup to cloud storage
0 3 * * 0 /path/to/backup-to-cloud.sh
```

### Cloud Storage Backup Script

**backup-to-cloud.sh**
```bash
#!/bin/bash

DATE=$(date +%Y%m%d)
BACKUP_DIR="/backups"
BUCKET_NAME="doamapper-backups"

# Upload to cloud storage (using s3cmd for S3-compatible storage)
s3cmd sync $BACKUP_DIR/ s3://$BUCKET_NAME/database-backups/

# Upload import files
s3cmd sync /app/Data/Imports/ s3://$BUCKET_NAME/import-files/

echo "Cloud backup completed for $DATE"
```

## Scalability Considerations

### Database Optimization

**Additional Indexes for Performance**
```sql
-- Full-text search indexes
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_players_search 
ON players USING gin(to_tsvector('english', name));

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_alliances_search 
ON alliances USING gin(to_tsvector('english', name));

-- Partitioning for large historical data (future consideration)
CREATE TABLE tiles_y2024 PARTITION OF tiles 
FOR VALUES FROM ('2024-01-01') TO ('2025-01-01');
```

### Application-Level Caching

```csharp
// Program.cs - Add caching
builder.Services.AddMemoryCache();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Services/CachedPlayerService.cs
public class CachedPlayerService : IPlayerService
{
    private readonly IPlayerService _playerService;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(15);

    public async Task<PagedResult<PlayerDto>> SearchPlayersAsync(string query, DateTime date, int page, int size)
    {
        var cacheKey = $"players_search_{query}_{date:yyyyMMdd}_{page}_{size}";
        
        if (_cache.TryGetValue(cacheKey, out PagedResult<PlayerDto> cachedResult))
        {
            return cachedResult;
        }

        var result = await _playerService.SearchPlayersAsync(query, date, page, size);
        _cache.Set(cacheKey, result, _cacheExpiry);
        
        return result;
    }
}
```

## Security Considerations

### Application Security

```csharp
// Program.cs - Security enhancements
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });
}

// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});
```

### File Upload Security

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

## Deployment Checklist

### Pre-Deployment
- [ ] Database migrations tested
- [ ] Environment variables configured
- [ ] SSL certificates installed
- [ ] Backup strategy implemented
- [ ] Monitoring configured
- [ ] Security headers configured
- [ ] File upload limits set
- [ ] Health checks implemented

### Post-Deployment
- [ ] Health check endpoint responding
- [ ] Database connectivity verified
- [ ] File upload functionality tested
- [ ] Search functionality tested
- [ ] Performance monitoring active
- [ ] Backup scripts scheduled
- [ ] Log rotation configured
- [ ] SSL certificate auto-renewal configured
