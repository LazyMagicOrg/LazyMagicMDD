# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the entire solution
dotnet build LazyMagicMDD.sln

# Build specific projects
dotnet build LazyMagicGenerator --configuration Release
dotnet build LazyMagicCLI --configuration Release

# Run all tests
dotnet test

# Run tests for specific project
dotnet test LazyMagicGenerator.Test
dotnet test TestDirectiveValidation

# Run a specific test
dotnet test LazyMagicGenerator.Test --filter "FullyQualifiedName~GenerateOperationIdTests"

# Package the CLI tool
dotnet pack LazyMagicCLI -c Release

# Install the CLI tool locally
dotnet tool install --global --add-source ./Packages LazyMagicCLI
```

## Architecture Overview

LazyMagic is a model-driven code generation framework that creates complete AWS serverless applications from OpenAPI specifications and YAML configuration files.

### Core Components

1. **LazyMagicGenerator** - The core library containing:
   - **Directives** (`LazyMagicGenerator/Directives/`) - Configuration classes that define what to generate
   - **ArtifactGeneration** (`LazyMagicGenerator/ArtifactGeneration/`) - Generators for AWS resources and .NET projects
   - **Parsing** (`LazyMagicGenerator/Parsing/`) - YAML parsing and deserialization logic

2. **LazyMagicCLI** - Command-line tool packaged as a .NET global tool
   - Entry point: `LazyMagicCLI/Program.cs`
   - Reads `LazyMagic.yaml` from target directory
   - Calls `LzSolution.ProcessAsync()` to generate projects

3. **LazyMagicVsExt** - Visual Studio extension for integrated project generation

4. **LazyMagicGenerator.Test** - Unit tests using xUnit

5. **TestDirectiveValidation** - Test project for directive validation

### Processing Pipeline

Directives are processed in dependency order:

1. **Schema** → Generate DTOs and DynamoDB repositories
2. **Module** → Generate controllers handling API operations
3. **Container** → Generate Lambda functions hosting modules
4. **Api** → Generate API Gateway configurations
5. **Authentication** → Generate Cognito user pools
6. **Queue** → Generate SQS queues
7. **Service** → Generate service-level AWS SAM templates
8. **WebApp** → Generate web application configurations
9. **Tenancy** → Generate multi-tenant deployment templates
10. **Deployment** → Generate deployment configurations

### Key Classes and Entry Points

- **`LzSolution.cs`** - Main orchestrator that loads directives and generates artifacts
- **`Directives.cs`** - Central processing logic for all directive types
- **`DirectiveBase.cs`** - Base class for all directives
- **`LzOperationNameGenerator.cs`** - Generates operation names with module prefixes

### Configuration

- **`LazyMagic.yaml`** - Main configuration file defining directives
  - Default Directives: Reusable configurations with `IsDefault: true`
  - Schema/Module/Container/Api Directives: Specify what to generate
  - References OpenAPI specs (e.g., `openapi.admin.yaml`)

### Version Management

- Version is centrally managed in `Version.props`
- GitHub Actions workflow (`.github/workflows/publish-nuget.yaml`) on push to main:
  - Builds and publishes NuGet packages
  - Creates git tags automatically based on version
  - Uses .NET 9.x

### Generated Artifact Structure

From OpenAPI specs and `LazyMagic.yaml`, the generator creates:

1. **Schemas** - DTOs and database repositories
   - `*Schema` projects with data models
   - `*Repo` projects with DynamoDB operations

2. **Modules** - Controllers handling API operations
   - Controller implementations with authorization
   - Operation handlers mapped from OpenAPI

3. **Containers** - Lambda functions
   - Host specific modules
   - Configure dependency injection

4. **ClientSDKs** - Generated API clients
   - Strongly-typed clients for each API

5. **AWS Templates** - SAM templates for deployment
   - Generated in `AWSTemplates/Generated/`

### Key Patterns

- **Model-Driven**: OpenAPI specs drive generation of entire stack
- **Module Prefix**: Operations are prefixed with module names for uniqueness
- **Default Directives**: Common configurations are defined once and reused
- **Artifact Templates**: Customizable templates for generated code structure