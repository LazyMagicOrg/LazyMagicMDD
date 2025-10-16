# DotNet Project Artifacts

This document describes the DotNet project artifacts that are generated and the dependencies among them.




| Directive | Highlighted Properties | Artifact | Exports | Imports |
| --- | --- | --- | --- | --- |
|Schema|OpenApiSpecs[]|DotNetSchemaProject|ExportedProjectPath,<br> ExportedProjectUsings,<br> ExportedOpenApiSpecs||
|Schema|OpenApiSpecs[]|DotNetRepoProject|ExportedProjectReferences,<br> ExportedProjectUsings,<br> ExportedInterfaces,<br> ExportedServiceRegistrations||
|Module|OpenApiSpecs[],<br>Schemas[]|DotNetControllerProject|ExportedProjectPath,<br> ExportedProjectUsings,<br> ExportedGlobalUsings,<br> ExportedOpenApiSpecs|DotNetSchemaProject.ExportedProjectPath,<br> DotNetSchemaProject.ExportedProjectUsing,<br> DotNetSchemaProject.ExportedOpenApiSpecs,<br> DotNetRepoProject.ExportedProjectPath,<br> DotNetRepoProject.ExportedProjectUsings,<br> DotNetRepoProject.ExportedInterfaces,<br> DotNetRepoProject.ExportedServiceRegistrations|
|Container|Modules[]|AspDotNetProject|ExportedProjectUsings,<br> ExportedOpenApiSpecs,<br> ExportedImageUri,<br> ExportedDockerfilePath,<br> ExportedApiPrefix|DotNetControllerProject.ExportedProjectPath,<br> DotNetControllerProject.ExportedGlobalUsing,<br> DotNetControllerProject.ExportedServiceRegistrations|
|Api|Containers[]|AspDotNetApiSDKProject||DotNetSchemaProject.ExportedProjectPath,<br> DotNetSchemaProject.ExportedProjectUsings,<br> DotNetSchemaProject.ExportedOpenApiSpecs,<br> DotNetControllerProject.ExportedProjectPath,<br> DotNetControllerProject.ExportedGlobalUsings,<br> DotNetControllerProject.ExportedOpenApiSpecs|

## Architecture Changes

### Current Architecture (v3.x)
LazyMagic now uses **AWS App Runner** with **ASP.NET Core** containers instead of AWS Lambda + API Gateway:

- **Container Artifact**: `AspDotNetProject` - Generates ASP.NET Core web applications that run in App Runner
- **API Artifact**: `AspDotNetApiSDKProject` - Generates strongly-typed client SDKs for the APIs
- **AWS Resource**: `AwsAppRunnerResource` - Generates CloudFormation resources for App Runner services

### Removed Artifacts (no longer supported)
- `DotNetLambdaProject` - Replaced by AspDotNetProject
- `DotNetHttpApiLambdaProject` - Replaced by AspDotNetProject
- `DotNetWSApiLambdaProject` - Replaced by AspDotNetProject
- `DotNetAppRunnerProject` - Replaced by AspDotNetProject
- `AwsApiLambdaResource` - Replaced by AwsAppRunnerResource
- `AwsHttpApiResource` - No longer needed (App Runner provides endpoints)
- `AwsWSApiResource` - No longer needed (App Runner provides endpoints)

## Container Artifact: AspDotNetProject

Generates an ASP.NET Core application that:
- Hosts one or more Module controllers
- Runs in an App Runner container
- Listens on port 8080
- Uses standard ASP.NET middleware for routing, authentication, etc.
- Includes a generated Dockerfile for building the container image
- Merges OpenAPI specs from all modules into a single `openapi.g.yaml`

Exports:
- `ExportedImageUri`: ECR image URI for the container
- `ExportedDockerfilePath`: Path to the generated Dockerfile
- `ExportedApiPrefix`: API prefix for routing (e.g., `/api`)
- `ExportedOpenApiSpec`: Path to merged OpenAPI specification

## Notes - current restrictions (should be checked in validators)
- The Schema directive may only contain one DotNetSchemaProject artifact
- The Schema directive may only contain one DotNetRepoProject artifact
- The Module directive may only contain one DotNetControllerProject artifact
- The Container directive may only contain one AspDotNetProject artifact
- The Api directive may only contain one AspDotNetApiSDKProject artifact

It is preferable to create multiple directives instead of multiple artifacts in a single directive.

