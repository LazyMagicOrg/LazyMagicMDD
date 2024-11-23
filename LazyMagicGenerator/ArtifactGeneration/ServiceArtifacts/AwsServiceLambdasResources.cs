using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using static LazyMagic.ArtifactUtils;
using Microsoft.AspNetCore.Routing.Template;

namespace LazyMagic
{
    public static class AwsServiceLambdasResources
    {
     

        /// <summary>
        /// Generate the #LzLambdas# content by aggregating all the 
        /// previously generated AWS::Lambda::Function resources. 
        /// Generate the AWS::Lambda::Permission resources for each 
        /// lambda as well.
        /// The resource definition for the lambdas and permissions 
        /// are returned as a string in yaml format.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="directiveArg"></param>
        /// <returns>SAM resource definitions</returns>
        /// <exception cref="Exception"></exception>
        public static string GenerateLambdaResources(SolutionBase solution, DirectiveBase directiveArg)
        {
            try
            {
                var template = "AWSTemplates/Snippets/sam.service.lambda.permission.yaml";
                Service directive = (Service)directiveArg;

                var lambdaPermissionSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template));
                //var sourceArnSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.sourcearn.yaml"));

                var apiGateways = new List<string>();

                var resourceBuilder = new StringBuilder();

                /* LAMBDAS */
                var lambdaArtifacts = GetLambdaResources(solution, directive);   
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
                    .Replace("__ResourceGenerator__", nameof(AwsServiceLambdasResources))
                    .Replace("__TemplateSource__",template);
                var templateResource = resourceBuilder.ToString();
                return templateResource;
            }

            catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsServiceLambdasResources)} : {ex.Message}");
            }
        }

        private static List<IAwsApiResource> GetApisForContainer(SolutionBase solution, AwsApiLambdaResource currentArtifact)
        {
            var containerDirective = solution.Directives.GetArtifactDirective(currentArtifact);
            return solution.Directives.Values
                .OfType<Api>()
                .Where(api => api.Containers.Contains(containerDirective.Key))
                .SelectMany(api => api.Artifacts.Values)
                .OfType<IAwsApiResource>()
                .ToList();
        }

        private static List<AwsApiLambdaResource> GetLambdaResources(SolutionBase solution, Service directive)
        {
            var lambdas = new List<AwsApiLambdaResource>();
            var lambdaArtifacts = solution.Directives.GetArtifactsByType<AwsApiLambdaResource>();
            foreach (var artifact in lambdaArtifacts)
            {
                var lambdaArtifact = artifact as AwsApiLambdaResource;
                if (lambdaArtifact == null)
                    throw new Exception($"Error generating AwsService: {directive.Key}, artifact isn't a AwsLambdaResource");
                lambdas.Add(lambdaArtifact);
            }
            return lambdas;
        }

    }
}
