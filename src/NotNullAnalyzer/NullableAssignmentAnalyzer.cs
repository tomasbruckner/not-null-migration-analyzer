using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NotNullAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableAssignmentAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.NOTNULL001);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var tracker = new ConcurrentDictionary<ISymbol, NullTrackingInfo>(SymbolEqualityComparer.Default);

            // Register symbol actions to find nullable properties and fields
            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var symbol = symbolContext.Symbol;
                ITypeSymbol type;
                Location location;

                switch (symbol)
                {
                    case IPropertySymbol property:
                        // Skip properties whose nullability is constrained by a base type or interface
                        if (property.IsOverride || ImplementsInterfaceMember(property))
                            return;
                        type = property.Type;
                        location = property.Locations.FirstOrDefault()!;
                        break;
                    case IFieldSymbol field:
                        type = field.Type;
                        location = field.Locations.FirstOrDefault()!;
                        break;
                    default:
                        return;
                }

                if (type.NullableAnnotation == NullableAnnotation.Annotated && location != null)
                {
                    tracker.TryAdd(symbol, new NullTrackingInfo(symbol, location));
                }
            }, SymbolKind.Property, SymbolKind.Field);

            // Track simple assignments: obj.Prop = value
            compilationContext.RegisterOperationAction(operationContext =>
            {
                var assignment = (IAssignmentOperation)operationContext.Operation;
                if (TryGetTargetSymbol(assignment.Target, out var targetSymbol) &&
                    tracker.TryGetValue(targetSymbol!, out var info))
                {
                    info.RecordAssignment(IsNullableValue(assignment.Value));
                }
            }, OperationKind.SimpleAssignment, OperationKind.CompoundAssignment);

            // Track property initializers: public string? Name { get; set; } = "value";
            compilationContext.RegisterOperationAction(operationContext =>
            {
                var initializer = (IPropertyInitializerOperation)operationContext.Operation;

                // Skip `= null` initializers — they are just the implicit default
                // for a nullable type and should not count as real assignments.
                if (IsNullLiteralInitializer(initializer.Value))
                    return;

                foreach (var property in initializer.InitializedProperties)
                {
                    if (tracker.TryGetValue(property, out var info))
                    {
                        info.RecordAssignment(IsNullableValue(initializer.Value));
                    }
                }
            }, OperationKind.PropertyInitializer);

            // Track field initializers: private string? _name = "value";
            compilationContext.RegisterOperationAction(operationContext =>
            {
                var initializer = (IFieldInitializerOperation)operationContext.Operation;

                // Skip `= null` initializers — they are just the implicit default
                // for a nullable type and should not count as real assignments.
                if (IsNullLiteralInitializer(initializer.Value))
                    return;

                foreach (var field in initializer.InitializedFields)
                {
                    if (tracker.TryGetValue(field, out var info))
                    {
                        info.RecordAssignment(IsNullableValue(initializer.Value));
                    }
                }
            }, OperationKind.FieldInitializer);

            // Report at compilation end
            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var kvp in tracker)
                {
                    var info = kvp.Value;
                    if (info.AssignmentCount > 0 && !info.HasNullableAssignment)
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.NOTNULL001,
                            info.DeclarationLocation,
                            info.Symbol.Name);
                        endContext.ReportDiagnostic(diagnostic);
                    }
                }
            });
        });
    }

    private static bool ImplementsInterfaceMember(IPropertySymbol property)
    {
        var containingType = property.ContainingType;
        if (containingType == null)
            return false;

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var member in iface.GetMembers().OfType<IPropertySymbol>())
            {
                var impl = containingType.FindImplementationForInterfaceMember(member);
                if (SymbolEqualityComparer.Default.Equals(impl, property))
                    return true;
            }
        }

        return false;
    }

    private static bool TryGetTargetSymbol(IOperation target, out ISymbol? symbol)
    {
        switch (target)
        {
            case IPropertyReferenceOperation propRef:
                symbol = propRef.Property;
                return true;
            case IFieldReferenceOperation fieldRef:
                symbol = fieldRef.Field;
                return true;
            default:
                symbol = null;
                return false;
        }
    }

    private static bool IsNullLiteralInitializer(IOperation value)
    {
        return value.ConstantValue.HasValue && value.ConstantValue.Value == null;
    }

    private static bool IsNullableValue(IOperation value)
    {
        var type = value.Type;
        if (type == null)
            return true;

        if (type.NullableAnnotation == NullableAnnotation.Annotated)
            return true;

        if (value.ConstantValue.HasValue && value.ConstantValue.Value == null)
            return true;

        if (value is IDefaultValueOperation && type.IsReferenceType)
            return true;

        return false;
    }
}
