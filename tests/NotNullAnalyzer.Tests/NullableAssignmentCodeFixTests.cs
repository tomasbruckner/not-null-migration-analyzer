using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NotNullAnalyzer;
using System.Threading.Tasks;
using Xunit;

namespace NotNullAnalyzer.Tests;

public class NullableAssignmentCodeFixTests
{
    [Fact]
    public async Task CodeFix_RemovesNullableAnnotation_ReferenceType()
    {
        var test = new CSharpCodeFixTest<NullableAssignmentAnalyzer, NullableAssignmentCodeFixProvider, DefaultVerifier>
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
            FixedCode = @"
#nullable enable
public class MyClass
{
    public string Name { get; set; }

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
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task CodeFix_RemovesNullableAnnotation_ValueType()
    {
        var test = new CSharpCodeFixTest<NullableAssignmentAnalyzer, NullableAssignmentCodeFixProvider, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public int? {|#0:Count|} { get; set; }

    public MyClass()
    {
        Count = 5;
    }
}
",
            FixedCode = @"
#nullable enable
public class MyClass
{
    public int Count { get; set; }

    public MyClass()
    {
        Count = 5;
    }
}
",
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.NOTNULL001)
                    .WithLocation(0)
                    .WithArguments("Count"),
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task CodeFix_RemovesNullInitializer()
    {
        var test = new CSharpCodeFixTest<NullableAssignmentAnalyzer, NullableAssignmentCodeFixProvider, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? {|#0:Name|} { get; set; } = null;

    public MyClass(string name)
    {
        Name = name;
    }
}
",
            FixedCode = @"
#nullable enable
public class MyClass
{
    public string Name { get; set; }

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
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task CodeFix_RemovesNullableAnnotation_Field()
    {
        var test = new CSharpCodeFixTest<NullableAssignmentAnalyzer, NullableAssignmentCodeFixProvider, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    private string? {|#0:_name|};

    public MyClass(string name)
    {
        _name = name;
    }
}
",
            FixedCode = @"
#nullable enable
public class MyClass
{
    private string _name;

    public MyClass(string name)
    {
        _name = name;
    }
}
",
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.NOTNULL001)
                    .WithLocation(0)
                    .WithArguments("_name"),
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task CodeFix_RemovesNullableAnnotation_InterfaceAndImplementation()
    {
        var test = new CSharpCodeFixTest<NullableAssignmentAnalyzer, NullableAssignmentCodeFixProvider, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public interface INameable
{
    string? {|#0:Name|} { get; set; }
}

public class MyClass : INameable
{
    public string? {|#1:Name|} { get; set; }

    public MyClass(string name)
    {
        Name = name;
    }
}
",
            FixedCode = @"
#nullable enable
public interface INameable
{
    string Name { get; set; }
}

public class MyClass : INameable
{
    public string Name { get; set; }

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
                    .WithLocation(1)
                    .WithArguments("Name"),
            },
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
        };
        await test.RunAsync();
    }
}
