using System.Threading.Tasks;
using YamlDotNet.Serialization; 

namespace LazyMagic
{
    public class ArtifactBase
    {
        public virtual string Key { get; set; } = "";
        public virtual string Family { get; set; } = "";
        public virtual string Type { get; set; } = "";
        public virtual string Template { get; set; }
        public virtual string OutputFolder { get; set; } 
        public virtual string NameSuffix { get; set; } 
        public virtual string ExportedName { get; set; } 
        [YamlIgnore]
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
