﻿using System.Threading.Tasks;
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
    public class AwsWSApiLambdaResource : AwsApiLambdaResource
    {

        public List<string> Authentications { get; set; } = new List<string>();
        public bool AuthenticationRequired { get; set; }
        public string WebScoketApi { get; set; }
        public string WebSocketApi { get; set; } = "";

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var lambdaName = "";
            string directiveKey = "";
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
                    cognitopolicy = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates/Snippets/sam.service.messaging.wsapilambda.cognitopolicy.yaml"));
                    if (Authentications.Count == 0)
                        throw new Exception($"{errMsgPrefix} {lambdaName}, Authentication == true but no Authentication services specified.");
                    string userpools = "";
                    foreach (var userpool in Authentications)
                        userpools += $@"            - !GetAtt {userpool}.Arn
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
                var template = Template ?? "AWSTemplates/Snippets/sam.service.messaging.wslambda.yaml";
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
                    .Replace("#CognitoUserPoolsReadPolicy#", cognitopolicy)
                    .Replace("__AuthenticationRequired__", AuthenticationRequired.ToString().ToLower())
                    .Replace("__LambdaName__", lambdaName)
                    .Replace("__OutputDir__", outputFolder);

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