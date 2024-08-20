using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;

namespace LazyMagic
{
    public class AwsDeploymentStackTemplate : ArtifactBase
    {
        public string StackName { get; set; } = null;
        public string ExportedStackName { get; set; } = null;
        public string ExportedTemplatePath { get; set; } = null;

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            Deployment directive = (Deployment)directiveArg;

            // set the stack name 
            var stackName = StackName ?? directive.Key;
            stackName += NameSuffix; // usually nothing 
            await InfoAsync($"Generating {directive.Key} {stackName}");

            // Get the template and replace __tokens__
            var template = Template ?? "AWSTemplates/sam.service.deployment.yaml";

            // Exports
            ExportedName = stackName;
            ExportedStackName = stackName;
            ExportedTemplatePath = template;
        }
    }
}
