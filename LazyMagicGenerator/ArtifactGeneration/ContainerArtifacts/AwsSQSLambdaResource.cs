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
    /// Generate a AWS::Serverless::Function resource suitable for 
    /// inclusion in an AWS SAM template. The resource is in 
    /// yaml format and available in the ExportedAwsResourceDefinition string property.
    /// </summary>
    public class AwsSQSLambdaResource : AwsApiLambdaResource
    {
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

                // Set the project name and namespace
                lambdaName = directive.Key + NameSuffix ?? "";
                var errMsgPrefix = $"Error generating {GetType().Name}: {lambdaName}";
                directiveKey = directive.Key;
                await InfoAsync($"Generating {directive.Key}Resource for {lambdaName}");

                // Get the DotNetLambdaProject Artifact. There should only be one.
                var dotNetLambdaProject = directive.Artifacts.Values
                    .Where(x => x is DotNetProject)
                    .FirstOrDefault() as DotNetProjectBase;   
                if(dotNetLambdaProject == null) 
                    throw new Exception($"{errMsgPrefix}, DotNet not found.");
                var outputFolder = dotNetLambdaProject.OutputFolder;

                // Get the template and replace __tokens__
                var template = Template ?? "AWSTemplates/Snippets/sam.service.messaging.sqslambda.yaml";
                var templateText = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template));
                
                templateText = templateText
                    .Replace("__LambdaName__", lambdaName)
                    .Replace("__OutputDir__", outputFolder)
                    .Replace("__MemorySize__", MemorySize.ToString())
                    .Replace("__Timeout__", Timeout.ToString())
                    .Replace("__Tracing__", Tracing)
                    .Replace("__DotNetTarget__", DotNetTarget)
                    .Replace("__Runtime__", Runtime)
                    // derived class specific properties
                    .Replace("__SQSQeue__", Queue)
                    .Replace("__WebSocketApi", WebSocketApi);

                // Exports
                ExportedContainerKey = directive.Key;
                ExportedName = lambdaName;
                ExportedAwsResourceName = lambdaName;
                ExportedAwsResourceDefinition = templateText;

            } catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} for {lambdaName}, {ex.Message}");
            }   

        }
    }
}
