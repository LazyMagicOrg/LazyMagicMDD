using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;

namespace LazyMagic
{
    public class AwsCognitoStack : ArtifactBase
    {
        public string UserPoolName { get; set; } = null;    
        public string StackName { get; set; } = null;
        public string ExportedStackName { get; set; } = null;   
        public string ExportedTemplatePath { get; set; } = null;  
        public string ExportedUserPoolName { get; set; } = null;    

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            Authentication directive = (Authentication)directiveArg;    

            // set the stack name 
            var stackName = StackName ?? directive.Key;
            stackName += NameSuffix; // usually nothing 
            await InfoAsync($"Generating {directive.Key} {stackName}");
            var userPoolName = UserPoolName ?? stackName;
            var templatePath = Template ?? "AWSTemplates/sam.service.cognito.jwt.managed.yaml";    

            // There is no processing necessary for this artifact.

            // Exports
            ExportedName = stackName;
            ExportedStackName = stackName;
            ExportedTemplatePath = templatePath;
            ExportedUserPoolName = userPoolName;
            
        }
    }
}
