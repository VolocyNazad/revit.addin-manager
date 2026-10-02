using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Options;
using Shouldly;

namespace Revit.AddinManager.Build.Modules;

[DependsOn<CreateInstallersModule>]
[DependsOn<ResolveVersioningModule>]
public sealed class PublishGithubModule(
    IOptions<BuildOptions> buildOptions,
    IOptions<PublishOptions> publishOptions) : Module
{
    protected override async Task ExecuteModuleAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        var versioning = (await context.GetModule<ResolveVersioningModule>()).ValueOrDefault!;
        Version.TryParse(versioning.Version, out _)
            .ShouldBeTrue($"GitHub releases require a stable version, but GitVersion produced '{versioning.Version}'");

        string outputDirectory = Path.GetFullPath(buildOptions.Value.OutputDirectory, BuildPaths.Root);
        string installer = Path.Combine(
            outputDirectory,
            $"RevitAddinManager-{versioning.Version}.msi");
        File.Exists(installer).ShouldBeTrue($"MSI was not created: {installer}");

        string notesPath = Path.Combine(outputDirectory, $"release-notes-{versioning.Version}.md");
        await File.WriteAllTextAsync(
            notesPath,
            ReleaseNotes.Create(BuildPaths.Changelog, versioning.Version, GetRepositoryUrl()),
            cancellationToken);

        var arguments = new List<string>
        {
            "release", "create", $"v{versioning.Version}",
            "--verify-tag",
            "--notes-file", notesPath,
            "--title", $"Revit.AddinManager {versioning.Version}"
        };

        if (publishOptions.Value.Draft)
            arguments.Add("--draft");

        arguments.Add(installer);
        await context.Shell.Command.ExecuteCommandLineTool(
            new GenericCommandLineToolOptions("gh") { Arguments = arguments },
            new CommandExecutionOptions { WorkingDirectory = BuildPaths.Root },
            cancellationToken: cancellationToken);
    }

    // GitHub Actions supplies both variables; a local publish omits the changelog link instead of guessing the URL.
    private static string? GetRepositoryUrl()
    {
        string? server = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL");
        string? repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        return string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(repository)
            ? null
            : $"{server.TrimEnd('/')}/{repository}";
    }
}
