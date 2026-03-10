using System.Collections.Concurrent;
using System.Collections.Generic;
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
                        // Skip overrides — their nullability is constrained by the base type
                        if (property.IsOverride)
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
                var compilation = endContext.Compilation;

                foreach (var kvp in tracker)
                {
                    var info = kvp.Value;
                    var symbol = info.Symbol;

                    if (symbol is IPropertySymbol property)
                    {
                        // Interface-implementing properties: skip independent reporting
                        if (ImplementsInterfaceMember(property))
                            continue;

                        // Interface properties: check if all implementations are non-nullable
                        if (property.ContainingType?.TypeKind == TypeKind.Interface)
                        {
                            ReportInterfacePropertyIfAllImplsNonNullable(
                                endContext, compilation, property, info, tracker);
                            continue;
                        }
                    }

                    // Regular properties and fields
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

    private static void ReportInterfacePropertyIfAllImplsNonNullable(
        CompilationAnalysisContext endContext,
        Compilation compilation,
        IPropertySymbol interfaceProperty,
        NullTrackingInfo interfaceInfo,
        ConcurrentDictionary<ISymbol, NullTrackingInfo> tracker)
    {
        var interfaceType = interfaceProperty.ContainingType;
        var implementations = new List<NullTrackingInfo>();
        var foundAll = true;

        foreach (var type in GetAllNamedTypes(compilation.GlobalNamespace))
        {
            if (type.TypeKind == TypeKind.Interface)
                continue;

            if (!type.AllInterfaces.Contains(interfaceType, SymbolEqualityComparer.Default))
                continue;

            var impl = type.FindImplementationForInterfaceMember(interfaceProperty);
            if (impl is not IPropertySymbol implProperty)
            {
                foundAll = false;
                break;
            }

            // The implementation must be tracked and have only non-nullable assignments
            if (tracker.TryGetValue(implProperty, out var implInfo))
            {
                if (implInfo.AssignmentCount == 0 || implInfo.HasNullableAssignment)
                {
                    foundAll = false;
                    break;
                }
                implementations.Add(implInfo);
            }
            else
            {
                // Implementation not tracked (not nullable-annotated or not found)
                foundAll = false;
                break;
            }
        }

        // Need at least one implementation and all must be non-nullable
        if (!foundAll || implementations.Count == 0)
            return;

        // Report on the interface property with additional locations for implementations
        var additionalLocations = implementations
            .Select(i => i.DeclarationLocation)
            .ToImmutableArray();

        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.NOTNULL001,
            interfaceInfo.DeclarationLocation,
            additionalLocations,
            properties: null,
            interfaceProperty.Name);
        endContext.ReportDiagnostic(diagnostic);
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol root)
    {
        foreach (var type in root.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in GetNestedTypes(type))
                yield return nested;
        }

        foreach (var ns in root.GetNamespaceMembers())
        {
            foreach (var type in GetAllNamedTypes(ns))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deepNested in GetNestedTypes(nested))
                yield return deepNested;
        }
    }

    private static bool ImplementsInterfaceMember(IPropertySymbol property)
    {
        var containingType = property.ContainingType;
        if (containingType == null || containingType.TypeKind == TypeKind.Interface)
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
