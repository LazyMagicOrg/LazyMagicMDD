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
    /// Generate a AWS::Serverless::HttpApi resource suitable for 
    /// inclusion in an AWS SAM template. The output is in yaml 
    /// format and available in the ExportedAwsResourceDefinition string property.
    /// </summary>
    public class AwsHttpApiResource : ArtifactBase, IAwsApiResource
    {
        public override string Template { get; set; } = "AwsTemplates/Snippets/sam.service.httpapi.cognito.yaml";
        public string DefinitionBodySnippet { get; set; } = "AwsTemplates/Snippets/sam.service.httpapi.definitionbody.yaml";
        public string PathSnippet { get; set; } = "AwsTemplates/Snippets/sam.service.httpapi.path.yaml";
        public string CognitoAuthSnippet { get; set; } = "AwsTemplates/Snippets/sam.service.httpapi.cognito.auth.yaml";
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
                resourceName = directive.Key;
                resourceName += NameSuffix ?? "";
                Info($"Generating {directive.Key} {resourceName}");

                var template = Template;   

                var cognitoResource = directive.Authentication;

                // Read snippets and generate
                var templateBuilder = new StringBuilder();
                templateBuilder
                    .Append(File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template)))
                    .Replace("__TemplateSource__", template);

                var definitionBody = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, DefinitionBodySnippet))
                    .Replace("__TemplateSource__", DefinitionBodySnippet);
                templateBuilder.Append(definitionBody);

                var pathTemplate = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, PathSnippet))
                    .Replace("__TemplateSource__", PathSnippet);

                templateBuilder.Replace("__ResourceGenerator__", this.GetType().Name);
                templateBuilder.Replace("__ApiGatewayName__", resourceName);

                var cognitoAuth = "";
                if(!string.IsNullOrEmpty(cognitoResource))
                {
                    cognitoAuth = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, CognitoAuthSnippet))
                        .Replace("__CognitoResource__", cognitoResource);
                }
                templateBuilder.Replace("#CognitoAuth#", cognitoAuth);

                var lambdaArtifacts = solution.Directives.GetArtifactsByType<DotNetApiLambdaProject>(directive.Containers);

                // Generate Paths and insert into template
                foreach(var lambdaArtifact in lambdaArtifacts)
                {
                    var lambda = (DotNetApiLambdaProject)lambdaArtifact;
                    if (string.IsNullOrEmpty(lambda.ExportedOpenApiSpec)) continue;
                    var openApiSpec = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, lambda.ExportedOpenApiSpec));
                    var openApiDocument = await ParseOpenApiYamlContent(openApiSpec);
                    foreach(var path in openApiDocument.Paths)
                    {
                        templateBuilder.AppendLine($"          '{path.Key}':");
                        foreach (var op in path.Value.Keys.ToList())
                        {
                            var pathDoc = pathTemplate.Replace("__op__", op);
                            pathDoc = pathDoc.Replace("__LambdaName__", lambda.ExportedName);
                            templateBuilder.AppendLine(pathDoc);
                        }
                    }
                }


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
        private List<DotNetApiLambdaProject> GetLambdaProjects(Api directive, Directives directives)
        {
            var projects = new List<DotNetApiLambdaProject>();
            foreach (var containerName in directive.Containers)
            {
                var container = (Container)directives[containerName];
                foreach (var artifact in container.Artifacts.Values.Where(x => x is AwsHttpApiResource))
                {
                    projects.Add((DotNetApiLambdaProject)artifact);
                }
            }
            return projects;
        }
    }
}
