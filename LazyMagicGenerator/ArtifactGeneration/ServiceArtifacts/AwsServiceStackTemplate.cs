using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.AwsServiceLambdasResources;
using static LazyMagic.AwsSQSResources;
using static LazyMagic.ArtifactUtils;
using System.Text;
using CommandLine;

namespace LazyMagic
{
    /// <summary>
    /// Generate the service stack template. The resources 
    /// defined in this stack include:
    /// - AWS::Serverless::Function
    /// - AWS::HttpApi::Api
    /// - AWS::Cognito::UserPool    
    /// - AWS::Cognito::UserPoolClient
    /// </summary>
    public class AwsServiceStackTemplate : ArtifactBase
    {
        public string LambdasTemplate { get; set; } = null;
        public string LambdaPermissionsTemplate { get; set; } = null;
        
        // Exports
        public string ExportedStackTemplatePath { get; set; } = null;

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var templateName = $"sam.{directiveArg.Key}.g.yaml";
            try
            {
                Service directive = (Service)directiveArg;

                // Set the service name
               
                await InfoAsync($"Generating {directive.Key} {templateName}");
                var systemTemplatePath = Template ?? "AWSTemplates/Snippets/sam.service.yaml";
                var tenantCloudFrontConfigFunctionSnippet = File.ReadAllText(
                    Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates/Snippets/sam.service.cloudfront.configfunction.yaml"));

                var ouptutsBuilder = new StringBuilder();

                var templateBuilder = new StringBuilder(); 
                templateBuilder.Append(File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, systemTemplatePath)));

                /* PARAMETERS */
                var lzParametersBuilder = new StringBuilder();
                lzParametersBuilder.AppendLine("#LzParameters start");
                lzParametersBuilder.AppendLine("# none configured");
                lzParametersBuilder.AppendLine("#LzParameters end");
                templateBuilder.Replace("#LzParameters#", lzParametersBuilder.ToString());

                /* LAMBDAS */
                var lzLambdasBuilder = new StringBuilder();
                lzLambdasBuilder.AppendLine("#LzLambdas start");
                lzLambdasBuilder.Append(GenerateLambdaResources(solution, directive));
                lzLambdasBuilder.AppendLine("#LzLambdas end");
                templateBuilder.Replace("#LzLambdas#",lzLambdasBuilder.ToString());

