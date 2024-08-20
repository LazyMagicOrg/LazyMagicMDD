# AWS Artifact Notes
LazyMagic generates AWS CloudFormation template artifacts. These templates can be used to create AWS resources using the AWS CloudFormation service. The templates are generated from the LazyMagic directives.

## Service : AwsServiceStackTemplate
Generate an AWS CloudFormation stack template for a Service directive
Resources: API Gateways, Cognito UserPools, Lambdas
Parameters: ArtifactsBucketParameter, StageParameter, SystemGuidParameter
Outputs: 
- for each api
	- {{api}Id
- for each auth
	- {{auth}UserPoolName
	- {{auth}UserPoolId
	- {{auth}UserPoolClientId
	- {{auth}IdentityPooldId

## Tenancy : AwsTenantStackTemplate
Generate an AWS CloudFormation stack template for a Tenancy directive
Resources: S3Buckets, DynamoDB table, ResponseHeadersPolicy, CloudFrontDistribution 
Parameters: 
- CreateBucketsParameter // true if the tenancy buckets should be created
- CreateTableParameter // true if the tenancy table should be created
- GuidParameter
- TenantKeyParameter
- OriginAccessIdentityParameter
- NotificationsWebSocketApiIdParameter
- EnvironmentParameter
- SubDomainParameter
- RootDomainParameter
- AcmCertificateArnParameter
- OriginRequestPolicyIdParameter
- CachePolicyIdParameter
- for each webapp used in the tenancy
	- {{webapp}BucketNameParameter
- for each api used in the tenancy
	- {{api}IdParameter
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

