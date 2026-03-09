using System.Threading;
using Microsoft.CodeAnalysis;

namespace NotNullAnalyzer;

internal class NullTrackingInfo
{
    public ISymbol Symbol { get; }
    public Location DeclarationLocation { get; }

    private int _hasNullableAssignment;
    private int _assignmentCount;

    public bool HasNullableAssignment => Volatile.Read(ref _hasNullableAssignment) != 0;
    public int AssignmentCount => Volatile.Read(ref _assignmentCount);

    public NullTrackingInfo(ISymbol symbol, Location declarationLocation)
    {
        Symbol = symbol;
        DeclarationLocation = declarationLocation;
    }

    public void RecordAssignment(bool isNullable)
    {
        Interlocked.Increment(ref _assignmentCount);
        if (isNullable)
        {
            Interlocked.Exchange(ref _hasNullableAssignment, 1);
        }
    }
}
