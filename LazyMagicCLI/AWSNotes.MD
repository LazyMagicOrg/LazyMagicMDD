# AWS Notes

- https://docs.aws.amazon.com/sdkfornet/v3/apidocs/

## Getting Settings for AWS Stack 

- https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/CloudFormation/TDescribeStackResourcesResponse.html
- DescribeStackResourceReponse - problematic, fails if stack has too many resources
- Returns a list of StackResource structures 
- https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/CloudFormation/TStackResource.html
  - LogicalResourceId - used
  - PhysicalResourceId - used
  - ResourceType - used
- https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/CloudFormation/MCloudFormationListStackResourcesListStackResourcesRequest.html
- ListStackResourceResponse 
- Returns StackResourceSummaries 
- https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/CloudFormation/TStackResourceSummary.html
  - LogicalResourceId
  - PhysicalResourceId
  - ResourceType 
 

 ## Mapping Notes

OpenAPI Tags -> Lambdas
API - API Gateway Logical Names 
ApiTagMap - maps tags to APIs
MethodMap - maps methods to API 

CloudFrontBaseUrl 
CloudFrontApiUrl - maps API to Behavior Path - can we read this from AWS?

## Stack Models 

StackConfig:
Account: AwsAccount
Region: AwsRegion
Auth[]:
  Cognito:
  - IdentityPoolId
  - UserPoolId
  - UserPoolClientId
	
Stacks[]
	- Name: example: Local, LocalAndriod, CloudFront, WebApi
	- Auth: Cognito
	- Resources[]
		- LogicalApiName
		- Type: Api, S3, ...
		- URL: 
		- PhysicalId
	- Profile: required for direct access to cloud resources
	- Tenant: optinal for multi-tenant 

Example:
{
  "Profile":"petstore-dev",
  "Account":"99999999",
  "Region":"us-west-2",
  "Authenticators": {
    "Cognito": {
      "IdentityPoolId":"",
      "UserPoolId":"",
	  "UserPoolClientId":""
	}
  },
  "DefaultServices":"WebApi",
  "Services:" {
	"AWS": {
      "Auth":"Cognito",
	  "Resources": {
		"HttpApiSecure": {
		  "ResourceType":"ApiGateway",
		  "Url":"https://8x9saosiw.execute-api.us-west-2.amazonaws.com/Dev",
		  "Id":""
	    }
	  }
	},
    "WebApi": {
      "Auth":"Cognito",
      "Resources:" {
	    "HttpApiSecure": {
	      "ResourceType":"Local",
	      "Url":"https://localhost:5001",
	      "Id":""
	    }
      }
	},
    "WebApiAndriod": {
      "Auth":"Cognito",
      "Resources": {
	    "HttpApiSecure": {
	      "ResourceType":"Local",
	      "Url":"https://10.0.2.2:5011",
	      "Id":""
	    }
      }
	},
	"CloudFront": {
      "Auth":"Cognito",
      "Resources": {
	    "Default": {
	      "ResourceType":"CloudFront",
          "Url":"https://mydomain.com/HttpApiSecure/Dev",
	      "Id":""
	    } 
      }
    }
  }
}












