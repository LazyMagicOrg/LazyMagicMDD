using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;

namespace LazyMagic
{
    /// <summary>
    /// Generate an AWS Lambda resource for use in a SAM template.
    /// The template is in ExportedAwsResourceDefinition
    /// </summary>
    public class AwsLambdaResource : ArtifactBase
    {
        public string LambdaName { get; set; } = null;
        public string Resource1 { get; set; } = null;
        public string Resource2 { get; set; } = null;
        public string Resource3 { get; set; } = null;

        public string ExportedContainerKey { get; set; } = null;
        public string ExportedAwsResourceDefinition { get; set; } = "";

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var lambdaName = "";
            try
            {
                Container directive = (Container)directiveArg;

                // Set the project name and namespace
                lambdaName = LambdaName ?? directive.Key;
                lambdaName += NameSuffix ?? "";
                await InfoAsync($"Generating {directive.Key} {lambdaName}");

                // Get the DotNetLambdaProject Artifact. There should only be one.
                var dotNetLambdaProject = directive.Artifacts.Values.Where(x => x.Type == "DotNetLambda").FirstOrDefault() as DotNetLambdaProject;   
                if(dotNetLambdaProject == null) 
                    throw new Exception($"Error generating AwsServerlessFunction: {lambdaName}, DotNetLambdaProject not found.");
                var outputFolder = dotNetLambdaProject.OutputFolder;

                // Get the template and replace __tokens__
                var template = Template ?? "AWSTemplates/Snippets/sam.service.lambda.yaml";
                var templateText = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template));
                
                templateText = templateText
                    .Replace("__LambdaName__", lambdaName)
                    .Replace("__OutputDir__", outputFolder)
                    .Replace("__Resource1__", Resource1)
                    .Replace("__Resource2__", Resource2)
                    .Replace("__Resource3__", Resource3);

                // Exports
                ExportedContainerKey = directive.Key;
                ExportedName = lambdaName;
                ExportedAwsResourceDefinition = templateText;

            } catch (Exception ex)
            {
                throw new Exception($"Error generating AwsServerlessFunction: {lambdaName}, {ex.Message}");
            }   

        }
    }
}
