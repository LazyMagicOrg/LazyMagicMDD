# README

## Release Process
- update version in csproj file
- use project properties / Package to update 
  - release notes
-  https://www.nuget.org/
-  Sign in
-  Click Upload
-  Browse to nupkg/LazyMagicApp.x.x.x.nupkg and click open

## Updating and testing 

You can add the LazyMagicApp/nupkg folder as a dotnet nuget source. This 
allows you to easily update to the latest version from the latest local 
build.

Add Source:
dotnet nuget add source C:\Users\TimothyMay\repos\_Dev\LazyMagicProjects\LazyMagicMDD\LazyMagicApp\nupkg

Check if Source was added:
dotnet nuget list source

Update package:
dotnet tool update -g LazyMagicapp

## NotificationsSvc - Phase 1
Because of time constraints we need to implement NotificationsSvc without too high a priority on Shared Assembly:
NotificationsSvc Components:

NotificationsSvc lambda. Services REST Api Calls. Used for polling and in WebSocket connection
- get '/notification/list/topicId/{topicId}/dateTimeTicks/{dateTimeTicks}': notificationListTopicDateTimeTicks
- post '/notification': addNotification
- post '/notification/subscribe': notificationSubscribe 
- post '/notification/unscubscribe': notificationsUnsubscribe 
Repo code in shared code project. Use namespace alias to resolve schema namespace. 
Notes: 
- These calls can alternatively associated with petStoreApp tag to eliminate need for separate NotificationsSvc lambda.
- Can't reuse the Shared Code ControllerImpl class directly. Need to:
- Provide an INotifications interface
- PetStoreAppControllerImpl must implement calls to class defined in Shared Code 
- For clarity and transparency we would implement NotificationsSvcControllerImpl the same way we would implement PetStoreAppControllerImpl 

NotificationsWebSocket lambda. Services WebSocket Connections.
- Connect: Returns connectionId
Code in Shared Code Project. Inherit lambda class. Use namespace alias to resolve schema namespace.

NotificationsFromStreams lambda. Services DynamoDb Streams.
- Calls WebSocket to send notifications to subscribed clients 
Code in Shared Code Project. use namespace alias to resolve schema manager. 
This one may be complicated. When we receive a stream record, it contains a PetStore entity. 
We need to enhance the DYDBRepository class so we have an interface with 
	public string[] GetTopics(record); This method returns the topics that the recore is associated with.
Doing this will allow the NotificationsFromStream implementation to be reused. 
Also, the behavior of the Notifications will now be defined by the individual repos and not require changes to the NotificationsSvc.

Notification and Subscription records will be in a separate table so we don't get double hits on the stream.

For now we will do queries against Subscription records. Later on we will probably use ElastiCache for Subscription records and maybe even Notification records.
This is both a performance and cost consideration. Doing Subscription record queries each time we process a batch of stream records should only be done for small systems.
However, standing up an ElastiCache instance requires a minimum monthly fee. So, for small, low volume or POC systems we can just do the queries.

PetStore Solution
```
LazyMagic.yaml
  OpenApiSpecs: 
  - NotificationsSvc.yaml 
  AwsTemplates:
  - NotificationsSvcSAM.yaml
```


## Reusable Service Features Musings
LazyMagicMDD 

Problem: It is difficult to create "reusable" APIs and Lambdas
For example I have a Notifications subsystem that I'd like to use in muliple solutions.
Because each solution has a single OpenAPI.yaml specification, NSWAG generates a single ClientSDK project and single Schema project. These projects assume concrete DTO definitions, so there are no Interfaces for the DTO.
If we supported multiple OpenAPI specifications per solution, then we could have modular projects as follows:

PetStore.yaml
	PetStoreClientSDK
	PetStoreSchema 
	PetStoreRepo

Notificaitons.yaml
	NotificationsClientSDK
	NotificationsSchema
	NotificationsRepo

Since Notifications is a reusable module, we can reuse libraries that reference the DTO schemas. 

Implementation
Modify LazyMagicSaaS
OpenApiSpecs: // Allow mulitple OpenApi files (merged). SolutionName.yaml added if not specified and found in solution folder.
- Notification.yaml 


Create solutions that contain a modular library we want to reuse. 
- Each solution needs to have it's own API mapping. However, we don't publish the solution containing the modular library.

