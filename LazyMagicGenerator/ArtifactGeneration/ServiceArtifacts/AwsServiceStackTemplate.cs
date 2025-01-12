using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.AwsServiceApiLambdasResources;
using static LazyMagic.AwsServiceWSApiLambdasResources;
using static LazyMagic.AwsSQSResources;
using static LazyMagic.ArtifactUtils;
using System.Text;
using CommandLine;
using NJsonSchema.CodeGeneration;

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
        public override string Template { get; set; } = "AWSTemplates/Snippets/sam.service.yaml";
        public string  ConfigFunctionTemplate { get; set; } = "AWSTemplates/Snippets/sam.service.cloudfront.configfunction.yaml";
        public string LambdasTemplate { get; set; } = null;
        public string LambdaPermissionsTemplate { get; set; } = null;
        public string MessagingWebSocketApi { get; set; } = null;
        
        // Exports
        public string ExportedStackTemplatePath { get; set; } = null;

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var templateName = $"sam.{directiveArg.Key}.g.yaml";
            try
            {
                Service directive = (Service)directiveArg;
                var wsApi = MessagingWebSocketApi ?? "";

                // Set the service name
               
                await InfoAsync($"Generating {directive.Key} {templateName}");
                var systemTemplatePath = Template;
                var tenantCloudFrontConfigFunctionSnippet = 
                    File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, ConfigFunctionTemplate))
                    .Replace("__TemplateSource__", ConfigFunctionTemplate);

                var ouptutsBuilder = new StringBuilder();

                var templateBuilder = new StringBuilder()
                    .Append(File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, systemTemplatePath)))
                    .Replace("__ResourceGenerator__", this.GetType().Name)
                    .Replace("__TemplateSource__", Template);

                ///////////////////////////////////////////////////////////////////////////////////////
                /* PARAMETERS */
                var lzParametersBuilder = new StringBuilder();
                lzParametersBuilder.AppendLine("#LzParameters start");
                lzParametersBuilder.AppendLine("# none configured");
                lzParametersBuilder.AppendLine("#LzParameters end");
                templateBuilder.Replace("#LzParameters#", lzParametersBuilder.ToString());

                ///////////////////////////////////////////////////////////////////////////////////////
                /* LAMBDAS */
                var lzLambdasBuilder = new StringBuilder();
                lzLambdasBuilder.AppendLine("#LzLambdas start");
                lzLambdasBuilder.Append(GenerateApiLambdaResources(solution, directive));
                lzLambdasBuilder.Append(GenerateWSApiLambdaResources(solution, directive));
                lzLambdasBuilder.AppendLine("#LzLambdas end");
                templateBuilder.Replace("#LzLambdas#",lzLambdasBuilder.ToString());

                ///////////////////////////////////////////////////////////////////////////////////////
                /* LZ APIs */
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


                ///////////////////////////////////////////////////////////////////////////////////////
                /* CONFIG FUNCTION */
                var configJson = new StringBuilder();
                var apiDirectives = directive.Apis.Select(x => solution.Directives[x].Cast<Api>()).Distinct();
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

                // we suppot only a single WSAPI 
                var wsUrl = string.IsNullOrEmpty(wsApi)
                    ? "wsUrl: '',"
                    : "wsUrl: 'wss://${__WebSocketApi__}.execute-api.${AWS::Region}.amazonaws.com/${EnvironmentParameter}',"
                        .Replace("__WebSocketApi__", wsApi);

                var configText = new StringBuilder()
                    .AppendLine("#LzConfigFunction start")
                    .Append(tenantCloudFrontConfigFunctionSnippet)
                    .Replace("__wsUrl__",wsUrl)
                    .Replace("__JsonText__", configJson.ToString())
                    .AppendLine("")
                    .AppendLine("#LzConfigFunction end").ToString();
                templateBuilder.Replace("#LzConfigFunction#", configText);

                ///////////////////////////////////////////////////////////////////////////////////////
                /* LzQueues */
                var lzQueuesBuilder = new StringBuilder()
                    .AppendLine("#LzQueues start");
                lzQueuesBuilder.Append(GenerateSQSResources(solution, directive));
                lzQueuesBuilder.AppendLine("#LzQeuues end");
                templateBuilder.Replace("#LzQueues#", lzQueuesBuilder.ToString());

                ///////////////////////////////////////////////////////////////////////////////////////
                /* Additional OUTPUTS */
                templateBuilder
                    .Replace("#LzOutputs#", ouptutsBuilder.ToString())
                    .Replace("__ResourceGenerator__", GetType().Name);
                var templatePath = Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Generated", templateName);

                ///////////////////////////////////////////////////////////////////////////////////////
                /* Write Template */
                File.WriteAllText(templatePath, templateBuilder.ToString());

                ///////////////////////////////////////////////////////////////////////////////////////
                // Exports
                ExportedStackTemplatePath = templatePath;
            }

            catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} for {templateName} : {ex.Message}");
            }
        }

        private List<IAwsApiResource> GetAwsApiResources(SolutionBase solution, Service directive) =>
            directive.Apis
                .Select(x => solution.Directives[x])
                .OfType<Api>()
                .SelectMany(apiDirective => apiDirective.Artifacts.Values.OfType<IAwsApiResource>())
                .Distinct()
                .ToList();
        
        private List<AwsCognitoResource> GetAwsCognitoResources(SolutionBase solution, Service directive) =>
           directive.Apis
               .Select(x => solution.Directives[x].Cast<Api>())
               .Where(api => api.Authentication != null)
               .Select(api => (AwsCognitoResource)solution.Directives[api.Authentication].Artifacts.Values.First())
               .Distinct()
               .ToList();
    }
}
