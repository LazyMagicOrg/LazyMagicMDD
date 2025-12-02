# AGENTS.md

## Build & Test Commands
```bash
dotnet build LazyMagicMDD.sln                    # Build all
dotnet test                                       # Run all tests
dotnet test --filter "FullyQualifiedName~TestName"  # Single test
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # Specific method
```

## Code Style
- **Namespace**: `LazyMagic` for all generator code
- **Imports**: System namespaces first, then third-party (YamlDotNet, NSwag, Newtonsoft), then project
- **Async**: Use `async Task` with `Async` suffix (e.g., `ProcessAsync`, `GenerateAsync`)
- **Properties**: PascalCase with auto-properties; use `{ get; set; }` on single line
- **Fields**: camelCase with access modifier (e.g., `protected bool defaultsAssigned`)
- **Regions**: Use `#region` for organizing code sections (Properties, Public Methods, etc.)
- **Braces**: Allman style (opening brace on new line for classes/methods)
- **Tests**: xUnit with `[Theory]/[InlineData]` for parameterized, `[Fact]` for single cases
- **Test pattern**: Arrange/Act/Assert with descriptive method names like `MethodName_Condition_ExpectedResult`
- **Error handling**: Catch exceptions, log via `LzLogger`, rethrow with context message
- **Virtual methods**: Base classes use `virtual` for override points in derived classes
