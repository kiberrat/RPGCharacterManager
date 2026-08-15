using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Abstractions.Extensions;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Database;
using RPGCharacterManager.Extensions;
using RPGCharacterManager.Infrastructure;

if (args.Length == 0)
{
    Console.Error.WriteLine("Укажите путь к .rpgpack. Добавьте --inspect для проверки без установки.");
    return 2;
}

var packagePath = Path.GetFullPath(args[0]);
var inspectOnly = args.Any(value => string.Equals(value, "--inspect", StringComparison.OrdinalIgnoreCase));
var builder = Host.CreateApplicationBuilder();
builder.Configuration.AddInMemoryCollection();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddContent();
builder.Services.AddExtensions();

using var host = builder.Build();
host.Services.GetRequiredService<IAppPathService>().EnsureDirectoriesExist();
var initialized = await host.Services.GetRequiredService<IDatabaseService>().InitializeAsync();
if (initialized.IsFailure)
{
    Console.Error.WriteLine(initialized.Error);
    return 3;
}

var extensions = host.Services.GetRequiredService<IExtensionService>();
var preview = await extensions.InspectAsync(packagePath);
if (preview.IsFailure)
{
    Console.Error.WriteLine(preview.Error);
    return 4;
}

Console.WriteLine(JsonSerializer.Serialize(new
{
    preview.Value.Manifest.Name,
    preview.Value.Manifest.Version,
    preview.Value.CanInstall,
    Sections = preview.Value.Sections.Select(section => new { section.Title, section.Count }),
    preview.Value.Problems,
    preview.Value.Warnings,
}, new JsonSerializerOptions { WriteIndented = true }));

if (inspectOnly)
{
    return preview.Value.CanInstall ? 0 : 5;
}

var installed = await extensions.InstallAsync(packagePath);
if (installed.IsFailure)
{
    Console.Error.WriteLine(installed.Error);
    return 6;
}

Console.WriteLine($"Установлено: {installed.Value.Manifest.Name}; объектов: {installed.Value.ObjectCount}.");
return 0;
