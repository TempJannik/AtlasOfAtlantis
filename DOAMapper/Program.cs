using DOAMapper.Services;
using DOAMapper.Data;
using DOAMapper.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Railway PORT environment variable for dynamic port assignment
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// Register encoding providers for legacy encodings (Windows-1252, ISO-8859-1, etc.)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Configure logging to completely suppress EF Core database commands
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);

// Add services to the container.
// API-only: no Razor components or interactive rendering

// Add API controllers
builder.Services.AddControllers();

// Output caching: long TTL policy with broad vary and global tag for invalidation
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("LongLivedData", b => b
        .Expire(TimeSpan.FromHours(12))
        .SetVaryByQuery("*")
        .Tag("data"));
});

// OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// CORS: restrict to production site only
builder.Services.AddCors(options =>
{
    options.AddPolicy("AtlasOnly", policy =>
        policy
            .WithOrigins("https://atlasofatlantis.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});


// Configure request size limits for large file uploads (100MB)
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100MB
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Database configuration
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"), sqliteOptions =>
        {
            sqliteOptions.CommandTimeout(300); // 5 minute timeout for large operations
        })
        .EnableSensitiveDataLogging(false)
        .EnableServiceProviderCaching()
        .EnableDetailedErrors(false)
        .LogTo(_ => { }, LogLevel.None) // Completely disable all EF Core logging
        .ConfigureWarnings(warnings =>
        {
            warnings.Ignore(RelationalEventId.MultipleCollectionIncludeWarning);
            warnings.Ignore(RelationalEventId.CommandExecuted);
            warnings.Ignore(CoreEventId.ContextInitialized);
        }));
}
else
{
    // Production: Use Railway's DATABASE_URL environment variable or fallback to PostgreSQL connection string
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    var configConnectionString = builder.Configuration.GetConnectionString("PostgreSQL");

    // Debug logging to see what we're getting
    Console.WriteLine($"DATABASE_URL environment variable: '{databaseUrl}'");
    Console.WriteLine($"PostgreSQL config connection string: '{configConnectionString}'");

    string connectionString;

    // Convert DATABASE_URL (postgresql://...) to Npgsql connection string format
    if (!string.IsNullOrEmpty(databaseUrl) && databaseUrl.StartsWith("postgresql://"))
    {
        try
        {
            var uri = new Uri(databaseUrl);
            connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Require;Trust Server Certificate=true;Client Encoding=UTF8";
            Console.WriteLine($"Converted DATABASE_URL to connection string: '{connectionString}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse DATABASE_URL: {ex.Message}");
            throw new InvalidOperationException($"Failed to parse DATABASE_URL: {ex.Message}");
        }
    }
    else
    {
        connectionString = databaseUrl ?? configConnectionString;
    }

    // Validate connection string
    if (string.IsNullOrEmpty(connectionString) || connectionString == "${DATABASE_URL}")
    {
        throw new InvalidOperationException(
            $"DATABASE_URL environment variable is not set or PostgreSQL connection string is missing. " +
            $"DATABASE_URL='{databaseUrl}', PostgreSQL config='{configConnectionString}'. " +
            "Please ensure you have added a PostgreSQL database service in Railway and the DATABASE_URL environment variable is properly configured.");
    }

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.CommandTimeout(300); // 5 minute timeout for large operations
        })
        .EnableSensitiveDataLogging(false)
        .EnableServiceProviderCaching()
        .EnableDetailedErrors(false));
}

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Register services
builder.Services.AddScoped<DOAMapper.Services.Interfaces.IChangeDetectionService, DOAMapper.Services.ChangeDetectionService>();
builder.Services.AddScoped<DOAMapper.Services.Interfaces.ITemporalDataService, DOAMapper.Services.TemporalDataService>();
builder.Services.AddScoped<DOAMapper.Services.Interfaces.IImportService, DOAMapper.Services.ImportService>();
builder.Services.AddScoped<DOAMapper.Services.Interfaces.IPlayerService, DOAMapper.Services.PlayerService>();
builder.Services.AddScoped<DOAMapper.Services.Interfaces.IAllianceService, DOAMapper.Services.AllianceService>();
builder.Services.AddScoped<DOAMapper.Shared.Services.IRealmService, DOAMapper.Services.RealmService>();
builder.Services.AddScoped<DOAMapper.Services.Interfaces.IMapService, DOAMapper.Services.MapService>();
builder.Services.AddSingleton<DOAMapper.Shared.Services.IAuthenticationService, DOAMapper.Services.AuthenticationService>();
builder.Services.AddSingleton<DOAMapper.Shared.Services.IAuthenticationStateService, DOAMapper.Services.AuthenticationStateService>();
builder.Services.AddSingleton<DOAMapper.Services.ErrorHandlingService>();

