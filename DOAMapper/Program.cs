using DOAMapper.Services;
using DOAMapper.Components;
using DOAMapper.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add API controllers
builder.Services.AddControllers();

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
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));
}

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Register services
builder.Services.AddScoped<DOAMapper.Services.Interfaces.IChangeDetectionService, DOAMapper.Services.ChangeDetectionService>();
builder.Services.AddScoped<DOAMapper.Services.Interfaces.ITemporalDataService, DOAMapper.Services.TemporalDataService>();
builder.Services.AddScoped<DOAMapper.Services.Interfaces.IImportService, DOAMapper.Services.ImportService>();
builder.Services.AddScoped<DOAMapper.Services.Interfaces.IPlayerService, DOAMapper.Services.PlayerService>();
builder.Services.AddScoped<DOAMapper.Services.Interfaces.IAllianceService, DOAMapper.Services.AllianceService>();
builder.Services.AddScoped<DOAMapper.Services.Interfaces.IMapService, DOAMapper.Services.MapService>();
builder.Services.AddSingleton<DOAMapper.Shared.Services.IAuthenticationService, DOAMapper.Services.AuthenticationService>();
builder.Services.AddSingleton<DOAMapper.Shared.Services.IAuthenticationStateService, DOAMapper.Services.AuthenticationStateService>();
builder.Services.AddSingleton<DOAMapper.Services.ErrorHandlingService>();

// Server should only register server services
// Client services are registered in the client project's Program.cs

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(DOAMapper.Client._Imports).Assembly);

// Map API controllers
app.MapControllers();

app.Run();
