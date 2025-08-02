using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DOAMapper.Client.Services;
using DOAMapper.Shared.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Configure HttpClient
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Add MudBlazor services
builder.Services.AddMudServices();

// Register client services
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IAllianceService, AllianceService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IRealmService, RealmService>();
builder.Services.AddScoped<IAuthenticationHttpService, AuthenticationHttpService>();
builder.Services.AddSingleton<DOAMapper.Shared.Services.IAuthenticationService, DOAMapper.Client.Services.AuthenticationService>();
builder.Services.AddSingleton<DOAMapper.Shared.Services.IAuthenticationStateService, DOAMapper.Client.Services.AuthenticationStateService>();
builder.Services.AddSingleton<DateStateService>();
builder.Services.AddSingleton<RealmStateService>();
builder.Services.AddSingleton<ErrorHandlingService>();

var app = builder.Build();

// Initialize authentication service to restore state from localStorage
var authService = app.Services.GetRequiredService<DOAMapper.Shared.Services.IAuthenticationService>();
await authService.EnsureInitializedAsync();

await app.RunAsync();
