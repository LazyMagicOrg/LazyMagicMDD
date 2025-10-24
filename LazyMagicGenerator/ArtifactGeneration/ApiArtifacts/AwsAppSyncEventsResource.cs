using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using static LazyMagic.LzLogger;
using static LazyMagic.OpenApiUtils;

namespace LazyMagic
{
    /// <summary>
    /// Generate a AWS::AppSync::EventAPI resource suitable for
    /// inclusion in an AWS SAM template. The output is in yaml
    /// format and available in the ExportedAwsResourceDefinition string property.
    /// Note: This is for AppSync Events (not GraphQL).
    /// </summary>
    public class AwsAppSyncEventsResource : AwsResourceArtifact, IAwsApiResource
    {
        public override string Template { get; set; } = "AWSTemplates/Snippets/sam.service.appsync-events.yaml";
        public string ExportedAwsResourceName { get; set; } = null;
        public string ExportedAwsResourceDefinition { get; set; } = null;
        public string ExportedPath { get; set; } = null;
        public string ExportedPrefix { get; set; } = null;
        public string ExportedResourceType { get; set; } = null;

        // AppSync Events specific configuration
        public string EventSourceName { get; set; } = "SessionEvents";
        public string NamespacePrefix { get; set; } = "session";

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var resourceName = "";
            try
            {
                await Task.Delay(0);
                Api directive = (Api)directiveArg;
                var apiPrefix = directive.Key;
                // Set the service name
                resourceName = directive.Key;
                resourceName += NameSuffix ?? "";
                Info($"Generating {directive.Key} {resourceName}");

                var template = Template;

                if(directive.Authenticators.Count == 0)
                {
                    throw new Exception($"AppSync Events API {directive.Key} must specify at least one authenticator.");
                }   
                if(directive.Authenticators.Count > 1)
                {
                    throw new Exception($"AppSync Events API {directive.Key} currently supports only one authenticator.");
                }

                var cognitoResource = directive.Authenticators.FirstOrDefault();

                // Read template and generate
                var templateBuilder = new StringBuilder();
                templateBuilder
                    .Append(File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template)));

                templateBuilder.Replace("__ResourceGenerator__", this.GetType().Name);
                templateBuilder.Replace("__AppSyncEventsApiName__", resourceName);
                templateBuilder.Replace("__EventSourceName__", EventSourceName);
                templateBuilder.Replace("__NamespacePrefix__", NamespacePrefix);

                // Handle Cognito authentication if specified
                var cognitoAuthProvider = "";
                var cognitoConnectionAuth = "";
                var cognitoPublishAuth = "";
                var cognitoSubscribeAuth = "";

                if (!string.IsNullOrEmpty(cognitoResource))
                {
                    // Add Cognito as an auth provider
                    cognitoAuthProvider = $@"
          - AuthType: AMAZON_COGNITO_USER_POOLS
            CognitoConfig:
              AwsRegion: !Ref AWS::Region
              UserPoolId: !Ref {cognitoResource}UserPoolIdParameter";

                    // Add Cognito to allowed auth modes
                    cognitoConnectionAuth = @"
          - AuthType: AMAZON_COGNITO_USER_POOLS";
                    cognitoPublishAuth = @"
          - AuthType: AMAZON_COGNITO_USER_POOLS";
                    cognitoSubscribeAuth = @"
          - AuthType: AMAZON_COGNITO_USER_POOLS";
                }

                templateBuilder.Replace("#CognitoAuth#", cognitoAuthProvider);
                templateBuilder.Replace("#CognitoConnectionAuth#", cognitoConnectionAuth);
                templateBuilder.Replace("#CognitoPublishAuth#", cognitoPublishAuth);
                templateBuilder.Replace("#CognitoSubscribeAuth#", cognitoSubscribeAuth);

                // Get App Runner project artifacts for channel definitions
                var appRunnerArtifacts = solution.Directives.GetArtifactsByType<AspDotNetProject>(directive.Containers);

                // Generate event channel configurations based on connected containers
                var channelConfig = new StringBuilder();
                foreach (var appRunnerArtifact in appRunnerArtifacts)
                {
                    var appRunnerProject = (AspDotNetProject)appRunnerArtifact;
                    if (string.IsNullOrEmpty(appRunnerProject.ExportedName)) continue;

                    // Add channel configuration for this container
                    channelConfig.AppendLine($"        # Channel for {appRunnerProject.ExportedName}");
                    channelConfig.AppendLine($"        {NamespacePrefix}.{appRunnerProject.ExportedName.ToLower()}:");
                    channelConfig.AppendLine($"          description: Event channel for {appRunnerProject.ExportedName} sessions");
                }

                templateBuilder.Replace("__ChannelConfiguration__", channelConfig.ToString());

                //Exports
                ExportedAwsResourceName = resourceName;
                ExportedAwsResourceDefinition = templateBuilder.ToString();
                ExportedPrefix = apiPrefix;
                ExportedResourceType = "AppSyncEvents";

                StackOutputs.Add($@"
  {ExportedAwsResourceName}Auth:
    Value: {cognitoResource}
");
                StackOutputs.Add($@"
  {ExportedAwsResourceName}Domain:
    Value: !GetAtt {ExportedAwsResourceName}.Dns.Http
");

            StackOutputs.Add($@"
  {ExportedAwsResourceName}ApiKey:
    Value: !GetAtt {ExportedAwsResourceName}ApiKey.ApiKey
");
        

        }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsAppSyncEventsResource)}: {resourceName}, {ex.Message}");
            }
        }
    }
}