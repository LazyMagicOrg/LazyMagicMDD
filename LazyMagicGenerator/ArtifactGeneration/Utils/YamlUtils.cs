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
            // Convert json to yaml
            dynamic expandoObject = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);
            var serializer = new SerializerBuilder()
                                //.DisableAliases()
                                .Build();
            var yamlContent = serializer.Serialize(expandoObject);

            // 3. ParseAndAdd the YAML string into YamlDotNet representation
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(yamlContent));

            // 4. Traverse the YAML nodes and quote the mapping keys
            QuoteMappingKeys(yamlStream.Documents[0].RootNode);

            // 5. Serialize the modified nodes back to a YAML string
            var writer = new StringWriter();
            yamlStream.Save(writer, false);

            return writer.ToString();
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