# DotNet Project Artifacts

This document describes the DotNet project artifacts that are generated and the dependencies among them.




| Directive | Highlighted Properties | Artifact | Exports | Imports |
| --- | --- | --- | --- | --- |
|Schema|OpenApiSpecs[],<br>|DotNetSchemaProject|ExportedProjectPath,<br> ExportedProjectUsings,<br> ExportedOpenApiSpecs||
|Schema|OpenApiSpecs[],<br>|DotNetRepoProject|ExportedProjectReferences,<br> ExportedProjectUsings,<br> ExportedInterfaces,<br> ExportedServiceRegistrations||
|Module|OpenApiSpecs[],<br>Schemas[]|DotNetControllerProject|ExportedProjectPath,<br> ExportedProjectUsings,<br> ExportedGlobalUsings,<br> ExportedOpenApiSpecs|DotNetSchemaProject.ExportedProjectPath,<br> DotNetSchemaProject.ExportedProjectUsing,<br> DotNetSchemaProject.ExportedOpenApiSpecs,<br> DotNetRepoProject.ExportedProjectPath,<br> DotNetRepoProject.ExportedProjectUsings,<br> DotNetRepoProject.ExportedInterfaces,<br> DotNetRepoProject.ExportedServiceRegistrations|
|Container|Modules[]|DotNetLambdaProject|ExportedProjectUsings,<br> ExportedOpenApiSpecs|DotNetControllerProject.ExportedProjectPath,<br> DotNetControllerProject.ExportedGlobalUsing,<br> DotNetControllerProject.ExportedServiceRegistrations|
|Container|Modules[]|DotNetWebApiProject|||
|Api|Containers[]|DotNetClientSDKProject||DotNetSchemaProject.ExportedProjectPath,<br> DotNetSchemaProject.ExportedProjectUsings,<br> DotNetSchemaProject.ExportedOpenApiSpecs,<br> DotNetControllerProject.ExportedProjectPath,<br> DotNetControllerProject.ExportedGlobalUsings,<br> DotNetControllerProject.ExportedOpenApiSpecs	|

## Notes - current restrictions (should be checked in validators)
- The Schema directive may only containone DotNetSchemaProject artifact
- The Schema directive may only contain one DotNetRepoProject artifact
- The Module directive may only contain one DotNetControllerProject artifact
- The Container directive may only contain one DotNetLambdaProject artifact
- The Container directive may only contain one DotNetWebApiProject artifact
- The Api directive may only contain one DotNetClientSDKProject artifact

It is preferable to create multiple directives instead of multiple artifacts in a single directive.

