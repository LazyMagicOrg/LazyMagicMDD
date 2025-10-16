# AWS Artifact Notes
LazyMagic generates AWS CloudFormation template artifacts. These templates can be used to create AWS resources using the AWS CloudFormation service. The templates are generated from the LazyMagic directives.

## Architecture Overview
LazyMagic has transitioned from AWS Lambda + API Gateway to AWS App Runner for container-based API deployments. This provides:
- Simpler deployment model with containerized ASP.NET applications
- Built-in load balancing and auto-scaling
- Native HTTPS endpoints without separate API Gateway configuration
- Easier local development and testing

## Service : AwsServiceStackTemplate
Generate an AWS CloudFormation stack template for a Service directive
Resources: AWS App Runner Services, Cognito UserPools, SQS Queues, AppSync Event APIs
Parameters: SystemKeyParameter, SystemSuffixParameter, EnvironmentParameter, ArtifactsBucketParameter
Outputs:
- for each App Runner service
	- {{apprunner}ServiceUrl
- for each auth
	- {{auth}UserPoolName
	- {{auth}UserPoolId
	- {{auth}UserPoolClientId
	- {{auth}IdentityPooldId

## Container : AwsAppRunnerResource
Generate an AWS App Runner service resource for a Container directive
Resources: AWS::AppRunner::Service with ECR image source
Configuration:
- CPU: 1024 (1 vCPU) - configurable
- Memory: 2048 MB (2 GB) - configurable
- Port: 8080 - the container port for the ASP.NET application
- Runtime: dotnet8
- Image: ECR image URI in format: {AccountId}.dkr.ecr.{Region}.amazonaws.com/{SystemKey}-{SystemSuffix}-{Environment}-{ContainerName}:latest
- Managed Policy ARNs: Configurable list of IAM policies for the service
- Authentication: Optional Cognito integration
- EventsApi: Optional AppSync Events API integration

The generator also creates a Dockerfile for building the container image from the ASP.NET project.

## Api : AwsApiAppRunnerResource
This artifact is kept for future use when implementing AWS ALB + Fargate for scalable deployments.
Currently, it simply creates output values for the API resource name. Since App Runner services handle their own routing and HTTPS endpoints, no additional API Gateway resources are needed.

## Tenancy : AwsTenantStackTemplate
Generate an AWS CloudFormation stack template for a Tenancy directive
Resources: S3Buckets, DynamoDB table, ResponseHeadersPolicy, CloudFrontDistribution
Parameters:
- CreateBucketsParameter // true if the tenancy buckets should be created
- CreateTableParameter // true if the tenancy table should be created
- GuidParameter
- TenantKeyParameter
- OriginAccessIdentityParameter
- EnvironmentParameter
- SubDomainParameter
- RootDomainParameter
- AcmCertificateArnParameter
- OriginRequestPolicyIdParameter
- CachePolicyIdParameter
- for each webapp used in the tenancy
	- {{webapp}BucketNameParameter
- for each App Runner service used in the tenancy
	- {{apprunner}ServiceUrlParameter
- for each auth used in the tenancy
	- {{auth}UserPoolNameParameter
	- {{auth}UserPoolIdParameter
	- {{auth}UserPoolClientIdParameter
	- {{auth}IdentityPooldIdParameter

### GuidParameter 
The GuidParameter allows for is a unique resource identifiers in the tenancy. It is used to create unique names for the resources in the tenancy. The GuidParameter is a required parameter for the tenancy stack template.

By convention, the tenancy assets are identified with the asset type,  tenancykey name, and SystemGuid appended. However, because S3 bucket names and CloudFront function names must be globally unique, the guid can be set to anything you like. Generally, unless someone is maliciously trying to create a assets with the same name, using tey SystemGuid will result in unique resource names. If you run into a resource name conflict, pass in your own unique guid instead of the SystemGuid.

## WebApp Buckets
The AWSTemplates/Templates/WebAppBucket.yaml template is used to create an S3 bucket for a webapp.
Parameters: WebAppNameParameter, GuidParameter, OriginAccessIdentityParameter

The bucket name is $"{WebAppNameParameter}-{GuidParameter}".

By convention, the SystemGuid is passed as GuidParameter. However, because S3 bucket names must be globally unique, the bucket name can be anything you like. Generally, unless someone is maliciously trying to create a bucket with the same name, passing SystemGuid will result in a unique bucket name.

