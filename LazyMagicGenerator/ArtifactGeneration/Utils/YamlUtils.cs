using Newtonsoft.Json;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace LazyMagic
{
    public static class YamlUtil
    {
        public static string ConvertJsonToYaml(string jsonText)
        {
            dynamic expandoObject = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);
            var serializer = new SerializerBuilder().Build();
            var yamlContent = serializer.Serialize(expandoObject);
            var yamlStream = new YamlStream();

            using (var reader = new StringReader(yamlContent))
            using (var writer = new StringWriter())
            {
                yamlStream.Load(reader);
                QuoteMappingKeys(yamlStream.Documents[0].RootNode);
                yamlStream.Save(writer, false);
                return writer.ToString();
            }
        }

        private static void QuoteMappingKeys(YamlNode node)
        {
            if (node is YamlMappingNode mapping)
            {
                var keysToReplace = new List<YamlNode>();

                foreach (var entry in mapping.Children)
                {
                    // For each key in the mapping, quote it
                    if (entry.Key.NodeType == YamlNodeType.Scalar)
                    {
                        var scalarKey = (YamlScalarNode)entry.Key;
                        if (scalarKey.Value.Contains("{") && !scalarKey.Value.StartsWith("'"))
                        {
                            keysToReplace.Add(entry.Key);
                        }
                    }

                    // Recursively process the value
                    QuoteMappingKeys(entry.Value);
                }

                foreach (var key in keysToReplace)
                {
                    var value = mapping.Children[key];
                    mapping.Children.Remove(key);

                    // We'll use the ScalarStyle.Plain which means no additional quotes are added
                    var newKey = new YamlScalarNode(key.ToString()) { Style = ScalarStyle.SingleQuoted };
                    newKey.Value = newKey.Value; 
                    mapping.Children[newKey] = value;
                }
            }
            else if (node is YamlSequenceNode sequence)
            {
                foreach (var child in sequence.Children)
                {
                    QuoteMappingKeys(child);
                }
            }
        }
    }
}