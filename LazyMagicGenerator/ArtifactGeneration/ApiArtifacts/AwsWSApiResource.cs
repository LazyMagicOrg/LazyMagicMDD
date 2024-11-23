using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using static LazyMagic.LzLogger;

namespace LazyMagic
{
    /// <summary>
    /// Generate a AWS::ApiGatewayV2::Api resource suitable for inclusion in 
    /// an AWS SAM Template. This resource is in yaml format and is 
    /// available in the ExportResource string property.
    /// </summary>
    public class AwsWSApiResource : ArtifactBase, IAwsApiResource
    {
        public override string Template { get; set; } = "AwsTemplates/Snippets/sam.service.messaging.wsapi.yaml";
        public string ExportedAwsResourceName { get; set; } = null;  
        public string ExportedAwsResourceDefinition { get; set; } = null;
        public string ExportedPath { get; set; } = null;   
        public string ExportedPrefix { get ; set; } = null; 
        
        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var resourceName = "";
            try
            {
                await Task.Delay(0);
                Api directive = (Api)directiveArg;
                var apiPrefix = directive.Key;   
                // Set the service name
                resourceName =  directive.Key;
                resourceName += NameSuffix ?? "";
                if (directive.Containers.Count == 0)
                    throw new Exception($"Containers not found for {directive.Key} {resourceName}");

                if(directive.Containers.Count > 1)
                    throw new Exception($"Only a single container allowed {directive.Key} {resourceName}");  
                
                var webSocketFunction = directive.Containers[0];
                Info($"Generating {directive.Key} {resourceName}");

                var template = Template;

                var cognitoResource = directive.Authentication;

                // Read snippets and generate
                var templateBuilder = new StringBuilder(File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template)));

                templateBuilder
                    .Replace("__ResourceGenerator__", this.GetType().Name)
                    .Replace("__TemplateSource__", Template)
                    .Replace("__WebSocketApi__", resourceName)
                    .Replace("__WebSocketFunction__", webSocketFunction)
                    .Replace("__CognitoResource__", cognitoResource);

                //Exports 
                ExportedAwsResourceName = resourceName;    
                ExportedAwsResourceDefinition = templateBuilder.ToString();
                ExportedPrefix = apiPrefix;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsHttpApiResource)}: {resourceName}, {ex.Message}");
            }
        }
        private List<DotNetWSApiLambdaProject> GetLambdaProjects(Api directive, Directives directives)
        {
            var projects = new List<DotNetWSApiLambdaProject>();
            foreach (var containerName in directive.Containers)
            {
                var container = (Container)directives[containerName];
                foreach (var artifact in container.Artifacts.Values.Where(x => x is DotNetWSApiLambdaProject))
                {
                    projects.Add((DotNetWSApiLambdaProject)artifact);
                }
            }
            return projects;
        }
    }
}
