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
    public class AwsSQSLambdaResource : AwsApiLambdaResource
    {
        public override string Template { get; set; } = "AWSTemplates/Snippets/sam.service.messaging.sqslambda.yaml";
        public string Queue{ get; set; } = "";
        public int BatchSize { get; set; } = 10;
        public int MaximumBatchingWindowInSeconds { get; set; } = 2;
        public string WebSocketApi { get; set; } = "";

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var lambdaName = "";
            string directiveKey = "";
            try
            {
                Container directive = (Container)directiveArg;

                // Check for required properties
                if(string.IsNullOrEmpty(Queue))
                    throw new Exception($"Error generating {GetType().Name}: Queue is required.");
                if (string.IsNullOrEmpty(WebSocketApi))
                    throw new Exception($"Error generating {GetType().Name}: WebSocketApi is required.");

                // Set the project name and namespace
                lambdaName = directive.Key + NameSuffix ?? "";
                var errMsgPrefix = $"Error generating {GetType().Name}: {lambdaName}";
                directiveKey = directive.Key;
                await InfoAsync($"Generating {directive.Key}Resource for {lambdaName}");

                // Get the DotNetLambdaProject Artifact. There should only be one.
                var dotNetLambdaProject = directive.Artifacts.Values
                    .Where(x => x is DotNetSQSLambdaProject)
                    .FirstOrDefault() as DotNetProjectBase;   
                if(dotNetLambdaProject == null) 
                    throw new Exception($"{errMsgPrefix}, DotNet not found.");
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
                    // derived class specific properties
                    .Replace("__BatchSize__", BatchSize.ToString())
                    .Replace("__MaximumBatchingWindowInSeconds__", MaximumBatchingWindowInSeconds.ToString())
                    .Replace("__SQSQueue__", Queue)
                    .Replace("__WebSocketApi__", WebSocketApi);

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
