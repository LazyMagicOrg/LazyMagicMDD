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
    /// This Api generator just creates some ouptut values for inclusion in an AWS SAM template.
    /// Since, AppRunners handle their own routing, there is no need to create any additional
    /// API Gateway resources.
    /// </summary>
    public class AwsApiAppRunnerResource : AwsResourceArtifact, IAwsApiResource
    {
        public string ExportedAwsResourceName { get; set; } = null;
        public string ExportedAwsResourceDefinition {get; set; } = null;    
        public string ExportedPath {get; set; } = null;
        public string ExportedPrefix {get; set; } = null;
        public string ExportedResourceType {get; set; } = null;

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var resourceName = "";
            try
            {
                await Task.Delay(0);
                Api directive = (Api)directiveArg;
                var apiPrefix = directive.Key;
                // Set the service name
                resourceName = directive.Key;
                resourceName += NameSuffix ?? "";
                Info($"Generating {directive.Key} {resourceName}");

                //Exports
                ExportedAwsResourceName = resourceName;

                var container = directive.Containers.FirstOrDefault();  
                StackOutputs.Add($@"
  {resourceName}:
    Value: !GetAtt {container}.ServiceUrl
");

            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsApiAppRunnerResource)}: {resourceName}, {ex.Message}");
            }
        }
    }
}