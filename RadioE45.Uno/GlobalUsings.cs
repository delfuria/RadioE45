global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;

// Uno.Sdk imports Uno.Extensions.Toolkit implicitly, which has its own IThemeService.
global using IThemeService = RadioE45.Services.Platform.IThemeService;
