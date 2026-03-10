# NotNullMigration.Analyzer

[![NuGet](https://img.shields.io/nuget/v/NotNullMigration.Analyzer.svg)](https://www.nuget.org/packages/NotNullMigration.Analyzer)
[![Build](https://github.com/tomasbruckner/not-null-migration-analyzer/actions/workflows/ci.yml/badge.svg)](https://github.com/tomasbruckner/not-null-migration-analyzer/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A Roslyn analyzer that helps migrate your C# projects to nullable-enabled (`<Nullable>enable</Nullable>`) by detecting nullable properties and fields that are only ever assigned non-nullable values.

## Installation

```bash
dotnet add package NotNullMigration.Analyzer
```

## Example

The analyzer detects nullable members that can safely be made non-nullable:

```csharp
// Before — NOTNULL001: 'Name' is declared nullable but is only assigned non-nullable values
public class User
{
    public string? Name { get; set; }

    public User(string name)
    {
        Name = name;
    }

    public void UpdateName(string newName)
    {
        Name = newName;
    }
}
```

Apply the code fix to remove the unnecessary `?`:

```csharp
// After — no diagnostic
public class User
{
    public string Name { get; set; }

    public User(string name)
    {
        Name = name;
    }

    public void UpdateName(string newName)
    {
        Name = newName;
    }
}
```

## Rules

| Rule ID | Category | Severity | Description |
|---------|----------|----------|-------------|
| NOTNULL001 | Design | Warning | Nullable member can be made non-nullable |

## How It Works

The analyzer uses the Roslyn IOperation API to track all assignments to nullable properties and fields across the entire compilation. At `CompilationEnd`, it reports any member that was only assigned non-nullable values.

For interface properties, it aggregates across all implementations — reporting only when every implementation assigns exclusively non-nullable values.

## Configuration

You can change the severity or disable the rule in your `.editorconfig`:

```editorconfig
[*.cs]
# Disable the rule
dotnet_diagnostic.NOTNULL001.severity = none

# Or change to error
dotnet_diagnostic.NOTNULL001.severity = error
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for build instructions and how to submit changes.

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.
