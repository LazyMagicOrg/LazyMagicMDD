# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the entire solution
dotnet build LazyMagicMDD.sln

# Build specific projects
dotnet build LazyMagicGenerator --configuration Release
dotnet build LazyMagicCLI --configuration Release

# Run tests
dotnet test LazyMagicGenerator.Test

# Run a specific test
dotnet test LazyMagicGenerator.Test --filter "FullyQualifiedName~GenerateOperationIdTests"

# Package the CLI tool
dotnet pack LazyMagicCLI -c Release

# Install the CLI tool locally
dotnet tool install --global --add-source ./Packages LazyMagicCLI
```

## Architecture Overview

LazyMagic is a code generation framework that creates AWS serverless applications from OpenAPI specifications and YAML configuration files. The solution consists of:

### Core Components

1. **LazyMagicGenerator** - The core library containing:
   - **Directives** - Configuration classes that define what to generate (Schema, Module, Container, Api, Service, etc.)
   - **ArtifactGeneration** - Classes that generate AWS resources and .NET projects
   - **Parsing** - YAML parsing and deserialization logic

2. **LazyMagicCLI** - Command-line tool packaged as a .NET global tool

3. **LazyMagicVsExt** - Visual Studio extension for generating projects

4. **LazyMagicGenerator.Test** - Unit tests using xUnit

### Key Concepts

- **Directives**: Configuration objects defined in YAML that specify what artifacts to generate
- **Artifacts**: Generated outputs including AWS SAM templates, .NET projects, and Lambda functions
- **Processing Pipeline**: Directives are processed in order: Schemas → Modules → Containers → Api → Authentication → Queue → Service → WebApp → Tenancy → Deployment

### Version Management

- Version is centrally managed in `Version.props`
- GitHub Actions workflow publishes NuGet packages on push to main branch
- Creates git tags automatically based on version

### Important Files

- `LazyMagic.yaml` - Main configuration file defining directives
- `Directives.cs` - Central processing logic for all directive types
- `LzOperationNameGenerator.cs` - Generates operation names with module prefixes

## Real-World Usage Example

The MagicPets project (`/mnt/c/Users/TimothyMay/repos/_Dev/MagicPets/Service/`) demonstrates how LazyMagic generates a complete serverless application:

### Generated Structure
From OpenAPI specs and `LazyMagic.yaml`, the generator creates:

1. **Schemas** - DTOs and database repositories
   - `AdminSchema`, `ConsumerSchema`, `SharedSchema`, etc.
   - Each with corresponding `*Repo` projects for DynamoDB operations

2. **Modules** - Controllers handling API operations
   - `AdminModule`, `ConsumerModule`, `PublicModule`, `StoreModule`
   - Each with authorization and controller implementations

3. **Containers** - Lambda functions
   - `AdminLambda`, `ConsumerLambda`, `PublicLambda`, `StoreLambda`
   - Each configured to host specific modules

4. **ClientSDKs** - Generated API clients
   - `AdminApi`, `ConsumerApi`, `PublicApi`, `StoreApi`

5. **AWS Templates** - SAM templates for deployment
   - Generated in `AWSTemplates/Generated/`

### Key Pattern
LazyMagic reads OpenAPI specifications (e.g., `openapi.admin.yaml`) and generates the entire stack from schemas to Lambda functions, maintaining consistent patterns across all components.