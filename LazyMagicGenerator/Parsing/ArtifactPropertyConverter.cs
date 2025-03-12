using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Core.Events;
using System.Xml.Linq;
using System.Linq;
using YamlDotNet.Core.Tokens;
using System.Runtime.InteropServices.ComTypes;

namespace LazyMagic
{
    public class ArtifactPropertyConverter : YamlTypeConverterBase, IYamlTypeConverter
    {
        public ArtifactPropertyConverter() { }

        public bool Accepts(Type type)
        {
            return type == typeof(ArtifactBase);
        }

        /// <summary>
        /// This custom converter reads the Artifact property from the YAML file and
        /// calls the Parse method of the Artifact class to parse the Artifact property
        /// into the artifactTypeName specified in the Artifact.Type property. 
        /// </summary>
        /// <param name="parser"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public object ReadYaml(IParser parser, Type typeArg)
        {

            var mappingNode = new YamlMappingNode();
            var artifactTypeName = "unknown";

            try
            {
                parser.Consume<MappingStart>();
                while (!(parser.Current is MappingEnd))
                {
                    var key = ConsumeYamlNode(parser).ToString();
                    var value = ConsumeYamlNode(parser);
                    mappingNode.Add(key, value);
                }
                parser.Consume<MappingEnd>();  // Consume the MappingEnd event
                var artifactString = serializer.Serialize(mappingNode);
                var artifact = (ArtifactBase)deserializer.Deserialize(artifactString, typeArg);
                return artifact;

            }
            catch (Exception ex)
            {
                var sep = ex.Message.StartsWith(".") ? "" : " ";
                var msg = $".{artifactTypeName}{sep}{ex.Message}";
                throw new Exception(msg);
            }
        }
    }
}
