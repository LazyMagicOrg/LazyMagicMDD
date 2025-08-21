# LazyMagic Model-Driven Development (MDD)

LazyMagic MDD is a powerful code generation framework that transforms OpenAPI specifications and YAML configuration into complete AWS serverless applications with .NET implementations. It follows a model-driven development approach where your API specifications drive the generation of schemas, controllers, Lambda functions, AWS infrastructure, and client SDKs.

## Overview

LazyMagic MDD generates complete serverless applications from:
- **OpenAPI Specifications** - Define your APIs, schemas, and operations
- **YAML Configuration** - Specify what components to generate and how they connect
- **Templates** - Customize the generated code structure

The framework produces ready-to-deploy AWS serverless applications with:
- .NET Lambda functions
- AWS SAM templates
- DynamoDB repositories
- API Gateway configurations
- Client SDKs
- Authentication and authorization

## Core Components

### 1. LazyMagicCLI
A .NET global tool that processes your configuration and generates projects.

**Installation:**
```bash
dotnet tool install --global --add-source ./Packages LazyMagicCLI
```

**Usage:**
```bash
# Generate projects from current directory
LazyMagicCLI

# Generate projects from specific solution path
LazyMagicCLI /path/to/solution
```

### 2. LazyMagicGenerator
The core library containing:
- **Directives** - Configuration classes that define generation targets
- **ArtifactGeneration** - Code generators for AWS resources and .NET projects
- **Parsing** - YAML processing and deserialization

### 3. LazyMagicVsExt
Visual Studio extension providing integrated project generation within the IDE.

## Key Concepts

### Directives
Directives are YAML configuration objects that specify what to generate. They follow a processing order:

1. **Schema** → Data Transfer Objects and DynamoDB repositories
2. **Module** → Controllers handling API operations  
3. **Container** → Lambda functions hosting modules
4. **Api** → API Gateway configurations
5. **Authentication** → Cognito user pools and authorization
6. **Queue** → SQS queues for async processing
7. **Service** → Service-level AWS SAM templates
8. **WebApp** → Web application configurations
9. **Tenancy** → Multi-tenant deployment templates
10. **Deployment** → Deployment configurations

### Artifacts
Generated outputs including:
- .NET projects and solutions
- AWS SAM templates
- Lambda function code
- Client SDK libraries
- Database repositories

## Configuration Structure

### LazyMagic.yaml
The main configuration file defining all directives:

```yaml
LazyMagicDirectivesVersion: 2.0.0

Directives:
  # Default configurations
  SchemaDefault:
    Type: Schema
    IsDefault: true
    Artifacts:
      DotNetSchemaProject: # DTOs
      DotNetRepoProject:   # DynamoDB repositories
  
  # Specific implementations
  ConsumerSchema:
    Type: Schema
    Defaults: SchemaDefault
    OpenApiSpecs:
    - openapi.consumer-schema.yaml
    
  ConsumerModule:
    Type: Module
    Defaults: ModuleDefault  
    OpenApiSpecs:
    - openapi.consumer.yaml
    
  ConsumerLambda:
    Type: Container
    Defaults: AwsApiLambdaContainerDefault
    Modules:
    - ConsumerModule
```

## Directive Types

### Schema Directives
Generate data models and repositories:
- **DotNetSchemaProject** - DTOs and model classes
- **DotNetRepoProject** - DynamoDB CRUD operations

```yaml
UserSchema:
  Type: Schema
  Defaults: SchemaDefault
  OpenApiSpecs:
  - openapi.user-schema.yaml
```

### Module Directives  
Generate API controllers:
- **DotNetControllerProject** - REST endpoint implementations

```yaml
UserModule:
  Type: Module
  Defaults: ModuleDefault
  OpenApiSpecs:
  - openapi.user.yaml
```

### Container Directives
Generate Lambda functions:
- **DotNetApiLambdaProject** - Lambda function code 
- **AwsApiLambdaResource** - SAM template resource

```yaml
UserLambda:
  Type: Container
  Defaults: AwsApiLambdaContainerDefault
  Modules:
  - UserModule
```

### Api Directives
Generate API Gateway configurations:
- **AwsHttpApiResource** - HTTP API resource
- **DotNetHttpApiSDKProject** - Client SDK

```yaml
UserApi:
  Type: Api
  Defaults: ApiDefault
  Authentication: JwtAuth
  Containers:
  - UserLambda
```

### Authentication Directives
Generate Cognito user pools:
- **AwsCognitoResource** - User pool configuration

```yaml
JwtAuth:
  Type: Authentication
  Artifacts:
    AwsCognitoResource:
      Template: AWSTemplates/Snippets/sam.service.cognito.jwt.managed.yaml
```

