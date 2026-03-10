# Contributing to NotNullMigration.Analyzer

Thanks for your interest in contributing!

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Building

```bash
dotnet build
```

## Running Tests

```bash
dotnet test
```

## Testing the Analyzer Locally

### Via project reference

Add a project reference to the analyzer in your test project:

```xml
<ProjectReference Include="path/to/NotNullAnalyzer.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

### Via local NuGet package

```bash
dotnet pack src/NotNullAnalyzer/NotNullAnalyzer.csproj -o ./nupkgs
dotnet add package NotNullMigration.Analyzer --source ./nupkgs
```

## Submitting Changes

1. Fork the repository
2. Create a feature branch (`git checkout -b my-feature`)
3. Make your changes
4. Ensure all tests pass (`dotnet test`)
5. Commit with a descriptive message
6. Push to your fork and open a Pull Request against `main`

## Code Style

This project uses an `.editorconfig` for consistent formatting. Your editor should pick it up automatically.
