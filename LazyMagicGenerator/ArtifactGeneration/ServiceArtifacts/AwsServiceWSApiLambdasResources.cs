using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using static LazyMagic.ArtifactUtils;
using Microsoft.AspNetCore.Routing.Template;

namespace LazyMagic
{
    public static class AwsServiceWSApiLambdasResources
    {
        /// <summary>
        /// Generate content for the #LzLambdas# section by aggregating all the 
        /// previously generated AWS::Lambda::Function resources related to 
        /// the WebSocket APIs in the service.
        /// Generate the AWS::Lambda::Permission resources for each 
        /// lambda as well.
        /// The resource definition for the lambdas and permissions 
        /// are returned as a string in yaml format.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="serviceDirectiveArg"></param>
        /// <returns>SAM resource definitions</returns>
        /// <exception cref="Exception"></exception>
        public static string GenerateWSApiLambdaResources(SolutionBase solution, DirectiveBase serviceDirectiveArg)
        {
            try
            {
                var template = "AWSTemplates/Snippets/sam.service.lambda.permission.yaml";
                Service serviceDirective = (Service)serviceDirectiveArg;

                var lambdaPermissionSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template));

                var apiGateways = new List<string>();

                var resourceBuilder = new StringBuilder();

                /* LAMBDAS */
                var lambdaArtifacts = GetLambdaResources(solution, serviceDirective);   
                foreach(var lambdaArtifact in lambdaArtifacts)
                {
                    var lambdaTemplate = lambdaArtifact.ExportedAwsResourceDefinition;
                    resourceBuilder.Append(lambdaTemplate);
                    resourceBuilder.AppendLine();

                    /* LAMBDA PERMISSIONS for access by API */

                    // Add SourceArns for each api calling the lambda
                    var permissions = "";
                    var apiArtifacts = GetApisForContainer(solution, lambdaArtifact);    
                    foreach (var apiArtifact in apiArtifacts)
                    {
                        permissions += lambdaPermissionSnippet

                            .Replace("__ApiName__", apiArtifact.ExportedAwsResourceName)
                            .Replace("__LambdaName__", lambdaArtifact.ExportedAwsResourceName);   
                    }
                    resourceBuilder.Append(permissions);
                    resourceBuilder.AppendLine();
                }
                resourceBuilder
                    .Replace("__ResourceGenerator__", nameof(AwsServiceApiLambdasResources))
                    .Replace("__TemplateSource__",template);
                var templateResource = resourceBuilder.ToString();
                return templateResource;
            }

            catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsServiceWSApiLambdasResources)} : {ex.Message}");
            }
        }

        private static List<IAwsApiResource> GetApisForContainer(SolutionBase solution, AwsWSApiLambdaResource currentArtifact)
        {
            var containerDirective = solution.Directives.GetArtifactDirective(currentArtifact);
            return solution.Directives.Values
                .OfType<Api>()
                .Where(api => api.Containers.Contains(containerDirective.Key))
                .SelectMany(api => api.Artifacts.Values)
                .OfType<IAwsApiResource>()
                .ToList();
        }

        private static List<AwsWSApiLambdaResource> GetLambdaResources(SolutionBase solution, Service directive) =>
            directive.Apis
                .Select(k => (Api)solution.Directives[k])
                .SelectMany(api => api.Containers)
                .Select(cn => (Container)solution.Directives[cn])
                .Where(c => c.IsDefault == false)
                .SelectMany(c => c.Artifacts.Values)
                .OfType<AwsWSApiLambdaResource>()
                .Distinct()
                .ToList();

    }
}
