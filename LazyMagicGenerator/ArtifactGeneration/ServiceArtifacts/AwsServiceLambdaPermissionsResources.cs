using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;
using System.Text;

namespace LazyMagic
{
    public static class AwsServiceLambdaPermissionsResources
    {
        // Not currently used

        public static string GenerateLambdaPermissionResources(SolutionBase solution, DirectiveBase directiveArg)
        {
            try
            {
                Service directive = (Service)directiveArg;

                var lambdaPermissionSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.service.lambdapermission.yaml"));

                var apiGateways = new List<string>();

                var resourceBuilder = new StringBuilder();
                var containerKeys = GetContainerKeys(solution, directive);
                var lambdaArtifacts = solution.Directives.GetArtifactsByType(containerKeys, "AwsLambdaResource");
                foreach (var artifact in lambdaArtifacts)
                {
                    var lambdaArtifact = artifact as AwsLambdaResource;
                    if (lambdaArtifact == null)
                        throw new Exception($"Error generating AwsLambdaPermissionsStack: {directive.Key}, {artifact.Key} artifact isn't a AwsLambdaResource");

                    // Insert the Lambda resource with permissions
                    var lambdaTemplate = lambdaPermissionSnippet;
                    lambdaTemplate = lambdaTemplate
                        .Replace("__LambdaName__", lambdaArtifact.ExportedName)
                        .Replace("__LambdaArn__", $"{lambdaArtifact.ExportedName}ArnParameter");
                    resourceBuilder.Append(lambdaTemplate);
                    var lambdaApis = GetApiKeysForContainer(solution, lambdaArtifact.ExportedContainerKey);
                    var apiArtifacts = solution.Directives.GetArtifactsByType(lambdaApis, "AwsHttpApiResource");
                    var permissions = "";
                    foreach (var apiArtifact in apiArtifacts)
                    {
                        var api = apiArtifact as AwsHttpApiResource;
                        if (api == null)
                            throw new Exception($"Error generating AwsService: {directive.Key}, {apiArtifact.Key} artifact isn't a AwsHttpApiResource.");
                    }
                    resourceBuilder.Append(permissions);
                }

                return resourceBuilder.ToString();
            }

            catch (Exception ex)
            {
                throw new Exception($"Error generating AwsServiceLambdaPermissions: {ex.Message}");
            }
        }

        private static List<string> GetApiKeysForContainer(SolutionBase solution, string containerKey)
        {
            var apis = new List<string>();
            foreach (var directive in solution.Directives.Values)
            {
                if (directive is Api)
                {
                    var api = directive as Api;
                    if (api.Containers.Contains(containerKey))
                        apis.Add(api.Key);
                }
            }
            return apis.Distinct().ToList();
        }

        private static List<string> GetContainerKeys(SolutionBase solution, Service directive)
        {
            var lambdas = new List<string>();
            foreach (var apiKey in directive.Apis)
                if (solution.Directives.TryGetValue(apiKey, out DirectiveBase apiDirective))
                {
                    if (!(apiDirective is Api))
                        throw new Exception($"Error generating AwsService: {directive.Key}, {apiKey} is not an Api.");

                    foreach (var containerKey in ((Api)apiDirective).Containers)
                    {
                        if (solution.Directives.TryGetValue(containerKey, out DirectiveBase container))
                        {
                            if (!(container is Container))
                                throw new Exception($"Error generating AwsService: {directive.Key}, {containerKey} is not a Container.");
                            lambdas.Add(containerKey);
                        }
                    }
                }
                else
                    throw new Exception($"Error generating AwsService: {directive.Key}, Api {apiKey} not found.");
            return lambdas.Distinct().ToList();
        }

    }
}