### Service Directives
Generate service-level AWS templates:
- **AwsServiceStackTemplate** - Complete service SAM template
- **DotNetLocalWebApiProject** - Local development web API

```yaml
Service:
  Type: Service
  Apis:
  - UserApi
  - AdminApi
  Artifacts:
    AwsServiceStackTemplate:
```

## Generation Process

### 1. Schema Processing
- Reads OpenAPI specifications
- Extracts entity definitions
- Resolves schema dependencies using topological sorting
- Generates DTOs and repository classes

### 2. Module Processing  
- Processes API path operations
- Merges with aggregate schemas for NSWAG compatibility
- Generates controller implementations
- Determines schema dependencies

### 3. Container Processing
- Creates Lambda function projects
- Configures module hosting
- Generates AWS SAM resources

### 4. Service Assembly
- Combines all components into deployable templates
- Generates client SDKs
- Creates deployment configurations

## Build Commands

```bash
# Build entire solution
dotnet build LazyMagicMDD.sln

# Build specific projects  
dotnet build LazyMagicGenerator --configuration Release
dotnet build LazyMagicCLI --configuration Release

# Run tests
dotnet test LazyMagicGenerator.Test

# Run specific test
dotnet test LazyMagicGenerator.Test --filter "FullyQualifiedName~GenerateOperationIdTests"

# Package CLI tool
dotnet pack LazyMagicCLI -c Release

# Install CLI tool locally  
dotnet tool install --global --add-source ./Packages LazyMagicCLI
```

## Real-World Example

A typical e-commerce application might be structured as:

### Schemas
```yaml
CustomerSchema:
  Type: Schema
  Defaults: SchemaDefault
  OpenApiSpecs:
  - openapi.customer-schema.yaml

ProductSchema:
  Type: Schema  
  Defaults: SchemaDefault
  OpenApiSpecs:
  - openapi.product-schema.yaml
```

### Modules
```yaml
CustomerModule:
  Type: Module
  Defaults: ModuleDefault
  OpenApiSpecs:
  - openapi.customer.yaml

ProductModule:
  Type: Module
  Defaults: ModuleDefault  
  OpenApiSpecs:
  - openapi.product.yaml
```

### Containers
```yaml
CustomerLambda:
  Type: Container
  Defaults: AwsApiLambdaContainerDefault
  Modules:
  - CustomerModule

ProductLambda:
  Type: Container
  Defaults: AwsApiLambdaContainerDefault
  Modules:
  - ProductModule
```

### APIs
```yaml
CustomerApi:
  Type: Api
  Defaults: ApiDefault
  Authentication: CustomerAuth
  Containers:
  - CustomerLambda

ProductApi:
  Type: Api
  Defaults: ApiDefault
  Containers:
  - ProductLambda
```

### Service
```yaml
ECommerceService:
  Type: Service
  Apis:
  - CustomerApi
  - ProductApi
  Artifacts:
    AwsServiceStackTemplate:
```

This configuration generates:
- **CustomerSchema** & **CustomerRepo** projects with DTOs and DynamoDB operations
- **ProductSchema** & **ProductRepo** projects with DTOs and DynamoDB operations  
- **CustomerModule** & **ProductModule** projects with API controllers
- **CustomerLambda** & **ProductLambda** projects hosting the modules
- **CustomerApi** & **ProductApi** client SDKs
- AWS SAM templates for complete infrastructure deployment

## Advanced Features

### Default Inheritance
Directives can inherit from default configurations using the `Defaults` property, enabling consistent patterns across your application while allowing customization where needed.

### Dependency Resolution
The framework automatically resolves dependencies between schemas and ensures proper generation order using topological sorting.

### Multi-Tenancy Support
Built-in support for multi-tenant applications with tenant-specific deployments and configurations.

### Template Customization
All generated code uses customizable templates, allowing you to adapt the output to your specific requirements.

## Version Management

Version is centrally managed in `Version.props`. The GitHub Actions workflow automatically publishes NuGet packages and creates git tags when pushing to the main branch.

## Architecture Benefits

- **Consistency** - All generated code follows the same patterns
- **Maintainability** - Changes to OpenAPI specs automatically propagate
- **Scalability** - Each component can scale independently  
- **Testability** - Clear separation of concerns enables comprehensive testing
- **Deployability** - Complete AWS infrastructure as code
- **Type Safety** - Strong typing throughout the entire stack

LazyMagic MDD transforms the traditional approach to serverless development by generating consistent, maintainable, and scalable applications from declarative specifications, significantly reducing boilerplate code and development time while ensuring architectural consistency.