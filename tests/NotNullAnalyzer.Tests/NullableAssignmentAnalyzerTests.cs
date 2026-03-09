using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NotNullAnalyzer;
using Xunit;

namespace NotNullAnalyzer.Tests;

public class NullableAssignmentAnalyzerTests
{
    [Fact]
    public async Task NoCode_NoDiagnostics()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = "",
        };
        await test.RunAsync();
    }
}
