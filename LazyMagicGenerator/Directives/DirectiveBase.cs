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

                var serializer = new SerializerBuilder()
                    .JsonCompatible()  // This enables JSON.NET compatibility
                    .Build();

                // Use Newtonsoft.Json to merge the default directive with this directive
                JObject defaultObject = JObject.FromObject(defaultDirective);
                var defaultObjectJson = defaultObject.ToString();
                JObject thisObject = JObject.FromObject(this);
                var thisObjectJson = thisObject.ToString();
                defaultObject.Merge(thisObject, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Union,
                    MergeNullValueHandling = MergeNullValueHandling.Ignore,
                });
                var mergedObjectJson = defaultObject.ToString();

                // Deserialization to a yaml object
                var yamlObject = new DeserializerBuilder()
                    .Build()
                    .Deserialize(new StringReader(mergedObjectJson));

                // Create a yaml string from yaml object
                var yamlstring = new SerializerBuilder().Build().Serialize(yamlObject);

                // Use our property and artifact converters to serialize to a directive
                var newDirective = new DeserializerBuilder()
                    //.WithTypeConverter(new DirectivesPropertyConverter())
                    .WithTypeConverter(new DirectivePropertyConverter())
                    .WithTypeConverter(new ArtifactsPropertyConverter())
                    .WithTypeConverter(new ArtifactPropertyConverter())
                    .WithNodeDeserializer(inner => new DetailedErrorNodeDeserializer(inner), s => s.InsteadOf<ObjectNodeDeserializer>())
                    .Build()
                    .Deserialize(new StringReader(yamlstring), type);

                directives[Key] = (DirectiveBase)newDirective;

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