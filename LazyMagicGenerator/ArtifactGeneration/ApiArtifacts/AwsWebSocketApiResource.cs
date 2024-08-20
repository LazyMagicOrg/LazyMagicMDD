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
    public class AwsWebSocketApiResource : ArtifactBase, IAwsApiResource
    {
        public string DefinitionBodySnippet { get; set; } = null;
        public string PathSnippet { get; set; } = null;
        public string ExportedResourceName { get; set; } = null;  
        public string ExportedResource { get; set; } = null;
        public string ExportedPath { get; set; } = null;   
        public string ExportedPrefix { get ; set; } = null; 
        
        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var resourceName = "";
            try
            {
                await Task.Delay(0);
                Api directive = (Api)directiveArg;
                var apiPrefix = directive.ApiPrefix ?? directive.Key;   
                // Set the service name
                resourceName =  directive.Key;
                if(directive.Containers.Count == 0)
                    throw new Exception($"Containers not found for {directive.Key} {resourceName}");
                if(directive.Containers.Count > 1)
                    throw new Exception($"Only a single container allowed {directive.Key} {resourceName}");   
                var webSocketFunction = directive.Containers[0];
                Info($"Generating {directive.Key} {resourceName}");

                var template = DefinitionBodySnippet ?? "AwsTemplates/Snippets/sam.service.websocketapi.yaml";

                var cognitoResource = directive.Authentication;

                // Read snippets and generate
                var templateBuilder = new StringBuilder(File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template)));

                templateBuilder
                    .Replace("__WebSocketApi__", resourceName)
                    .Replace("__WebSocketFunction__", webSocketFunction);


                //Exports 
                ExportedName = resourceName;
                ExportedResourceName = resourceName;    
                ExportedResource = templateBuilder.ToString();
                ExportedPrefix = apiPrefix;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsHttpApiResource)}: {resourceName}, {ex.Message}");
            }
        }
        private List<DotNetLambdaProject> GetLambdaProjects(Api directive, Directives directives)
        {
            var projects = new List<DotNetLambdaProject>();
            foreach (var containerName in directive.Containers)
            {
                var container = (Container)directives[containerName];
                foreach (var artifact in container.Artifacts.Values.Where(x => x.Type.Equals("DotNetLambda")))
                {
                    projects.Add((DotNetLambdaProject)artifact);
                }
            }
            return projects;
        }
    }
}
