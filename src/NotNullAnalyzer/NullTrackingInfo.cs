using Microsoft.CodeAnalysis;

namespace NotNullAnalyzer;

internal class NullTrackingInfo
{
    public ISymbol Symbol { get; }
    public Location DeclarationLocation { get; }
    public bool HasNullableAssignment { get; set; }
    public int AssignmentCount { get; set; }

    public NullTrackingInfo(ISymbol symbol, Location declarationLocation)
    {
        Symbol = symbol;
        DeclarationLocation = declarationLocation;
    }
}
