# LazyMagic Model-Driven Development (MDD)

LazyMagic MDD is a powerful code generation framework that transforms OpenAPI specifications and YAML configuration into complete AWS serverless applications with .NET implementations. It follows a model-driven development approach where your API specifications drive the generation of schemas, controllers, Lambda functions, AWS infrastructure, and client SDKs.

## Overview

LazyMagic MDD generates complete serverless applications from:
- **OpenAPI Specifications** - Define your APIs, schemas, and operations
- **YAML Configuration** - Specify what components to generate and how they connect
- **Templates** - Customize the generated code structure

The framework produces ready-to-deploy AWS applications with:
- ASP.NET Core containerized applications running on AWS App Runner
- AWS SAM templates
- DynamoDB repositories
- Strongly-typed client SDKs
- Cognito authentication and authorization
- AppSync Events for real-time notifications

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
3. **Container** → ASP.NET Core applications running in AWS App Runner
4. **Api** → API configurations (reserved for future ALB + Fargate support)
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
- ASP.NET Core containerized applications
- Dockerfiles for container builds
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
Generate ASP.NET Core applications for AWS App Runner:
- **AspDotNetProject** - ASP.NET Core application with Dockerfile
- **AwsAppRunnerResource** - SAM template resource for App Runner service

```yaml
UserContainer:
  Type: Container
  Defaults: AwsAppRunnerContainerDefault
  Modules:
  - UserModule
  Artifacts:
    AspDotNetProject:
      NameSuffix: "Service"
    AwsAppRunnerResource:
      NameSuffix: "Service"
      Cpu: 1024        # 1 vCPU
      Memory: 2048     # 2 GB
      Port: 8080
      ManagedPolicyArns:
      - "arn:aws:iam::aws:policy/AmazonDynamoDBFullAccess"
```

### Api Directives
Reserved for future AWS ALB + Fargate support:
- **AwsApiAppRunnerResource** - Placeholder for future API configurations
- **AspDotNetApiSDKProject** - Client SDK

Currently, App Runner services handle their own routing and HTTPS endpoints without requiring separate API Gateway resources.

```yaml
UserApi:
  Type: Api
  Defaults: ApiDefault
  Authentication: JwtAuth
  Containers:
  - UserContainer
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
- Creates ASP.NET Core application projects
- Configures module hosting with dependency injection
- Generates Dockerfiles for containerization
- Generates AWS App Runner SAM resources

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
CustomerContainer:
  Type: Container
  Defaults: AwsAppRunnerContainerDefault
  Modules:
  - CustomerModule
  Artifacts:
    AspDotNetProject:
      NameSuffix: "Service"
    AwsAppRunnerResource:
      NameSuffix: "Service"

ProductContainer:
  Type: Container
  Defaults: AwsAppRunnerContainerDefault
  Modules:
  - ProductModule
  Artifacts:
    AspDotNetProject:
      NameSuffix: "Service"
    AwsAppRunnerResource:
      NameSuffix: "Service"
```

### APIs
```yaml
CustomerApi:
  Type: Api
  Defaults: ApiDefault
  Authentication: CustomerAuth
  Containers:
  - CustomerContainer

ProductApi:
  Type: Api
  Defaults: ApiDefault
  Containers:
  - ProductContainer
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
- **CustomerContainerService** & **ProductContainerService** ASP.NET Core projects with Dockerfiles
- **CustomerApi** & **ProductApi** client SDKs
- AWS SAM templates with App Runner service definitions for complete infrastructure deployment

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
- **Scalability** - App Runner handles auto-scaling automatically
- **Testability** - Clear separation of concerns enables comprehensive testing
- **Deployability** - Complete AWS infrastructure as code with SAM
- **Type Safety** - Strong typing throughout the entire stack
- **Simplicity** - Container-based deployment is easier than Lambda + API Gateway
- **Local Development** - ASP.NET Core applications run locally without special tooling

## Architectural Shift: Lambda to App Runner

LazyMagic v3.x moved from AWS Lambda + API Gateway to AWS App Runner for several key reasons:

### Benefits of App Runner
1. **Simpler Architecture** - App Runner services are self-contained with built-in HTTPS endpoints
2. **Standard ASP.NET** - Use familiar ASP.NET Core patterns without Lambda-specific code
3. **Better Local Development** - Test containerized applications locally exactly as they'll run in production
4. **Easier Debugging** - Standard debugging tools work without Lambda emulation
5. **Auto-scaling** - Built-in scaling without configuring API Gateway throttling
6. **Cost Efficiency** - Pay for actual usage without API Gateway per-request charges

### What Changed
- **Container Artifact**: `AspDotNetProject` replaces Lambda-specific projects
- **AWS Resource**: `AwsAppRunnerResource` replaces Lambda + API Gateway resources
- **Deployment**: Docker images pushed to ECR instead of Lambda ZIP files
- **Routing**: App Runner handles routing natively instead of API Gateway integration

### Future Plans
The Api directive is reserved for implementing AWS Application Load Balancer + Fargate for scenarios requiring:
- More granular control over networking
- VPC-internal APIs
- Advanced load balancing features
- Higher request volumes

LazyMagic MDD transforms cloud application development by generating consistent, maintainable, and scalable applications from declarative specifications, significantly reducing boilerplate code and development time while ensuring architectural consistency.