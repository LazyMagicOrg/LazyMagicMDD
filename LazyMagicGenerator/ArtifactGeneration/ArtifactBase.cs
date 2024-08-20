using System.Threading.Tasks;
using YamlDotNet.Serialization; 

namespace LazyMagic
{
    public class ArtifactBase
    {
        public string Key { get; set; } = "";
        public virtual string Family { get; set; } = "";
        public string Type { get; set; } = "";
        public string Template { get; set; }
        public string OutputFolder { get; set; } 
        public string NameSuffix { get; set; } 
        public string ExportedName { get; set; } 
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
