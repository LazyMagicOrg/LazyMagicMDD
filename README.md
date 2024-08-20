# LazyMagicMDD 
This solution generates two LazyMagic Generator projects:
- LazyMagicApp - dotnet command line tool
- LazyMagicExt - Visual Studio extension 

## Version 0.9.0
Added Module Support 

## Generator
The LazyMagic Generator performs the following tasks:
- Reads the LazyMagic directives file
- Reads the OpenAPI spec files
- Generates Controller projects 
- Generates Lambda projects 
- Generates ClientSDK projects
- Generates Schema project 
- Reads the SAM template files
- Generates the AWSTeamplates/sam.service.g.yaml SAM file

### Module Support 
The goal is to make it easier to create and manage modules in a LazyMagic application. OpenAPI doesn't natively support modules, so we introduce conventions and transformations to support reusable modules. 

Previous versions of the MDD used tags in the OpenAPI routes to assign routes to a Lambda and generate a controller for the lambda. This approach worked well for simple applications, but it didn't scale well for larger systems with many applications and shared code/apis. 

The new approach makes two substantive changes to the LazyMagic Generator:
- Tags now generate modules (controllers), not lambdas.
- The ApiTagMap diretive is replaced by the Containers directive, and Service directive.

This example is somewhat contrived but demonstrates the flexibility of the new approach:

```
Containers: # containers execute code in a service
- Name: LambdaFunction1: # name of the Lambda function resource
  Type: Function # types include Function, Cluster, ...
  Modules:
  - module1 # tag in OpenAPI spec serviced by LambdaFunction1
  - module2 # tag in OpenAPI spec serviced by LambdaFunction1
- Name: LambdaFunction2: # name of the Lambda function resource
  Type: Function
  Modules:
  - module1 # tag in OpenAPI spec serviced by LambdaFunction2
  - module3 # tag in OpenAPI spec serviced by LambdaFunction2
- Name:LambdaFunction3: # name of the Lambda function resource
  Type: Function
  Modules:
  - module1 # tag in OpenAPI spec serviced by LambdaFunction3
  - module2 # tag in OpenAPI spec serviced by LambdaFunction3
  - module3 # tag in OpenAPI spec serviced by LambdaFunction3

Service:
  Api1: # name of the API Gateway resource
  - LambdaFunction1
  Api2: # name of the API Gateway resource
  - LambdaFunction1
  - LambdaFunction2
  Api3: # name of the API Gateway resource
  - LambdaFunction1
  - LambdaFunction3
  Api4: # name of the API Gateway resource
  - LambdaFunction1
  - LambdaFunction2
  - LambdaFunction3
``` 

- Modules are identified by tags.
- We can set a default tag by openapi spec file.
- We generate a controller project for each module.
- OperationId is prefixed with the module name.

### ClientSDK
We currently generate a single ClientSDK for all lambdas. We will now generate a separate ClientSDK for each lambda.

### OperationId Module Name prefix
Each method in the controller is named by the OperationId in the OpenAPI spec. Since we generate a ClientSDK that also uses these method names, we need to ensure they are unique acrosss the whole of the ClientSDK class. We can't guarantee uniqueness across all modules, so we prefix the OperationId with the module name. This also has the benefit that the ClientSDK groups the module methods together and makes it easier what each available module provides.

We also aggregate all modles (controllers) in the WebApi project. This means that each OperationId needs to be unqiue across all modules in the system.

### Routes 


The existing ProjectOptions Lambda funtion reference remain unchanged. For example:
```
ProjectOptions:
  ...
  LambdaFunction1:
    ProjectReferences:
	  - ..\..\Controllers\Module1\Module1.csproj
	  - ..\..\Controllers\Module2\Module2.csproj
  LambdaFunction2:
    ProjectReferences:
	  - ..\..\Controllers\Module1\Module1.csproj
	  - ..\..\Controllers\Module2\Module3.csproj
  LambdaFunction3:
	ProjectReferences:
	  - ..\..\Controllers\Module1\Module1.csproj
	  - ..\..\Controllers\Module2\Module2.csproj
	  - ..\..\Controllers\Module2\Module3.csproj
```

The LazyMagic Generator generates the controllers and lambda function projects. It also generates separate ClientSDK packages for each lambda. A single Schema package is generated that contains the shared DTO models for all routes. As an optimization, we may later generate separate Schema packages for each lambda that only contains the models used by that lambda. 

The LazyMagic Generator also generates the sam.service.g.yaml SAM template file. This accomplished by reading the sam.services.yaml file and inserting a DefinitionBody: section under each API gateway. For example:
```
Resources:
  HttpApi1:
    Type: AWS::Serverless::HttpApi
    Properties:
      Description: Api1
      ...
      DefinitionBody:
        openapi: 3.0.1
        paths:
          /api1/address:
            post:
              x-amazon-apigateway-integration:
                httpMethod: POST
                type: aws_proxy
                uri:
                  Fn::Sub: arn:${AWS::Partition}:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${LambdaFunction1.Arn}/invocations
                payloadFormatVersion: 2.0
```

## Routes
Routes in the OpenAPI spec are generally defined using route parameters to allow for reuse in different lambdas. For example:
```
openapi: 3.0.1
info:
  title: Gallery API
  version: 1.0.0
paths:
  /{anyPath}/address:
    post:
    ...
```

ChatGPT claims NSWAG supports route parameters in the OpenAPI spec. The LazyMagic Generator uses the route parameters to generate the controller and client SDK. The client SDK uses the route parameters to generate the client method signature. 

When we generate the routes for the lambda, we can drop the route parameters or replace them with a lambda prefix value. For example:
```
Resources:
  HttpApi1:
    Type: AWS::Serverless::HttpApi
    Properties:
      Description: Api1
      ...
      DefinitionBody:
        openapi: 3.0.1
        paths:
          /api1/address:
            post:
              x-amazon-apigateway-integration:
                httpMethod: POST
                type: aws_proxy
                uri:
                  Fn::Sub: arn:${AWS::Partition}:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${LambdaFunction1.Arn}/invocations
                payloadFormatVersion: 2.0
```

## Controllers 
The controller is generated from the OpenAPI spec. The controller is a partial class that implements the controller methods. The controller methods are generated from the OpenAPI spec. The controller methods call the lambda function. The controller methods are generated using the route parameters.

## Current Implementation 
```
Execute()
    ProcessOpenApiAsync()
        LoadLazyMagicDirectivesAsync()
            ParseLzConfigurationAsync()
        AssignDefaultTag()
        LoadSAMAsync()
            load each specified SAM template
            add AwsResource objects 
        ParseOpenApiTagsObjectForLambdaNamesAsync()
            parse tags for lambda names
            add Lambdas objects
            add LambdaNameByTagName objects
            add TagNameByLambdaName objects 
            add ApiNameByTagName objects
        ParseApiTagMapAsync()
            update ApiNameByTagName objects 
        ParseOpenApiTagsObjectAsuync()
            create Lambda AwsResources
        ParseAwsResourcesForLambdasAsync()
            updates AwsResource 
            updates Apis
        DiscoverSecurityLevel()
    WriteSamAsync()
    ProcessProjects.ProcessAsync()
        ProcessClientSDKProject.ProcessAsync() 
        ProcessRepoProject.ProcessAsync()
        ProcessLambdaProject.ProcessAsync()
        ProcessControllerProject.ProcessAsync()
```

The current model uses tags to generate both the Lambda and Controller.

## New Implementation 

The new model uses the tag to generate the controller. 

We introduce a new directive, ApiLambdas, to map the API Gateway to the Lambda.