using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using static LazyMagic.ArtifactUtils;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LazyMagic
{
    /// <summary>
    /// Generate the #LzQueues# content by aggregating all the 
    /// previously generated AWS::SQS::Queue  resources.
    /// </summary>
    public static class AwsSQSResources
    {
        public static string GenerateSQSResources(SolutionBase solution, DirectiveBase serviceDirectiveArg)
        {
            var resourceBuilder = new StringBuilder();
            try
            {
                // Get all the SQS resources
                var queues = GetSQSResources(solution, serviceDirectiveArg as Service);
                foreach (var queue in queues)
                {
                    resourceBuilder.Append(queue.ExportedAwsResourceDefinition);
                    resourceBuilder.AppendLine();
                }

                // Get all the SQS Lambda resources
                var lambdas = GetSQSLambdaResources(solution, serviceDirectiveArg as Service);
                foreach (var lambda in lambdas)
                {
                    resourceBuilder.Append(lambda.ExportedAwsResourceDefinition);
                    resourceBuilder.AppendLine();   
                }
            } catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsSQSResources)} (#LzQeuues#): {ex.Message}");
            }
            return resourceBuilder.ToString();
        }
        
        private static List<AwsSQSResource> GetSQSResources(SolutionBase solution, Service directive) =>
            directive.Queues
                .Select(k => (Queue)solution.Directives[k])
                .SelectMany(c => c.Artifacts.Values)
                .OfType<AwsSQSResource>()
                .Distinct()
                .ToList();
        
        private static List<AwsSQSLambdaResource> GetSQSLambdaResources(SolutionBase solution, Service directive) =>
            directive.Queues
                .Select(k => (Queue)solution.Directives[k])
                .SelectMany(api => api.Containers)
                .Select(cn => (Container)solution.Directives[cn])
                .Where(c => c.IsDefault == false)
                .SelectMany(c => c.Artifacts.Values)
                .OfType<AwsSQSLambdaResource>()
                .Distinct()
                .ToList();
    }
}
