using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.AwsServiceAppRunnersResources;
using static LazyMagic.ArtifactUtils;
using System.Text;
using CommandLine;
using NJsonSchema.CodeGeneration;

namespace LazyMagic
{
    /// <summary>
    /// Generate the service stack template.
    /// 
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
        public AwsServiceConfig AwsServiceConfig { get; set; } = null;

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var templateName = $"sam.{directiveArg.Key}.g.yaml";
            try
            {
                Service service = (Service)directiveArg;

                // Set the service name
                await InfoAsync($"Generating {service.Key} {templateName}");
                var systemTemplatePath = Template;
                var tenantCloudFrontConfigFunctionSnippet = 
                    File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, ConfigFunctionTemplate))
                    .Replace("__TemplateSource__", ConfigFunctionTemplate);

                var templateBuilder = new StringBuilder()
                    .Append(File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, systemTemplatePath)))
                    .Replace("__ResourceGenerator__", this.GetType().Name)
                    .Replace("__TemplateSource__", Template);

                ///////////////////////////////////////////////////////////////////////////////////////
                /* PARAMETERS  - each resource exports what parameteres it needs as inputs in the service stack*/
                var lzParametersBuilder = new StringBuilder();
                lzParametersBuilder.AppendLine("#LzParameters start");
                var awsResources = GetAwsResources(solution, service);
                var lzParameters = new List<string>();  
                foreach (var awsResource in awsResources)
                {
                    lzParameters.AddRange(awsResource.StackParameters);
                }
                lzParameters.Distinct().ToList().ForEach(p => lzParametersBuilder.AppendLine(p));
                if (awsResources.Count == 0)
                    lzParametersBuilder.AppendLine("# none configured");
                lzParametersBuilder.AppendLine("#LzParameters end");
                templateBuilder.Replace("#LzParameters#", lzParametersBuilder.ToString());

                ///////////////////////////////////////////////////////////////////////////////////////
                /* GetAwsAppRunnerResources */
                var lzContainersBuilder = new StringBuilder();
                var appRunnersBuilder = new StringBuilder();    
                appRunnersBuilder.AppendLine("#LzAppRunners start");
                var appRunnerResources = GetAppRunnerResources(solution, service);
                foreach(var appRunnerResource in appRunnerResources)
                {
                    appRunnersBuilder.Append(appRunnerResource.ExportedAwsResourceDefinition);
                }
                appRunnersBuilder.AppendLine("#LzAppRunners end");
                templateBuilder.Replace("#LzAppRunners#", appRunnersBuilder.ToString());

                ///////////////////////////////////////////////////////////////////////////////////////
                /* LZ APIs */
                var apiTemplateBuilder = new StringBuilder();
                apiTemplateBuilder.AppendLine("#LzApis start");
                var apiResources = GetAwsApiResources(solution, service);
                foreach (var apiResource in apiResources)
                {
                    apiTemplateBuilder.Append(apiResource.ExportedAwsResourceDefinition);
                }
                apiTemplateBuilder.AppendLine("");
                apiTemplateBuilder.AppendLine("#LzApis end");

                templateBuilder.Replace("#LzApis#", apiTemplateBuilder.ToString());

                ///////////////////////////////////////////////////////////////////////////////////////
                /* Additional OUTPUTS */
                var outputsBuilder = new StringBuilder();
                outputsBuilder.AppendLine("#LzOutputs start");
                var stackOutputs = new List<string>();
                foreach (var awsResource in awsResources)
                {
                    stackOutputs.AddRange(awsResource.StackOutputs);
                }
                stackOutputs.Distinct().ToList().ForEach(p => outputsBuilder.AppendLine(p));
                if (awsResources.Count == 0)
                    outputsBuilder.AppendLine("# none configured");
                outputsBuilder.AppendLine("#LzOutputs end");
                templateBuilder
                    .Replace("#LzOutputs#", outputsBuilder.ToString())
                    .Replace("__ResourceGenerator__", GetType().Name);
                var templatePath = Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Generated", templateName);

                ///////////////////////////////////////////////////////////////////////////////////////
                /* Write Template */
                File.WriteAllText(templatePath, templateBuilder.ToString());

                ///////////////////////////////////////////////////////////////////////////////////////
                // Exports
                ExportedStackTemplatePath = templatePath;
                AwsServiceConfig = new AwsServiceConfig()
                {
                    Name = service.Key
                };
            }

            catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} for {templateName} : {ex.Message}");
            }
        }

        /// <summary>
        /// Get all AWS API resource artifacts from the Service hierarchy.
        /// Follows: Service -> Api (for Api-level resources) and Service -> Api -> Container (for Container-level API resources)
        /// </summary>
        private List<IAwsApiResource> GetAwsApiResources(SolutionBase solution, Service directive)
        {
            var resources = new List<IAwsApiResource>();

            // Get all Api directives referenced by this Service
            var apiDirectives = directive.Apis
                .Select(x => solution.Directives[x])
                .OfType<Api>()
                .ToList();

            // Get Api-level resources (AwsApiAppRunnerResource, etc.)
            var apiLevelResources = apiDirectives
                .SelectMany(api => api.Artifacts.Values.OfType<IAwsApiResource>())
                .Distinct()
                .ToList();

            resources.AddRange(apiLevelResources);

            // Get Container-level Api resources (AwsAppRunnerResource implements IAwsApiResource)
            var containerApiResources = apiDirectives
                .SelectMany(api => api.Containers)
                .Select(cn => (Container)solution.Directives[cn])
                .Where(c => c.IsDefault == false)
                .SelectMany(c => c.Artifacts.Values.OfType<IAwsApiResource>())
                .Distinct()
                .ToList();

            resources.AddRange(containerApiResources);

            return resources.Distinct().ToList();
        }

        /// <summary>
        /// Get all AWS resource artifacts from the Service hierarchy.
        /// Follows: Service -> Api -> Container and Service -> Api -> Authentication
        /// </summary>
        private List<AwsResourceArtifact> GetAwsResources(SolutionBase solution, Service directive)
        {
            var resources = new List<AwsResourceArtifact>();

            // Get all Api directives referenced by this Service
            var apiDirectives = directive.Apis
                .Select(x => solution.Directives[x])
                .OfType<Api>()
                .ToList();

            // Get Api-level resources (AwsApiAppRunnerResource, etc.)
            var apiLevelResources = apiDirectives
                .SelectMany(api => api.Artifacts.Values.OfType<AwsResourceArtifact>())
                .Distinct()
                .ToList();

            resources.AddRange(apiLevelResources);

            // Get Authentication resources from Api directives
            var authResources = apiDirectives
                .Where(api => api.Authenticators != null && api.Authenticators.Any())
                .SelectMany(api => api.Authenticators)
                .Select(authKey => solution.Directives[authKey])
                .SelectMany(auth => auth.Artifacts.Values.OfType<AwsResourceArtifact>())
                .Distinct()
                .ToList();

            resources.AddRange(authResources);

            // Get Container-level resources (AppRunner, etc.) from Api -> Container hierarchy
            var containerResources = apiDirectives
                .SelectMany(api => api.Containers)
                .Select(cn => (Container)solution.Directives[cn])
                .Where(c => c.IsDefault == false)
                .SelectMany(c => c.Artifacts.Values.OfType<AwsResourceArtifact>())
                .Distinct()
                .ToList();

            resources.AddRange(containerResources);

            return resources.Distinct().ToList();
        }

        /// <summary>
        /// Get AppRunner resources from the Service hierarchy.
        /// Follows: Service -> Api -> Container
        /// </summary>
        private List<AwsAppRunnerResource> GetAppRunnerResources(SolutionBase solution, Service directive) =>
            directive.Apis
                .Select(x => solution.Directives[x])
                .OfType<Api>()
                .SelectMany(api => api.Containers)
                .Select(cn => (Container)solution.Directives[cn])
                .Where(c => c.IsDefault == false)
                .SelectMany(c => c.Artifacts.Values)
                .OfType<AwsAppRunnerResource>()
                .Distinct()
                .ToList();

        /// <summary>
        /// Generic helper method to get resources of a specific type from the Service hierarchy.
        /// Useful for filtering resources by type without writing separate methods.
        /// </summary>
        private List<T> GetResourcesByType<T>(SolutionBase solution, Service directive) where T : AwsResourceArtifact
        {
            return GetAwsResources(solution, directive).OfType<T>().ToList();
        }
    }

}