// Register new background import services
builder.Services.AddScoped<DOAMapper.Services.BackgroundImportService>();
builder.Services.AddScoped<DOAMapper.Services.ImportStatusService>();

// Server should only register server services
// Client services are registered in the client project's Program.cs

var app = builder.Build();

// Ensure database is created and import directory exists
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Initializing database...");
        context.Database.EnsureCreated();
        logger.LogInformation("Database initialization completed successfully");



        // Migrate existing ImportSessions to default realm
        await MigrateExistingImportSessionsAsync(context, logger);

        // Ensure import directory exists
        var importDirectory = app.Configuration["ImportSettings:ImportDirectory"] ?? "/app/Data/Imports";
        if (!Directory.Exists(importDirectory))
        {
            Directory.CreateDirectory(importDirectory);
            logger.LogInformation("Created import directory: {ImportDirectory}", importDirectory);
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to initialize database or create import directory");
        throw; // Re-throw to prevent application startup with invalid database
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Generic API exception handler producing ProblemDetails
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            var problem = Results.Problem(
                title: "An unexpected error occurred",
                statusCode: StatusCodes.Status500InternalServerError);
            await problem.ExecuteAsync(context);
        });
    });
    app.UseHsts();

    // Swagger disabled in production
}

// API-only: no HTTPS redirection here (handled by hosting)

// Enable Output Cache globally
app.UseOutputCache();

// Enable CORS globally (both environments)
app.UseCors("AtlasOnly");

// Map API controllers
app.MapControllers();

app.Run();



static async Task MigrateExistingImportSessionsAsync(ApplicationDbContext context, Microsoft.Extensions.Logging.ILogger logger)
{
    try
    {
        // Find ImportSessions that don't have a RealmId set (RealmId is Guid.Empty)
        var importSessionsToMigrate = await context.ImportSessions
            .Where(s => s.RealmId == Guid.Empty)
            .ToListAsync();

        if (importSessionsToMigrate.Any())
        {
            logger.LogInformation("Found {Count} ImportSessions with invalid RealmId (Guid.Empty)", importSessionsToMigrate.Count);

            // Try to get any existing realm to migrate to
            var anyRealm = await context.Realms
                .Where(r => r.IsActive)
                .FirstOrDefaultAsync();

            if (anyRealm != null)
            {
                logger.LogInformation("Migrating {Count} existing ImportSessions to realm '{RealmId}'", importSessionsToMigrate.Count, anyRealm.RealmId);

                foreach (var session in importSessionsToMigrate)
                {
                    session.RealmId = anyRealm.Id;
                }

                await context.SaveChangesAsync();
                logger.LogInformation("Successfully migrated {Count} ImportSessions to realm '{RealmId}'", importSessionsToMigrate.Count, anyRealm.RealmId);
            }
            else
            {
                logger.LogWarning("No active realms found. Deleting {Count} orphaned ImportSessions", importSessionsToMigrate.Count);

                // Delete orphaned ImportSessions since there are no realms to assign them to
                context.ImportSessions.RemoveRange(importSessionsToMigrate);
                await context.SaveChangesAsync();

                logger.LogInformation("Deleted {Count} orphaned ImportSessions", importSessionsToMigrate.Count);
            }
        }
        else
        {
            logger.LogInformation("No ImportSessions need migration - all are already assigned to realms");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to migrate existing ImportSessions");
        throw;
    }
}
