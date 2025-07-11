using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DOAMapper.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Configure HttpClient
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register client services
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IAllianceService, AllianceService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddSingleton<DateStateService>();
builder.Services.AddSingleton<ErrorHandlingService>();

await builder.Build().RunAsync();
