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

        public static string GenerateQueueResources(SolutionBase solution, DirectiveBase directiveArg)
        {
            try
            {

            } catch (Exception ex)
            {
                throw new Exception($"Error generating {nameof(AwsSQSResources)} (#LzQeuues#): {ex.Message}");
            }
            return "";
        }
        
    }
}
