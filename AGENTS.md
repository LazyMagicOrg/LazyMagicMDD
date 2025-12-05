# AGENTS.md

## Overview

LazyMagic is a model-driven code generation framework that creates complete AWS cloud applications from OpenAPI specifications and YAML configuration files. Version 3.x uses AWS App Runner with containerized ASP.NET Core applications (not AWS Lambda + API Gateway).

## Build & Test Commands
```bash
dotnet build LazyMagicMDD.sln                    # Build all
dotnet build LazyMagicGenerator -c Release       # Build generator (Release)
dotnet build LazyMagicCLI -c Release             # Build CLI (Release)
dotnet test                                       # Run all tests
dotnet test LazyMagicGenerator.Test              # Run generator tests
dotnet test TestDirectiveValidation              # Run directive validation tests
dotnet test --filter "FullyQualifiedName~TestName"  # Single test
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # Specific method
dotnet pack LazyMagicCLI -c Release              # Package CLI tool
dotnet tool install --global --add-source ./Packages LazyMagicCLI  # Install CLI locally
```

## Architecture

### Core Components
1. **LazyMagicGenerator** - Core library with Directives, ArtifactGeneration, and Parsing
2. **LazyMagicCLI** - Command-line tool that reads `LazyMagic.yaml` and calls `LzSolution.ProcessAsync()`
3. **LazyMagicGenerator.Test** - Unit tests using xUnit
4. **TestDirectiveValidation** - Directive validation tests

### Key Entry Points
- `LzSolution.cs` - Main orchestrator that loads directives and generates artifacts
- `Directives.cs` - Central processing logic for all directive types
- `DirectiveBase.cs` - Base class for all directives
- `LzOperationNameGenerator.cs` - Generates operation names with module prefixes

### Directive Processing Order
Directives are processed in dependency order:
1. **Schema** → DTOs and DynamoDB repositories
2. **Module** → Controllers handling API operations
3. **Container** → ASP.NET Core apps with Dockerfiles for App Runner
4. **Api** → Reserved for future ALB + Fargate support
5. **Authentication** → Cognito user pools
6. **Queue** → SQS queues
7. **Service** → Service-level AWS SAM templates
8. **WebApp** → Web application configurations
9. **Tenancy** → Multi-tenant deployment templates
10. **Deployment** → Deployment configurations

### Generated Artifacts
From OpenAPI specs and `LazyMagic.yaml`, the generator creates:
- **Schemas** - `*Schema` (DTOs) and `*Repo` (DynamoDB operations) projects
- **Modules** - Controller implementations with authorization
- **Containers** - ASP.NET Core apps with Dockerfiles (port 8080)
- **ClientSDKs** - Strongly-typed API clients (`AspDotNetApiSDKProject`)
- **AWS Templates** - SAM templates in `AWSTemplates/Generated/`

### Key Patterns
- **Model-Driven**: OpenAPI specs drive entire stack generation
- **Module Prefix**: Operations prefixed with module names for uniqueness
- **Default Directives**: Common configs defined once with `IsDefault: true`, reused via `Defaults` property
- **Container-Based**: ASP.NET Core in Docker on App Runner (no Lambda support in v3.x)

## Configuration

### LazyMagic.yaml Structure
```yaml
LazyMagicDirectivesVersion: 2.0.0
Directives:
  SchemaDefault:          # Default configuration
    Type: Schema
    IsDefault: true
    Artifacts:
      DotNetSchemaProject:
      DotNetRepoProject:
  
  MySchema:               # Specific implementation
    Type: Schema
    Defaults: SchemaDefault
    OpenApiSpecs:
    - openapi.my-schema.yaml
```

### Version Management
- Version centrally managed in `Version.props`
- GitHub Actions (`.github/workflows/publish-nuget.yaml`) publishes NuGet packages and creates git tags on push to main

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
