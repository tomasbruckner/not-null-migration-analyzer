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

    [Fact]
    public async Task NullableProperty_InitAccessor_AssignedNonNullable_Reports()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? {|#0:Name|} { get; init; }

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
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_MixedNullAndNonNull_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? Name { get; set; }

    public MyClass(string name)
    {
        Name = name;
    }

    public void Clear()
    {
        Name = null;
    }
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableField_AssignedDefault_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    private string? _name = default;
}
",
        };
        await test.RunAsync();
    }

    // ========== null! tests ==========

    [Fact]
    public async Task NullableProperty_AssignedNullForgiving_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? Name { get; set; }

    public MyClass()
    {
        Name = null!;
    }
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableField_AssignedNullForgivingInitializer_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    private string? _name = null!;
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_AssignedNullForgivingAndNonNull_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class MyClass
{
    public string? Name { get; set; }

    public MyClass(string name) { Name = name; }
    public void Reset() { Name = null!; }
}
",
        };
        await test.RunAsync();
    }

    // ========== Inheritance tests ==========

    [Fact]
    public async Task NullableProperty_OverridesVirtualBase_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public class BaseClass
{
    public virtual string? Name { get; set; }
}

public class DerivedClass : BaseClass
{
    public override string? Name { get; set; }

    public DerivedClass(string name)
    {
        Name = name;
    }
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_ImplementsInterface_AllImplsNonNullable_Reports()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
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
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.NOTNULL001)
                    .WithLocation(0)
                    .WithLocation(1)
                    .WithArguments("Name"),
            },
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_ImplementsInterface_MultipleImplsAllNonNullable_Reports()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public interface INameable
{
    string? {|#0:Name|} { get; set; }
}

public class ClassA : INameable
{
    public string? {|#1:Name|} { get; set; }
    public ClassA(string name) { Name = name; }
}

public class ClassB : INameable
{
    public string? {|#2:Name|} { get; set; }
    public ClassB(string name) { Name = name; }
}
",
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.NOTNULL001)
                    .WithLocation(0)
                    .WithLocation(1)
                    .WithLocation(2)
                    .WithArguments("Name"),
            },
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_ImplementsInterface_OneImplNullable_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public interface INameable
{
    string? Name { get; set; }
}

public class ClassA : INameable
{
    public string? Name { get; set; }
    public ClassA(string name) { Name = name; }
}

public class ClassB : INameable
{
    public string? Name { get; set; }
    public void Clear() { Name = null; }
    public ClassB(string name) { Name = name; }
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_ImplementsInterface_NoImplementations_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public interface INameable
{
    string? Name { get; set; }
}
",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableProperty_OverridesAbstract_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<NullableAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = @"
#nullable enable
public abstract class BaseClass
{
    public abstract string? Name { get; set; }
}

public class DerivedClass : BaseClass
{
    public override string? Name { get; set; }

    public DerivedClass(string name)
    {
        Name = name;
    }
}
",
        };
        await test.RunAsync();
    }
}
