using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using static LazyMagic.ArtifactUtils;

namespace LazyMagic
{
    public static class AwsServiceLambdasResources
    {

        /// <summary>
        /// Return the Lambda and Lambda Permissions resources for the service.
        /// We grab the exported resource from the AwsLambdaResouce artifact and
        /// add in the permissions for each api calling the lambda.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="directiveArg"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string GenerateLambdaResources(SolutionBase solution, DirectiveBase directiveArg)
        {
            try
            {
                Service directive = (Service)directiveArg;

                var lambdaPermissionSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.service.lambda.permission.yaml"));
                //var sourceArnSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.sourcearn.yaml"));

                var apiGateways = new List<string>();

                var resourceBuilder = new StringBuilder();

                /* LAMBDAS */
                var lambdaArtifacts = GetLambdaResources(solution, directive);   
                foreach(var lambdaArtifact in lambdaArtifacts)
                {
                    var lambdaTemplate = lambdaArtifact.ExportedAwsResourceDefinition;
                    resourceBuilder.Append(lambdaTemplate);

                    /* LAMBDA PERMISSIONS */

                    // Add SourceArns for each api calling the lambda
                    var permissions = "";
                    var apiArtifacts = GetApisForContainer(solution, lambdaArtifact.ExportedContainerKey);    
                    foreach (var apiArtifact in apiArtifacts)
                    {
                        permissions += lambdaPermissionSnippet
                            .Replace("__ApiName__", apiArtifact.ExportedName)
                            .Replace("__LambdaName__", lambdaArtifact.ExportedName);   
                    }
                    resourceBuilder.Append(permissions);
                }
                var result = resourceBuilder.ToString();
                return result;
            }

            catch (Exception ex)
            {
                throw new Exception($"Error generating AwsServerLambda:  {ex.Message}");
            }
        }

        private static List<AwsHttpApiResource> GetApisForContainer(SolutionBase solution, string containerKey)
        {
            var apis = new List<AwsHttpApiResource>();
            foreach (var directive in solution.Directives.Values.Where(x => x is Api).Cast<Api>().ToList())
                foreach(var artifact  in directive.Artifacts.Values.Where(x => x is AwsHttpApiResource).Cast<AwsHttpApiResource>().ToList())
                    apis.Add(artifact);
            return apis.Distinct().ToList();
        }

        private static List<AwsLambdaResource> GetLambdaResources(SolutionBase solution, Service directive)
        {
            var lambdas = new List<AwsLambdaResource>();
            var containerKeys = GetContainers(solution, directive);
            var lambdaArtifacts = solution.Directives.GetArtifactsByType(containerKeys, "AwsLambdaResource");
            foreach (var artifact in lambdaArtifacts)
            {
                var lambdaArtifact = artifact as AwsLambdaResource;
                if (lambdaArtifact == null)
                    throw new Exception($"Error generating AwsService: {directive.Key}, {artifact.Key} artifact isn't a AwsLambdaResource");
                lambdas.Add(lambdaArtifact);
            }
            return lambdas;
        }

    }
}
