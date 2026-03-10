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

    [Fact]
    public async Task NullableProperty_WithNonNullableInitializer_Reports()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? {|#0:Name|} { get; set; } = ""default"";
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

    [Fact]
    public async Task NullableField_AssignedNonNullableAcrossMethods_Reports()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    private string? {|#0:_name|};

    public void SetFromA(string a) { _name = a; }
    public void SetFromB(string b) { _name = b; }
}
",
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.NOTNULL001)
                    .WithLocation(0)
                    .WithArguments("_name"),
            },
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableValueType_AssignedLiteralOnly_Reports()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public int? {|#0:Count|} { get; set; }

    public MyClass()
    {
        Count = 42;
    }
}
",
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.NOTNULL001)
                    .WithLocation(0)
                    .WithArguments("Count"),
            },
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_AssignedViaObjectInitializer_Reports()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? {|#0:Name|} { get; set; }
}

public class Consumer
{
    public void Create()
    {
        var obj = new MyClass { Name = ""hello"" };
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

    [Fact]
    public async Task NullableProperty_AssignedNull_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? Name { get; set; }

    public void Reset() { Name = null; }
    public MyClass(string name) { Name = name; }
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_AssignedFromNullableVariable_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? Name { get; set; }

    public void Set(string? value) { Name = value; }
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_NoAssignments_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? Name { get; set; }
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_AssignedFromNullableMethod_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? Name { get; set; }

    public void Load()
    {
        Name = GetName();
    }

    private string? GetName() => null;
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_WithNullInitializer_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? Name { get; set; } = null;
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NonNullableProperty_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string Name { get; set; } = ""default"";
}
",
        };
        await test.RunAsync();
    }
}
