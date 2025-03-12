using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Routing.Template;

namespace LazyMagic
{

    public class AwsSQSResource : ArtifactBase
    {
        public override string Template { get; set; } = "AWSTemplates/Snippets/sam.service.messaging.sqs.yaml";
        public string ExportedContainerKey { get; set; } = null;
        public string ExportedAwsResourceDefinition { get; set; } = "";
        public string ExportedAwsResourceName { get; set; } = "";   
        public int VisibilityTimeout { get; set; } = 180;
        public int MessageRetentionPeriod { get; set; } = 345600; // 4 days
        public int DelaySeconds { get; set; } = 0;
        public int DlqMaxReceiveCount { get; set; } = 500;
        public int DlqMessageRetentionPeriod { get; set; } = 345600; // 4 days

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var queueName = directiveArg.Key + NameSuffix ?? "";
            string directiveKey = "";
            try
            {
                Queue directive = (Queue)directiveArg;
                // Set the project name and namespace
                
                var errMsgPrefix = $"Error generating {GetType().Name}: {queueName}";
                directiveKey = directive.Key;
                await InfoAsync($"Generating {directive.Key}Resource for {queueName}");

                // Get the template and replace __tokens__
                var template = Template;
                var templateText = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template));

                templateText = templateText
                    .Replace("__ResourceGenerator__", this.GetType().Name)
                    .Replace("__TemplateSource__",Template)
                    .Replace("__QueueName__", queueName)
                    .Replace("__VisibilityTimeout__", VisibilityTimeout.ToString())
                    .Replace("__MessageRetentionPeriod__", MessageRetentionPeriod.ToString())
                    .Replace("__DelaySeconds__", DelaySeconds.ToString())
                    .Replace("__DlqMaxReceiveCount__", DlqMaxReceiveCount.ToString())
                    .Replace("__DlqMessageRetentionPeriod__", DlqMessageRetentionPeriod.ToString())
                    ;

                // Exports
                ExportedContainerKey = directive.Key;
                ExportedAwsResourceName = queueName;
                ExportedAwsResourceDefinition = templateText;

            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} for {queueName}, {ex.Message}");
            }

        }
    }
}
