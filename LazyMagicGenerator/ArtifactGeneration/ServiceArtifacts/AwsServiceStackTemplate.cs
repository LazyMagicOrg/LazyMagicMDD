using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.AwsServiceLambdasResources;
using static LazyMagic.AwsServiceLambdaPermissionsResources;    
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
            var templateName = "";    
            try
            {
                Service directive = (Service)directiveArg;

                // Set the service name
                templateName = $"sam.{directive.Key}.g.yaml";
                await InfoAsync($"Generating {directive.Key} {templateName}");
                var systemTemplatePath = Template ?? "AWSTemplates/Snippets/sam.service.yaml";
                var lambdasTemplatePath = LambdasTemplate ?? "AWSTemplates/Snippets/sam.service.lambdas.yaml";
                var lambdaPermissionsTemplate = LambdaPermissionsTemplate ?? "AWSTemplates/Snippets/sam.service.lambdapermissions.yaml";

                var ouptutsBuilder = new StringBuilder();

                var templateBuilder = new StringBuilder(); 
                templateBuilder.Append(File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, systemTemplatePath)));

                /* LAMBDAS */
                var lambdas = GenerateLambdaResources(solution, directive);   
                templateBuilder.Replace("#LzLambdas", lambdas);

                /* HTTP APIs */
                var apiTemplateBuilder = new StringBuilder();
                var apiResources = GetAwsApiResources(solution, directive);
                foreach (var httpApiResource in apiResources)
                {
                    apiTemplateBuilder.Append(httpApiResource.ExportedResource);
                    ouptutsBuilder.Append($@"
  {httpApiResource.ExportedResourceName}Id:
    Value: !Ref {httpApiResource.ExportedResourceName}
");
                }

                templateBuilder.Replace("#LzApis#", apiTemplateBuilder.ToString());

                /* AUTHENTICATORS */
                var awsCognitoTemplateBuilder = new StringBuilder();
                var awsCognitoResources = GetAwsCognitoResources(solution, directive);
                foreach (var cognitoResource in awsCognitoResources)
                {
                    awsCognitoTemplateBuilder.Append(cognitoResource.ExportedResource);
                    ouptutsBuilder.Append($@"
  {cognitoResource.ExportedResourceName}UserPoolName:
    Value: {cognitoResource.ExportedResourceName}
  {cognitoResource.ExportedResourceName}UserPoolId:
    Value: !Ref {cognitoResource.ExportedResourceName}UserPool
  {cognitoResource.ExportedResourceName}UserPoolClientId:
    Value: !Ref {cognitoResource.ExportedResourceName}UserPoolClient
  {cognitoResource.ExportedResourceName}IdentityPoolId:
    Value: """"
  {cognitoResource.ExportedResourceName}SecurityLevel:
    Value: {cognitoResource.ExportedSecurityLevel}
");
                }
                templateBuilder.Replace("#LzAuthenticators#", awsCognitoTemplateBuilder.ToString());


                /* Additional OUTPUTS */   
                

                templateBuilder.Replace("#LzOutputs#", ouptutsBuilder.ToString());

                var templatePath = Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Generated", templateName);
                File.WriteAllText(templatePath, templateBuilder.ToString());

                // Exports
                ExportedStackTemplatePath = templatePath;
            }

            catch (Exception ex)
            {
                throw new Exception($"Error generating AwsServiceStack template: {templateName}, {ex.Message}");
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
