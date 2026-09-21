# Build pipeline

The separate `Revit.AddinManager.Build.slnx` solution contains the build pipeline. It compiles
the launcher and creates one MSI.

Build the pipeline solution itself:

```powershell
dotnet build build/Revit.AddinManager.Build.slnx -c Release
```

Build the launcher:

```powershell
dotnet run --project build/Revit.AddinManager.Build.csproj -c Release
```

Build the launcher and create the installer:

```powershell
dotnet run --project build/Revit.AddinManager.Build.csproj -c Release -- pack
```

The single MSI is written to the configured `Build:OutputDirectory` (`output` by
default). The product version is calculated by GitVersion from Git tags. Create and
checkout an exact stable tag before producing the release installer:

```powershell
git tag v1.4.0
git switch --detach v1.4.0
dotnet run --project build/Revit.AddinManager.Build.csproj -c Release -- pack
```

GitHub releases are created manually by the `Publish release` workflow. Tag the
current commit (for example, `v1.4.0`), push the commit and tag, and run the
workflow for that branch. GitVersion resolves the version from the tag; the
pipeline creates the MSI file and publishes it as the release asset.

The workflow delegates the complete release to ModularPipelines. The equivalent
local command (requires authenticated GitHub CLI) is:

```powershell
dotnet run --project build/Revit.AddinManager.Build.csproj -c Release -- publish
```

Inspect the version resolved by GitVersion without compiling the launcher:

```powershell
dotnet run --project build/Revit.AddinManager.Build.csproj -c Release --no-launch-profile -- version
```

The installer generator (`installer/`) produces exactly one MSI per product version
(`RevitAddinManager-<version>.msi`), upgrading previous installs in place via a
stable upgrade code.