                /* HTTP APIs */
                var apiTemplateBuilder = new StringBuilder();
                apiTemplateBuilder.AppendLine("#LzApis start");
                var apiResources = GetAwsApiResources(solution, directive);
                foreach (var httpApiResource in apiResources)
                {
                    apiTemplateBuilder.Append(httpApiResource.ExportedAwsResourceDefinition);
                    ouptutsBuilder.Append($@"
  {httpApiResource.ExportedAwsResourceName}Id:
    Value: !Ref {httpApiResource.ExportedAwsResourceName}
");
                }
                apiTemplateBuilder.AppendLine("");
                apiTemplateBuilder.AppendLine("#LzApis end");
                templateBuilder.Replace("#LzApis#", apiTemplateBuilder.ToString());

                /* AUTHENTICATORS */
                var awsCognitoTemplateBuilder = new StringBuilder();
                awsCognitoTemplateBuilder.AppendLine("#LzAuthenticators start");
                var awsCognitoResources = GetAwsCognitoResources(solution, directive);
                foreach (var cognitoResource in awsCognitoResources)
                {
                    awsCognitoTemplateBuilder.Append(cognitoResource.ExportedAwsResourceDefinition);
                    ouptutsBuilder.Append($@"
  {cognitoResource.ExportedAwsResourceName}UserPoolName:
    Value: {cognitoResource.ExportedAwsResourceName}
  {cognitoResource.ExportedAwsResourceName}UserPoolId:
    Value: !Ref {cognitoResource.ExportedAwsResourceName}UserPool
  {cognitoResource.ExportedAwsResourceName}UserPoolClientId:
    Value: !Ref {cognitoResource.ExportedAwsResourceName}UserPoolClient
  {cognitoResource.ExportedAwsResourceName}IdentityPoolId:
    Value: """"
  {cognitoResource.ExportedAwsResourceName}SecurityLevel:
    Value: {cognitoResource.ExportedSecurityLevel}
");
                }
                awsCognitoTemplateBuilder.AppendLine("");
                awsCognitoTemplateBuilder.AppendLine("#LzAuthenticators end");
                templateBuilder.Replace("#LzAuthenticators#", awsCognitoTemplateBuilder.ToString());


                /* INSERT CONFIG FUNCTION */
                var configJson = new StringBuilder();
                var apiDirectives = directive.Apis.Select(x => solution.Directives[x].Cast<Api>()).ToList().Distinct().ToList();
                foreach (var apiDirective in apiDirectives)
                {
                    if (string.IsNullOrEmpty(apiDirective.Authentication)) continue; // No authentication 
                    var apiArtifact = apiDirective.Artifacts.Values.FirstOrDefault(x => x is AwsHttpApiResource) as AwsHttpApiResource;
                    var jsonText = $@"
                                {apiDirective.Authentication}: {{
                                    awsRegion: '${{AWS::Region}}',
                                    userPoolName: '{apiDirective.Authentication}',
                                    userPoolId: '${{{apiDirective.Authentication}UserPool}}',
                                    userPoolClientId: '${{{apiDirective.Authentication}UserPoolClient}}',
                                    userPoolSecurityLevel: 1,
                                    identityPoolId: ''
                                }},
";
                    configJson.Append(jsonText);
                }
                var config = new StringBuilder();
                config.AppendLine("#LzConfigFunction start");
                config.Append(tenantCloudFrontConfigFunctionSnippet);
                config = config.Replace("__JsonText__", configJson.ToString());
                config.AppendLine("");
                config.AppendLine("#LzConfigFunction end");
                var configText = config.ToString();
                templateBuilder.Replace("#LzConfigFunction#", configText);


                /* INSERT LzQueues */
                var lzQueuesBuilder = new StringBuilder();
                lzQueuesBuilder.AppendLine("#LzQueues start");
                lzQueuesBuilder.Append(GenerateQueueResources(solution, directive));
                lzQueuesBuilder.AppendLine("#LzQeuues end");
                templateBuilder.Replace("#LzQueues#", lzQueuesBuilder.ToString());

                /* Additional OUTPUTS */


                templateBuilder.Replace("#LzOutputs#", ouptutsBuilder.ToString());

                var templatePath = Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Generated", templateName);
                File.WriteAllText(templatePath, templateBuilder.ToString());

                // Exports
                ExportedStackTemplatePath = templatePath;
            }

            catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} for {templateName} : {ex.Message}");
            }
        }

        private List<IAwsApiResource> GetAwsApiResources(SolutionBase solution, Service directive)
        {
            var apiResources = new List<IAwsApiResource>();
            foreach (var apiDirective in directive.Apis.Select(x => solution.Directives[x].Cast<Api>()).ToList())
                foreach (var artifact in apiDirective.Artifacts.Values.OfType<IAwsApiResource>().ToList())
                    apiResources.Add(artifact);
            return apiResources.Distinct().ToList();
        }
        private List<AwsCognitoResource> GetAwsCognitoResources(SolutionBase solution, Service directive)
        {
            var cognitoResources = new List<AwsCognitoResource>();
            foreach (var api in directive.Apis.Select(x => solution.Directives[x].Cast<Api>()).ToList())
                if (api.Authentication != null)
                    cognitoResources.Add((AwsCognitoResource)solution.Directives[api.Authentication].Artifacts.Values.First());    
            
            return cognitoResources.Distinct().ToList();
        }
       

    }
}
