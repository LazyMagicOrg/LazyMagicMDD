using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

namespace LazyMagic
{
    public class DirectiveBase
    {
        public DirectiveBase() { 

        }
       
        protected bool defaultsAssigned = false;    
        protected bool validated = false;

        #region Properties
        public string Key { get; set; } = "";
        public string Type { get; set; } = "";
        public bool IsDefault { get; set; } = false; public string Defaults { get; set; }
        public Artifacts Artifacts { get; set; } 
        #endregion


        public virtual void AssignDefaults(Directives directives) => defaultsAssigned = true;
        public virtual void Validate(Directives directives) => validated = true;
        public virtual void AssignDefaults(Directives directives, Type type)
        {
            if (IsDefault) return; // Do not assign defaults to defaults
            if (string.IsNullOrEmpty(Defaults)) return; // no defaults to assign


            if (directives.TryGetValue(Defaults, out var defaultDirective))
            {
                var deserializer = new DeserializerBuilder()
                       .WithTypeConverter(new DirectivePropertyConverter())
                       .WithTypeConverter(new ArtifactsPropertyConverter())
                       .WithTypeConverter(new ArtifactPropertyConverter())
                       .WithNodeDeserializer(inner => new DetailedErrorNodeDeserializer(inner), s => s.InsteadOf<ObjectNodeDeserializer>())
                       .Build();

                var serializer = new SerializerBuilder()
                    .Build();


                // Use Newtonsoft.Json to merge the default directive with this directive
                JObject defaultObject = JObject.FromObject(defaultDirective);
                JObject thisObject = JObject.FromObject(this);
                defaultObject.Merge(thisObject, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Union,
                    MergeNullValueHandling = MergeNullValueHandling.Ignore,
                });
                var directiveJson = defaultObject.ToString();
                var directiveObject = JsonConvert.DeserializeObject(directiveJson, type);
                var yaml = serializer.Serialize(directiveObject);

                using (var reader = new StringReader(yaml))
                {
                    var newDirective = deserializer.Deserialize(reader, type);
                    directives[Key] = (DirectiveBase)newDirective;
                }
                return;
            }
            else
                throw new Exception($"{Key}.Defaults={Defaults}, referenced default not found.");
        }
        public virtual async Task GenerateAsync(SolutionBase solution)
        {
            foreach (var artifact in Artifacts.Values)
            {
                await artifact.GenerateAsync(solution, this);
            }
        }

    }


}