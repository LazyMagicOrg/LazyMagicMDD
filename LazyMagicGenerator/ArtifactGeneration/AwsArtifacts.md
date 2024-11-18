# AWS Artifacts

| Directive |  Artifact | Output | Imports | Description | Snippets |
| --- | --- | --- | --- | --- | --- |
|Api|AwsHttpApiResource|export|DotNetLambda|AWS\::Serverless::HttpApi|sam.service.httpapi.cognito.yaml<br>sam.service.httpapi.definitionbody.yaml<br>sam.service.httpapi.path.yaml|
|Api|AwsWebSocketApiResource|export|DotNetLambda|AWS\::ApiGatewayV2::Api |sam.service.messaging.websocketapi.yaml|
|Authentication|AwsCognitoResource|export||AWS\::Cognito::UserPool|sam.service.cognito.jwt.managed.yaml<br>sam.service.cognito.jwt.unmanaged.yaml|
|Container|AwsLambdaResource|export||AWS\::Serverless::Function|sam.service.lambda.yaml|
|Container|AwsMessagingLambdaResouce|export||AWS\::Serverless::Function|sam.service.messaging.sqslambda.yaml|
|Container|AwsWebsocketLambdaResource|export||AWS\::Serverless::Function|sam.service.messaging.websocketlambda.yaml|
|Queue|AwsSQSResource|export||AWS\::SQS::Queue|sam.service.messaging.sqs.yaml|
|Service|AwsServiceLambdaResource|string|AwsLambdaResources[]<br>AwsHttpApiResource[]|#LzLambdas# section|sam.service.lambda.permission.yaml|
|Service|AwsServiceStackTemplate|sam.service.g.yaml||template|sam.service.yaml<br>sam.service.lambdas.yaml<br>sam.service.cloudfront.configfunction.yaml|
|Tenancy|AwsTenancyStackTemplate|sam.tenant.g.yaml|||sam.tenant.yaml<br>sam.tenant.cloudfront.yaml<br>sam.tenant.cloudfront.webapp.yaml<br>sam.tenant.cloudfront.apiorigin.yaml<br>sam.tenant.cloudfront.ws.yaml<br>sam.tenant.cloudfront.wsorigin.yaml<br>sam.tenant.cloudfront.webapporigin.yaml<br>sam.tenant.cloudfront.landingpage.yaml|