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
    /// Generate a AWS::AppRunner::Service resource suitable for
    /// inclusion in an AWS SAM template. The output is in yaml
    /// format and available in the ExportedAwsResourceDefinition string property.
    /// </summary>
    public class AwsAppRunnerResource : AwsResourceArtifact
    {
        public override string Template { get; set; } = "AWSTemplates/Snippets/sam.service.apprunner.yaml";
        public string ExportedAwsResourceName { get; set; } = null;
        public string ExportedAwsResourceDefinition { get; set; } = null;
        public string ExportedPath { get; set; } = null;
        public string ExportedPrefix { get ; set; } = null;
        public string ExportedResourceType { get; set; } = null;

        // App Runner specific configuration
        public int Cpu { get; set; } = 1024;        // 1 vCPU (1024 CPU units)
        public int Memory { get; set; } = 2048;     // 2 GB
        public int Port { get; set; } = 8080;       // Container port
        public string Runtime { get; set; } = "dotnet8";
        public List<string> ManagedPolicyArns { get; set; } = new List<string>();   

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var resourceName = "";
            try
            {
                await Task.Delay(0);
                Container directive = (Container)directiveArg;
                var apiPrefix = directive.Key;
                // Set the service name
                resourceName = directive.Key;
                resourceName += NameSuffix ?? "";
                Info($"Generating {directive.Key} {resourceName}");

                var template = Template;

                // Read template and generate
                var templateBuilder = new StringBuilder();
                templateBuilder
                    .Append(File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template)));

                templateBuilder.Replace("__ResourceGenerator__", this.GetType().Name);
                templateBuilder.Replace("__AppRunnerServiceName__", resourceName);
                templateBuilder.Replace("__Cpu__", Cpu.ToString());
                templateBuilder.Replace("__Memory__", Memory.ToString());
                templateBuilder.Replace("__Port__", Port.ToString());
                templateBuilder.Replace("__Runtime__", Runtime);

                // Aggregate ManagedPolicyArns from modules and combine with container-level
                var moduleManagedPolicyArns = DiscoverModuleManagedPolicyArns(solution, directive);
                var allManagedPolicyArns = new List<string>();
                allManagedPolicyArns.AddRange(ManagedPolicyArns); // Container-level (backward compatibility)
                allManagedPolicyArns.AddRange(moduleManagedPolicyArns); // Module-level
                allManagedPolicyArns = allManagedPolicyArns.Distinct().ToList();

                templateBuilder.Replace("__ManagedPolicyArns__", string.Join("\n", allManagedPolicyArns.Select(x => $"      - {x}")));

                // Get App Runner project artifacts
                var appRunnerArtifacts = directive.Artifacts.Where(a => a.Value is AspDotNetProject).Select(a => a.Value).Distinct().ToList();
                if (appRunnerArtifacts.Count() == 0)
                    throw new Exception($"No {nameof(AspDotNetProject)} artifacts found for {directive.Key}.");

                if(appRunnerArtifacts.Count() > 1)
                    throw new Exception($"More than one {nameof(AspDotNetProject)} artifacts found for {directive.Key}.");
                var appRunnerArtifact = appRunnerArtifacts.First(); 

                var appRunnerProject = (AspDotNetProject)appRunnerArtifact;

                if(string.IsNullOrEmpty(appRunnerProject.ExportedImageUri))
                    throw new Exception($"{nameof(appRunnerProject.ExportedImageUri)} is null or empty for {appRunnerProject.ExportedName}.");

                templateBuilder.Replace("__ImageUri__", appRunnerProject.ExportedImageUri);

                // Discover authenticators from parent Api directive(s)
                var authenticators = DiscoverAuthenticators(solution, directive);

                // Discover EventsApis from modules
                var eventsApis = DiscoverModuleEventsApis(solution, directive);

                // Generate runtime environment variables
                var runtimeEnvVars = GenerateRuntimeEnvironmentVariables(authenticators, eventsApis);
                templateBuilder.Replace("__RuntimeEnvironmentVariables__", runtimeEnvVars);

                //Exports
                ExportedAwsResourceName = resourceName;
                ExportedAwsResourceDefinition = templateBuilder.ToString();
                ExportedPrefix = apiPrefix;
                ExportedResourceType = "AppRunner";

                // The path is the base path for the API
                ExportedPath = "/";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsAppRunnerResource)}: {resourceName}, {ex.Message}");
            }
        }

        /// <summary>
        /// Discover authenticators from parent Api directive(s) following Service -> Api -> Container hierarchy.
        /// Supports both new Authenticators list and legacy Authentication property for backward compatibility.
        /// </summary>
        private List<string> DiscoverAuthenticators(SolutionBase solution, Container container)
        {
            var authenticators = new List<string>();

            // Find parent Api directives that reference this container
            var parentApis = solution.Directives.Values
                .OfType<Api>()
                .Where(api => api.Containers.Contains(container.Key))
                .ToList();

            // Collect all authenticators from parent APIs
            foreach (var api in parentApis)
            {
                // Use new Authenticators list if present
                if (api.Authenticators != null && api.Authenticators.Any())
                {
                    authenticators.AddRange(api.Authenticators);
                }
            }

            return authenticators.Distinct().ToList();
        }

        /// <summary>
        /// Discover ManagedPolicyArns from Module directives.
        /// Aggregates ExportedManagedPolicyArns from all AwsModuleResource artifacts in referenced modules.
        /// </summary>
        private List<string> DiscoverModuleManagedPolicyArns(SolutionBase solution, Container container)
        {
            var managedPolicyArns = new List<string>();

            // Get all Module directives referenced by this container
            foreach (var moduleName in container.Modules)
            {
                if (solution.Directives.TryGetValue(moduleName, out var directive) && directive is Module module)
                {
                    // Find AwsModuleResource artifacts in this module
                    var moduleResourceArtifacts = module.Artifacts.Values
                        .OfType<AwsModuleResource>()
                        .ToList();

                    // Collect ManagedPolicyArns from each AwsModuleResource
                    foreach (var moduleResource in moduleResourceArtifacts)
                    {
                        if (moduleResource.ExportedManagedPolicyArns != null && moduleResource.ExportedManagedPolicyArns.Any())
                        {
                            managedPolicyArns.AddRange(moduleResource.ExportedManagedPolicyArns);
                        }
                    }
                }
            }

            return managedPolicyArns.Distinct().ToList();
        }

        /// <summary>
        /// Discover EventsApis from Module directives.
        /// Aggregates ExportedEventsApis from all AwsModuleResource artifacts in referenced modules.
        /// </summary>
        private List<string> DiscoverModuleEventsApis(SolutionBase solution, Container container)
        {
            var eventsApis = new List<string>();

            // Get all Module directives referenced by this container
            foreach (var moduleName in container.Modules)
            {
                if (solution.Directives.TryGetValue(moduleName, out var directive) && directive is Module module)
                {
                    // Find AwsModuleResource artifacts in this module
                    var moduleResourceArtifacts = module.Artifacts.Values
                        .OfType<AwsModuleResource>()
                        .ToList();

                    // Collect EventsApis from each AwsModuleResource
                    foreach (var moduleResource in moduleResourceArtifacts)
                    {
                        if (moduleResource.ExportedEventsApis != null && moduleResource.ExportedEventsApis.Any())
                        {
                            eventsApis.AddRange(moduleResource.ExportedEventsApis);
                        }
                    }
                }
            }

            return eventsApis.Distinct().ToList();
        }

        /// <summary>
        /// Generate RuntimeEnvironmentVariables YAML section for App Runner.
        /// Follows MagicPetsService pattern: LZ_AUTH_{NAME}_USERPOOLID
        /// </summary>
        private string GenerateRuntimeEnvironmentVariables(List<string> authenticators, List<string> eventsApis)
        {
            var envVars = new List<string>();

            // Standard environment variables
            envVars.Add("              - Name: ASPNETCORE_ENVIRONMENT");
            envVars.Add("                Value: !Ref EnvironmentParameter");
            envVars.Add("              - Name: AWS_REGION");
            envVars.Add("                Value: !Ref AWS::Region");

            // Authenticator environment variables (LZ_AUTH_{NAME}_USERPOOLID pattern)
            foreach (var authName in authenticators)
            {
                envVars.Add($"              - Name: LZ_AUTH_{authName.ToUpper()}_USERPOOLID");
                envVars.Add($"                Value: !Ref {authName}UserPoolIdParameter");
            }

            // AppSync Events API configuration (named for each EventsApi)
            foreach (var eventsApiName in eventsApis)
            {
                envVars.Add($"              - Name: AWS__AppSync__{eventsApiName}__HttpDomain");
                envVars.Add($"                Value: !GetAtt {eventsApiName}.Dns.Http");
                envVars.Add($"              - Name: AWS__AppSync__{eventsApiName}__ApiKey");
                envVars.Add($"                Value: !GetAtt {eventsApiName}ApiKey.ApiKey");
                envVars.Add($"              - Name: AWS__AppSync__{eventsApiName}__Region");
                envVars.Add("                Value: !Ref AWS::Region");
            }

            return string.Join("\n", envVars);
        }
    }
}