using System.Collections.Generic;

namespace LazyMagic
{
    /// <summary>
    /// Each AwsResourceArtifacts instance represents a set of AWS CloudFormation 
    /// resource template content. 
    /// </summary>
    public class AwsResourceArtifact : ArtifactBase
    {
        // StackParameters are the parameters required by this resource 
        public List<string> StackParameters { get; set; } = new List<string>();
        // StackResource is the actual resource definition snippet filled out from the template
        public string StackResource { get; set; } = "";
        // StackOutputs are the outputs provided by this resource to be included in the overall stack outputs
        public List<string> StackOutputs { get; set; } = new List<string>();
    }
}


