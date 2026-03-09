using Microsoft.CodeAnalysis;

namespace NotNullAnalyzer;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor NOTNULL001 = new(
        id: "NOTNULL001",
        title: "Nullable member can be made non-nullable",
        messageFormat: "'{0}' is declared nullable but is only assigned non-nullable values",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
