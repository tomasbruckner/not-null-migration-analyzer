# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Git workflow

Never commit directly to `main`. Always create a feature branch and open a pull request.

## Commands

```bash
dotnet build                          # Build all projects
dotnet test                           # Run all 43 tests
dotnet test --filter "TestMethodName" # Run a single test by name
```

## Architecture

Roslyn analyzer (NOTNULL001) targeting netstandard2.0 with Roslyn 4.3.1. Tests run on net8.0 with xUnit.

### Three-phase compilation-wide approach

1. **CompilationStart** — Create `ConcurrentDictionary<ISymbol, NullTrackingInfo>` for tracking nullable symbols
2. **Operation Actions** — Track `SimpleAssignment`, `CompoundAssignment`, `PropertyInitializer`, `FieldInitializer` via IOperation API
3. **CompilationEnd** — Report diagnostics for symbols only assigned non-nullable values

### Key files

| File | Purpose |
|------|---------|
| `src/NotNullAnalyzer/NullableAssignmentAnalyzer.cs` | Core analyzer — CompilationStart/End, IOperation tracking |
| `src/NotNullAnalyzer/NullableAssignmentCodeFixProvider.cs` | Removes `?`/`Nullable<T>`, handles interface+impl multi-doc fix |
| `src/NotNullAnalyzer/NullTrackingInfo.cs` | Thread-safe tracking with `Interlocked`/`Volatile` |
| `src/NotNullAnalyzer/DiagnosticDescriptors.cs` | NOTNULL001 descriptor with `CompilationEnd` tag |

### Design decisions

- **IOperation API** over syntax — handles all assignment forms uniformly
- **Override properties skipped** — nullability is a base type contract
- **Interface properties aggregated** — report only when ALL implementations are non-nullable
- **`= null` initializers skipped** — implicit default, not a real assignment
- **`null!` detected via ConstantValue** — null-forgiving doesn't change the constant value

## Testing patterns

Tests use `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`. Key patterns:

- **CompilationEnd diagnostics require** `CodeFixTestBehaviors.SkipLocalDiagnosticCheck` in code fix tests
- Diagnostic locations use marker syntax: `{|#0:SymbolName|}`
- All test code must include `#nullable enable`
- `InternalsVisibleTo` exposes `DiagnosticDescriptors` to tests
- For `required` keyword tests: use `LanguageVersion.Preview` + `ReferenceAssemblies.Net.Net80`

## Release process

1. Update `CHANGELOG.md` and move entries from `AnalyzerReleases.Unshipped.md` to `Shipped.md`
2. Update `Version` in `src/NotNullAnalyzer/NotNullAnalyzer.csproj`
3. Push a `v*` tag — GitHub Actions publishes to NuGet automatically
