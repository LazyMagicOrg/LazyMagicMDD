using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;

namespace LazyMagic
{
  
    public class AwsCognitoResource : ArtifactBase
    {
        public int SecurityLevel { get; set; } = 1; // defaults to JWT
        public string ExportedResource { get; set; } = null;   
        public string ExportedResourceName { get; set; } = null;    
        public int ExportedSecurityLevel { get; set; }

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            Authentication directive = (Authentication)directiveArg;    

            // set the stack name 
            var resourceName = directive.Key;
            await InfoAsync($"Generating {directive.Key} {resourceName}");
            var templatePath = Template ?? "AWSTemplates/sam.service.cognito.jwt.managed.yaml";    

            var template = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, templatePath));
            template = template.Replace("__CognitoResource__", resourceName);

            // Exports
            ExportedName = resourceName;
            ExportedResourceName = resourceName;
            ExportedResource = template;
            ExportedSecurityLevel = SecurityLevel;
            
        }
    }
}
