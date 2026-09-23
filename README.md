# revit.addin-manager

Pre-launch manager for Autodesk Revit `.addin` manifests (Revit 2021–2027):
browse every installed plugin across versions and scopes, toggle `.addin` files
on/off, and edit their manifests (structured form or raw XML) — all before Revit starts.

- Features and current scope: [docs/FEATURES.md](docs/FEATURES.md).
- User guide: [docs/WIKI.md](docs/WIKI.md).

## Quick start

Prerequisites: Windows and the .NET SDK from `global.json` (10.0.100+, `latestFeature` roll-forward).

```powershell
dotnet build Revit.AddinManager.slnx -c Release
dotnet test Revit.AddinManager.slnx -c Release --no-build
dotnet run --project src/AddinManager.Launcher -c Release
```

Single MSI and releases: [build/README.md](build/README.md).

## Development documentation

- [Development policy](docs/policies/development.md)
- [Repository guide and technology stack](docs/repository.md)

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting changes.

## License

Distributed under the [Apache License 2.0](LICENSE).
