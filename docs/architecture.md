# NOTNULL001 — Architecture & Edge Cases

## What triggers a diagnostic

NOTNULL001 reports when a nullable property or field (`T?`) is **only ever assigned non-nullable values** across the entire compilation. This means the `?` annotation is unnecessary and can be safely removed.

A diagnostic is reported when **all** of these conditions are true:

1. The member is declared with a nullable type (`string?`, `int?`, `T?`, `Nullable<T>`)
2. `#nullable enable` is active at the declaration site
3. At least one assignment to the member exists
4. Every assignment writes a non-nullable value

```csharp
// NOTNULL001 reported — Name is nullable but only assigned non-nullable values
public class User
{
    public string? Name { get; set; }
    public User(string name) { Name = name; }
}
```

## What does NOT trigger a diagnostic

### No assignments

A nullable member with zero tracked assignments is left alone. It may be set via reflection, serialization, or external code.

```csharp
public string? Name { get; set; }  // no assignments → no diagnostic
```

### Any nullable assignment

A single nullable assignment anywhere in the compilation suppresses the diagnostic for that member.

```csharp
public string? Name { get; set; }
public void Set(string name) { Name = name; }  // non-nullable
public void Clear() { Name = null; }            // nullable — no diagnostic
```

### `= null` initializer

An explicit `= null` initializer is treated as the implicit default for a nullable type and is **not** counted as an assignment. Without this rule, every `string? Name { get; set; } = null;` would incorrectly appear as "has an assignment."

```csharp
public string? Name { get; set; } = null;  // ignored — no diagnostic
```

### `= default` initializer

`default` for a reference type is `null`, so it counts as a nullable assignment.

```csharp
private string? _name = default;  // nullable value — no diagnostic
```

### `null!` (null-forgiving operator)

`null!` suppresses compiler warnings but the underlying constant value is still `null`. The analyzer looks at `ConstantValue`, not the suppression operator, so `null!` is treated as a nullable assignment.

```csharp
public string? Name { get; set; }
public MyClass() { Name = null!; }  // still null — no diagnostic
```

### Assignment from nullable source

When the assigned value has a nullable type annotation (`string?`, `T?`) or comes from a method returning nullable, the member keeps its `?`.

```csharp
public string? Name { get; set; }
public void Set(string? value) { Name = value; }     // nullable param
public void Load() { Name = GetName(); }              // string? return
private string? GetName() => null;
```

### Ternary / conditional expressions that may be null

When one branch of a conditional is `null`, the result type is nullable.

```csharp
public string? Name { get; set; }
public void Set(bool b, string v) { Name = b ? v : null; }  // nullable
```

### `as` casts

The `as` operator returns a nullable type.

```csharp
public string? Name { get; set; }
public void Set(object obj) { Name = obj as string; }  // nullable
```

### Override properties

Properties that override a virtual or abstract base member are **skipped entirely**. Nullability is a contract defined by the base type — a derived class cannot narrow it without breaking substitutability.

```csharp
public abstract class Base
{
    public abstract string? Name { get; set; }
}
public class Derived : Base
{
    public override string? Name { get; set; }
    public Derived(string n) { Name = n; }  // no diagnostic
}
```

### Non-nullable members

Members declared without `?` are not tracked at all — there is nothing to remove.

### Expression-bodied (read-only) properties

`public string? Name => _name;` has no setter and no assignments, so it falls under "no assignments."

## Interface properties

Interface properties use special aggregation logic. An implementation property is **never** reported individually — only the interface member can trigger a diagnostic.

### When the diagnostic fires

The interface property is reported when **every** implementation in the compilation:
- Is tracked (nullable-annotated)
- Has at least one assignment
- Has only non-nullable assignments

The diagnostic includes additional locations pointing to each implementation, so the code fix can update them all at once.

```csharp
// NOTNULL001 reported on INameable.Name with additional locations on ClassA.Name and ClassB.Name
public interface INameable { string? Name { get; set; } }
public class ClassA : INameable
{
    public string? Name { get; set; }
    public ClassA(string n) { Name = n; }
}
public class ClassB : INameable
{
    public string? Name { get; set; }
    public ClassB(string n) { Name = n; }
}
```

### When it does NOT fire

- **No implementations** exist — nothing to verify
- **Any implementation** assigns a nullable value — the interface must stay nullable
- **Any implementation** is missing from the tracker (not nullable-annotated)

```csharp
// No diagnostic — ClassB assigns null
public interface INameable { string? Name { get; set; } }
public class ClassA : INameable
{
    public string? Name { get; set; }
    public ClassA(string n) { Name = n; }
}
public class ClassB : INameable
{
    public string? Name { get; set; }
    public void Clear() { Name = null; }
}
```

## Generics

Generic type parameters with nullable annotations work the same way:

```csharp
// NOTNULL001 — Value is only assigned non-nullable T
public class Container<T> where T : class
{
    public T? Value { get; set; }
    public Container(T value) { Value = value; }
}

// Same for struct constraints
public class Box<T> where T : struct
{
    public T? Value { get; set; }
    public Box(T value) { Value = value; }
}
```

## Partial classes

Assignments are tracked across all parts of a partial class. If one file assigns non-nullable and another assigns `null`, the member is correctly left alone.

```csharp
// File1.cs
public partial class MyClass { public string? Name { get; set; } }

// File2.cs — assigns null, so no diagnostic
public partial class MyClass { public void Clear() { Name = null; } }
```

## Compilation-wide analysis

The analyzer uses a three-phase approach:

1. **CompilationStart** — creates a `ConcurrentDictionary<ISymbol, NullTrackingInfo>` to track all nullable members
2. **Operation actions** — registers handlers for `SimpleAssignment`, `CompoundAssignment`, `PropertyInitializer`, and `FieldInitializer` to record every assignment
3. **CompilationEnd** — iterates all tracked members and reports those with only non-nullable assignments

This design ensures assignments across files, partial classes, and object initializers are all captured. Thread safety is handled via `ConcurrentDictionary`, `Interlocked`, and `Volatile`.

## Assignment forms tracked

The IOperation API captures all assignment forms uniformly:

| Form | Example | IOperation kind |
|------|---------|-----------------|
| Direct assignment | `obj.Name = value` | `SimpleAssignment` |
| Compound assignment | `obj.Count += 1` | `CompoundAssignment` |
| Object initializer | `new Foo { Name = "x" }` | `SimpleAssignment` |
| Property initializer | `string? Name { get; set; } = "x"` | `PropertyInitializer` |
| Field initializer | `string? _name = "x"` | `FieldInitializer` |
| Constructor assignment | `Name = name` | `SimpleAssignment` |
