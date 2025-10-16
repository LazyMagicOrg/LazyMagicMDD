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
        public string Authentication { get; set; } = null;

        // App Runner specific configuration
        public int Cpu { get; set; } = 1024;        // 1 vCPU (1024 CPU units)
        public int Memory { get; set; } = 2048;     // 2 GB
        public int Port { get; set; } = 8080;       // Container port
        public string Runtime { get; set; } = "dotnet8";
        public List<string> ManagedPolicyArns { get; set; } = new List<string>();
        public string EventsApi { get; set; } = null;   

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
                templateBuilder.Replace("__ManagedPolicyArns__", string.Join("\n", ManagedPolicyArns.Select(x => $"      - {x}")));
                templateBuilder.Replace("__CognitoResource__", Authentication);
                templateBuilder.Replace("__AppSyncEventsApiName__", EventsApi);

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
    }
}