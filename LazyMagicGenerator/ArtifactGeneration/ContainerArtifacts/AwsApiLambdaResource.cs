using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using Microsoft.AspNetCore.Routing.Template;

namespace LazyMagic
{
    /// <summary>
    /// Generate a AWS::Serverless::Function resource suitable for 
    /// inclusion in an AWS SAM template. The resource is in 
    /// yaml format and available in the ExportedAwsResourceDefinition string property.
    /// </summary>
    public class AwsApiLambdaResource : ArtifactBase
    {
        public override string Template { get; set; } = "AWSTemplates/Snippets/sam.service.apilambda.yaml";
        public string ExportedContainerKey { get; set; } = null;
        public string ExportedAwsResourceDefinition { get; set; } = "";
        public string ExportedAwsResourceName { get; set; } = "";
        public int MemorySize { get; set; } = 256;
        public int Timeout { get; set; } = 30;
        public string Tracing { get; set; } = "Active";
        public string Runtime { get; set; } = "dotnet8";
        public string DotNetTarget { get; set; } = "net8.0";

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var lambdaName = "";
            string directiveKey = "";
            try
            {
                Container directive = (Container)directiveArg;

                // Set the project name and namespace
                lambdaName = directive.Key + NameSuffix ?? "";
                var errMsgPrefix = $"Error generating {GetType().Name}: {lambdaName}";
                directiveKey = directive.Key;
                await InfoAsync($"Generating {directive.Key}Resource for {lambdaName}");

                // Get the DotNetLambdaProject Artifact. There should only be one.
                var dotNetLambdaProject = directive.Artifacts
                    .Values
                    .Where(x => x is DotNetApiLambdaProject)
                    .FirstOrDefault() as DotNetProjectBase;   
                if(dotNetLambdaProject == null) 
                    throw new Exception($"{errMsgPrefix}, DotNetLambda not found.");
                var outputFolder = dotNetLambdaProject.OutputFolder;

                // Get the template and replace __tokens__
                var template = Template;
                var templateText = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template));
                
                templateText = templateText
                    .Replace("__ResourceGenerator__", this.GetType().Name)
                    .Replace("__TemplateSource__",Template)
                    .Replace("__LambdaName__", lambdaName)
                    .Replace("__OutputDir__", outputFolder)
                    .Replace("__MemorySize__", MemorySize.ToString())
                    .Replace("__Timeout__", Timeout.ToString())
                    .Replace("__Tracing__", Tracing)
                    .Replace("__DotNetTarget__", DotNetTarget)
                    .Replace("__Runtime__", Runtime)
                    ;

                // Exports
                ExportedContainerKey = directive.Key;
                ExportedAwsResourceName = lambdaName;
                ExportedAwsResourceDefinition = templateText;

            } catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} for {lambdaName}, {ex.Message}");
            }   

        }
    }
}
