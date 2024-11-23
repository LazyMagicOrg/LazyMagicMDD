using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Core.Events;

namespace LazyMagic
{
    public class DirectivePropertyConverter : YamlTypeConverterBase, IYamlTypeConverter
    {
        public DirectivePropertyConverter() { }

        private Dictionary<string, Type> DirectiveTypes = new Dictionary<string, Type> 
        {
            {"Schema", typeof(Schema)},
            {"Module", typeof(Module)},
            //{"Authorization", typeof(Authorization)},
            {"Container", typeof(Container)},
            {"Authentication", typeof(Authentication)},
            {"Api", typeof(Api)},
            {"Service", typeof(Service)},
            {"WebApp", typeof(WebApp)},
            {"Tenancy", typeof(Tenancy)},
            {"Deployment", typeof(Deployment)},
            {"Queue", typeof(Queue)},
        };

        public bool Accepts(Type type)
        {
            return type == typeof(DirectiveBase);
        }

        public object ReadYaml(IParser parser, Type typeArg)
        {
            var mappingNode = new YamlMappingNode();
            var key = "";

            try
            {
                parser.Consume<MappingStart>();  // Consume the MappingStart event for the Directive
                YamlNode value = null;
                string type = null;
                Artifacts artifacts = new Artifacts();
                while (!(parser.Current is MappingEnd))
                {
                    key = ConsumeYamlNode(parser).ToString();
                    switch (key)
                    {
                        case "Artifacts":
                            artifacts = (Artifacts) new ArtifactsPropertyConverter().ReadYaml(parser, typeof(Artifacts));
                            break;
                        case "Type":
                            type = ConsumeTypeNode(parser);
                            if (!DirectiveTypes.ContainsKey(type)) throw new Exception($" = {type} Unknown Type.");
                            mappingNode.Add(key, type);
                            break;
                        default:
                            value = ConsumeYamlNode(parser);
                            mappingNode.Add(key, value);
                            break;
                    }
                    key = null;
                }
                if (type == null) throw new Exception(" Type property is required.");
                parser.Consume<MappingEnd>();  // Consume the MappingEnd event
                // The mapping node contains everything except the Artifacts
                var directiveString = serializer.Serialize(mappingNode);
                var directive = (DirectiveBase)deserializer.Deserialize(directiveString, DirectiveTypes[type]);
                // Assign the Directive.Artifacts property
                directive.Artifacts = artifacts;
                return directive;

            }
            catch (Exception ex)
            {
                var sep = ex.Message.StartsWith(".") ? "" : " ";
                var msg = $".{key}{sep}{ex.Message}";
                throw new Exception(msg);
            }
        }
    }
}
