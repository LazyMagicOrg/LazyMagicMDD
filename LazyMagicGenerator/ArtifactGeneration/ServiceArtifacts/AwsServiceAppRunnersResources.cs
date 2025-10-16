using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using static LazyMagic.ArtifactUtils;
using Microsoft.AspNetCore.Routing.Template;

namespace LazyMagic
{
    public static class AwsServiceAppRunnersResources
    {
        /// <summary>
        /// Generate content for the #LzAppRunners# section by aggregating all the 
        /// previously generated AppRunner resources related to 
        /// the APIs in the service.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="serviceDirectiveArg"></param>
        /// <returns>SAM resource definitions</returns>
        /// <exception cref="Exception"></exception>
        public static string GenerateAppRunnersResources(SolutionBase solution, DirectiveBase serviceDirectiveArg)
        {
            try
            {
                Service serviceDirective = (Service)serviceDirectiveArg;

                var resourceBuilder = new StringBuilder();

                var artifacts = GetAwsAppRunnerResources(solution, serviceDirective);   
                foreach(var lambdaArtifact in artifacts)
                {
                    var lambdaTemplate = lambdaArtifact.ExportedAwsResourceDefinition;
                    resourceBuilder.Append(lambdaTemplate);
                    resourceBuilder.AppendLine();
                }
                return resourceBuilder.ToString();
            }

            catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsServiceAppRunnersResources)} : {ex.Message}");
            }
        }
        private static List<AwsAppRunnerResource> GetAwsAppRunnerResources(SolutionBase solution, Service directive) =>
            directive.Apis
                .Select(k => (Api)solution.Directives[k])
                .SelectMany(api => api.Containers)
                .Select(cn => (Container)solution.Directives[cn])
                .Where(c => c.IsDefault == false)
                .SelectMany(c => c.Artifacts.Values)
                .OfType<AwsAppRunnerResource>()
                .Distinct()
                .ToList();
    }

}
