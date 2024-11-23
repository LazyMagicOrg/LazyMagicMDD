using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;
using Microsoft.AspNetCore.Routing.Template;

namespace LazyMagic
{
    public class AwsDeploymentStackTemplate : ArtifactBase
    {
        public override string Template { get; set; } = "AWSTemplates/sam.service.deployment.yaml";
        public string ExportedStackName { get; set; } = null;
        public string ExportedTemplatePath { get; set; } = null;

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            Deployment directive = (Deployment)directiveArg;

            // set the stack name 
            var stackName = directive.Key + NameSuffix ?? "";
            await InfoAsync($"Generating {directive.Key} {stackName}");

            // Get the template and replace __tokens__
            var template = Template;
            template
                .Replace("__ResourceGenerator__", this.GetType().Name)
                .Replace("__TemplateSource__",Template);

            // Exports
            ExportedStackName = stackName;
            ExportedTemplatePath = template;
        }
    }
}
