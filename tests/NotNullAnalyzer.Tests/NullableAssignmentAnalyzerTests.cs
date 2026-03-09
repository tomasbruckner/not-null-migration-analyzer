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

    [Fact]
    public async Task NullableProperty_AssignedNonNullableInConstructor_Reports()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? {|#0:Name|} { get; set; }

    public MyClass(string name)
    {
        Name = name;
    }
}
",
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.NOTNULL001)
                    .WithLocation(0)
                    .WithArguments("Name"),
            },
        };
        await test.RunAsync();
    }
}
