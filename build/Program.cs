using Revit.AddinManager.Build.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Extensions;

Environment.SetEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en");

string mode = args.FirstOrDefault(argument => !argument.StartsWith('-'))?.ToLowerInvariant() ?? "compile";

var builder = Pipeline.CreateBuilder();

builder.Configuration.AddJsonFile("appsettings.json");
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Services
    .AddOptions<BuildOptions>()
    .Bind(builder.Configuration.GetSection("Build"));
builder.Services
    .AddOptions<PublishOptions>()
    .Bind(builder.Configuration.GetSection("Publish"));

switch (mode)
{
    case "compile":
        builder.Services.AddModule<CompileProjectModule>();
        break;
    case "pack":
        builder.Services.AddModule<CreateInstallersModule>();
        break;
    case "publish":
        builder.Services.AddModule<PublishGithubModule>();
        break;
    case "version":
        builder.Services.AddModule<ResolveVersioningModule>();
        break;
    default:
#pragma warning disable ConsoleUse // Контекстного логгера пайплайна ещё нет: ошибка режима — до его сборки.
        Console.Error.WriteLine($"Unknown pipeline mode '{mode}'. Expected: compile (default), pack, publish or version.");
#pragma warning restore ConsoleUse
        Environment.ExitCode = 2;
        return;
}

await (await builder.BuildAsync()).RunAsync();
