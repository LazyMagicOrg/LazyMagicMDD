using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;

namespace LazyMagic
{
    /// <summary>
    /// Generate a AWS::Cognito::UserPool resource suitable for 
    /// inclusion in an AWS SAM template. The resource is in 
    /// yaml format and available in the ExportedAwsResourceDefinition string property.
    /// </summary>
    public class AwsCognitoResource : ArtifactBase
    {
        public int SecurityLevel { get; set; } = 1; // defaults to JWT
        public string ExportedAwsResourceDefinition { get; set; } = null;   
        public string ExportedAwsResourceName { get; set; } = null;    
        public int ExportedSecurityLevel { get; set; }

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            Authentication directive = (Authentication)directiveArg;    

            // set the stack name 
            var resourceName = directive.Key + NameSuffix ?? "";
            await InfoAsync($"Generating {directive.Key} {resourceName}");
            var templatePath = Template ?? "AWSTemplates/sam.service.cognito.jwt.managed.yaml";    

            var template = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, templatePath));
            template = template.Replace("__CognitoResource__", resourceName);

            // Exports
            ExportedName = resourceName;
            ExportedAwsResourceName = resourceName;
            ExportedAwsResourceDefinition = template;
            ExportedSecurityLevel = SecurityLevel;
            
        }
    }
}
