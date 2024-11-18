using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using Newtonsoft.Json;

namespace LazyMagic
{

    public class AwsSQSResource : ArtifactBase
    {
        public string ExportedContainerKey { get; set; } = null;
        public string ExportedAwsResourceDefinition { get; set; } = "";
        public string ExportedAwsResourceName { get; set; } = "";   
        public int VisibilityTimeout { get; set; } = 180;
        public int MessageRetentionPeriod { get; set; } = 345600; // 4 days

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
                var template = Template ?? "AWSTemplates/Snippets/sam.service.messaging.sqs.yaml";
                var templateText = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, template));

                templateText = templateText
                    .Replace("__QueueName__", queueName)
                    .Replace("__VisibilityTimeout__", VisibilityTimeout.ToString())
                    .Replace("__MessageRetentionPeriod__", MessageRetentionPeriod.ToString());

                // Exports
                ExportedContainerKey = directive.Key;
                ExportedName = queueName;
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