The API controller generation is the key element here. 
Controller -- generated
ControllerImpl -- implemented 

When we want to reuse an external library we add a directive to LazyMagicMDD
DoNotGenerateController: // prevent generation of controller class by tag
- notificationsApp 
DoNotGenerateSchema: // prevent generation of schema class by entity name
- Notification 

This will prevent the generation of a Controller and associated Schema.
We will still generate the Lambda! and that lambda will reference the external project containing the:
- NotificationsController
- NotificationsControllerImpl
- NotificationsSchema
- NotificationsRepo 

Lambdas:
We still generate all the lambdas in the Solution so we can perform compile time configuration etc. 
This is generally not a coding or maintenance overhead as the Lambdas are fully generated. 

Note: If you have a reusable Lambda that is not generated you can just create a wrapper lambda where 
you derive a lambda from the reusable Lambda project. Very small one time coding task.

Note: This strategy also allows for possible multi-stack services. There are some questions around how 
the client configuration files would need to be handled but that can be worked out separately. 
- Build time configuration would be coming from muliple sources. MSBuild has some issues regarding collection reuse so we may have to implement a dll task or 
just make sure we have separate ViewModel libraries for each configuration.

### Modular Service Features
We achieve modular shared service features as follows (using Notifications system as example):
- Make Notifications a shared source library in LazyMagic 
	- Controllers 
	- Lambdas 
- Provide a Notifications.yaml -- OpenAPI specification
- Provide a NotificationsSAM.yaml -- SAM Tempalate
- Use OpenApiSpecs directive to load PetStore.yaml and Notifications.yaml
- Use the DoNotGenerateController directive so no NotificationsApp controller will be generated.
- Use the DoNotGenerateLambda directive (when necessary) so no NotificationsApp lambda will be generated. 
- Use the DoNotGenerateSchema directive (when necessary) so no Notification record will be generated. - generally we don't use this
- The Schema and ClientSDK libraries will contain the aggregation of all the "modules".
- Note that the Schema models library contains concrete DTO classes. This is the reason we are using shared code instead of shared assemblies with data entity Interfaces.

The general idea here is reuse the OpenApi, SAM, and supporting C# code across Solutions. 
Our first reusable "module" is Notifications. 
Other candidates include:
- Users 
- ...

### Option to use Shared Assemblies 1
If one really wanted to use shared assemblies, you could do it this way:
- In your module Controller implementation you could make calls to a shared library where you have wrapped the DTO object with an interface and then use that interface in the shared library.
Example:
```
public class SharedNotification : Notification, INotification {}
var sharedNotification = new SharedNotification();
var notification = (Notification)sharedNotification;
var sharedNotification2 = (SharedNotification)notification;
```
In essense, we treat our controller implemenation class as an interface layer to the underlying shared library. 

### Option to use Shared Assemblies
PetStore Solution
LazyMagic.yaml
	OpenApiSpces:
	- NotificationSvc.yaml
	AwsTemplates:
	- NotificationsSvcSAM.yaml
	DoNotGnerateController:
	- NotificationSvc
	DoNotGenerateLambda 
	- NotificationSvc
	DoNotGenerateSchema 
	- Notification 
	DoNotGenerateSDK:
	- notificationsSvc

NotificationsSvc Solution
LazyMagic.yaml
	AwsTemplates:
	- NotificationsSvcSAM.yaml

### UseModule: Directive 
NotificationsSvc Solution
NotificationSvc.yaml
LazyMagic.yaml
NotificationsSvcSAM.yaml 

PetStore Solution
LazyMagic.yaml
	UseModule:
		Path: ../NotificaitonsSvc
		
Idea: Load NotificationsSvc solution and selectively pull items from it.
This is a powerful pattern but probably a substantial amount of work.
Challenges:
- Generating single clientSDK, Schema project in PetStore (how to reconcile Schema namespaces?)
- Reusing Repo projects? Particularily how to manage Schema namespace?
- SAM template bifracation (standalone service versus module)

Stupid Idea:
- Use one namespace for Schema across all solutions. 

### Shared versus Aggregate 
- Shared means using modules referencing another module
	- 
- Aggregate means aggregating multiple modules in a single API
	- API Gateway aggregation 
	- ClientSDK aggregation 
	- Schema aggregation 




