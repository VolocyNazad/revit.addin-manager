using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Options;
using Shouldly;

namespace Revit.AddinManager.Build.Modules;

[DependsOn<CompileProjectModule>]
[DependsOn<ResolveVersioningModule>]
public sealed class CreateInstallersModule(IOptions<BuildOptions> options) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioning = (await context.GetModule<ResolveVersioningModule>()).ValueOrDefault!;

        await context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = BuildPaths.InstallerProject,
            Configuration = "Release"
        }, cancellationToken: cancellationToken);

        string installer = BuildPaths.GetInstallerExecutable();
        File.Exists(installer).ShouldBeTrue($"Installer generator was not found: {installer}");

        string outputDirectory = Path.GetFullPath(options.Value.OutputDirectory, BuildPaths.Root);
        Directory.CreateDirectory(outputDirectory);

        string sourceDirectory = (await context.GetModule<CompileProjectModule>()).ValueOrDefault!;
        Directory.Exists(sourceDirectory).ShouldBeTrue($"Build output was not found: {sourceDirectory}");

        await context.Shell.Command.ExecuteCommandLineTool(
            new GenericCommandLineToolOptions(installer)
            {
                Arguments =
                [
                    versioning.Version,
                    sourceDirectory,
                    outputDirectory
                ]
            }, cancellationToken: cancellationToken);

        string msiPath = Path.Combine(
            outputDirectory,
            $"RevitAddinManager-{versioning.Version}.msi");
        File.Exists(msiPath).ShouldBeTrue($"MSI was not created: {msiPath}");
    }
}
