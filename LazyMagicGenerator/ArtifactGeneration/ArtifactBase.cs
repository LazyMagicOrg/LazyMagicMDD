using Amazon.Util.Internal;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization; 

namespace LazyMagic
{
    public class ArtifactBase
    {
        public virtual string ProjectTemplatesFolder { get; set; } = "ProjectTemplates";
        public virtual string Template { get; set; }
        
        /// <summary>
        /// Gets the full template path by combining ProjectTemplatesFolder with Template.
        /// </summary>
        protected virtual string TemplatePath => 
            string.IsNullOrEmpty(Template) ? "" : 
            string.IsNullOrEmpty(ProjectTemplatesFolder) ? Template : 
            Path.Combine(ProjectTemplatesFolder, Template);
            
        public virtual string OutputFolder { get; set; } 
        public virtual string NameSuffix { get; set; } 
        public virtual string ExportedName { get; set; }

        //[YamlIgnore]
        public virtual string ProjectFilePath { get; set;  } = "";
        public virtual void AssignDefaults(ArtifactBase artifactBase)
        {
            ;
        }

        public virtual void Validate(ArtifactBase artifactBase)
        {
            ;
        }

        public async virtual Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            await Task.Delay(0);
        }
    }
}
