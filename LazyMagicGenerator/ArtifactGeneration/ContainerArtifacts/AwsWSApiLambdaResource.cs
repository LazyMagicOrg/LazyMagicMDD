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
    public class AwsWSApiLambdaResource : ArtifactBase
    {
        public override string Template { get; set; } = "AWSTemplates/Snippets/sam.service.messaging.wsapilambda.yaml";
        public string ExportedContainerKey { get; set; } = null;
        public string ExportedAwsResourceDefinition { get; set; } = "";
        public string ExportedAwsResourceName { get; set; } = "";
        public int MemorySize { get; set; } = 256;
        public int Timeout { get; set; } = 30;
        public string Tracing { get; set; } = "Active";
        public string Runtime { get; set; } = "dotnet8";
        public string DotNetTarget { get; set; } = "net8.0";
        public string CognitoPolicyTemplate { get; set; } = "AWSTemplates/Snippets/sam.service.messaging.wsapilambda.cognitopolicy.yaml";    
        public List<string> Authentications { get; set; } = new List<string>();
        public bool AuthenticationRequired { get; set; } = true;
        public string WebSocketApi { get; set; } = "";

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var lambdaName = "";
            string errMsgPrefix = $"Error generating {GetType().Name}:";
            try
            {
                Container directive = (Container)directiveArg;

                // Set the project name and namespace
                lambdaName = directive.Key + NameSuffix ?? "";
                await InfoAsync($"Generating {directive.Key}Resource for {lambdaName}");
                string cognitopolicy = "";
                if (AuthenticationRequired)
                {
                    cognitopolicy = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, CognitoPolicyTemplate));
                    cognitopolicy.Replace("__TemplateSource__", CognitoPolicyTemplate);
                    if (Authentications.Count == 0)
                        throw new Exception($"{errMsgPrefix} {lambdaName}, Authentication == true but no Authentication services specified.");
                    string userpools = "";
                    foreach (var userpool in Authentications)
                        userpools += $@"            - !Ref {userpool}UserPoolArnParameter
";
                    cognitopolicy = cognitopolicy.Replace("#UserPoolArns#", userpools);
                }

                // Get the DotNetLambdaProject Artifact. There should only be one.
                var dotNetLambdaProject = directive.Artifacts.Values
                    .Where(x => x is DotNetWSApiLambdaProject)
                    .FirstOrDefault() as DotNetProjectBase;   

                if(dotNetLambdaProject == null) 
                    throw new Exception($" {errMsgPrefix} DotNetLambda not found.");
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
                    .Replace("#CognitoUserPoolsReadPolicy#", cognitopolicy)
                    .Replace("__WebSocketApi__",WebSocketApi)
                    .Replace("__AuthenticationRequired__", AuthenticationRequired.ToString().ToLower())
                    .Replace("__LambdaName__", lambdaName)
                    .Replace("__OutputDir__", outputFolder);

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
