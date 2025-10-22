using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using static LazyMagic.LzLogger;

namespace LazyMagic
{
    /// <summary>
    /// AWS Module-level resource artifact that exports ManagedPolicyArns and EventsApis
    /// for aggregation by Container artifacts (e.g., AwsAppRunnerResource).
    /// This artifact does NOT generate CloudFormation templates - it only
    /// exports metadata about the module's AWS resource requirements.
    /// </summary>
    public class AwsModuleResource : AwsResourceArtifact
    {
        // Input: ManagedPolicyArns configured in YAML at module level
        public List<string> ManagedPolicyArns { get; set; } = new List<string>();

        // Export: ManagedPolicyArns available for Container aggregation
        public List<string> ExportedManagedPolicyArns { get; set; } = new List<string>();

        // Input: EventsApis configured in YAML at module level
        public List<string> EventsApis { get; set; } = new List<string>();

        // Export: EventsApis available for Container aggregation
        public List<string> ExportedEventsApis { get; set; } = new List<string>();

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var moduleName = "";
            try
            {
                await Task.Delay(0);
                Module directive = (Module)directiveArg;
                moduleName = directive.Key;

                Info($"Processing AWS module resource for {moduleName}");

                // Export the ManagedPolicyArns for Container aggregation
                ExportedManagedPolicyArns = new List<string>(ManagedPolicyArns);

                // Export the EventsApis for Container aggregation
                ExportedEventsApis = new List<string>(EventsApis);

                Info($"Exported {ExportedManagedPolicyArns.Count} ManagedPolicyArn(s) and {ExportedEventsApis.Count} EventsApi(s) from module {moduleName}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsModuleResource)}: {moduleName}, {ex.Message}");
            }
        }
    }
}
