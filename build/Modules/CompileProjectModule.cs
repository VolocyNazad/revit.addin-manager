using ModularPipelines.Attributes;
using ModularPipelines.Options;

namespace Revit.AddinManager.Build.Modules;

[DependsOn<ResolveVersioningModule>]
public sealed class CompileProjectModule : Module<string>
{
    protected override async Task<string?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        var versioning = (await context.GetModule<ResolveVersioningModule>()).ValueOrDefault!;

        await context.Shell.Command.ExecuteCommandLineTool(
            new GenericCommandLineToolOptions("dotnet")
            {
                Arguments =
                [
                    "build",
                    BuildPaths.LauncherProject,
                    "--configuration", BuildPaths.ReleaseConfiguration,
                    "--nologo",
                    "--consoleLoggerParameters:ErrorsOnly;Summary",
                    $"-p:Version={versioning.Version}",
                    "-p:DebugType=None",
                    "-p:DebugSymbols=false",
                    "-p:GenerateDocumentationFile=false"
                ]
            }, cancellationToken: cancellationToken);

        return BuildPaths.GetLauncherOutput();
    }
}
