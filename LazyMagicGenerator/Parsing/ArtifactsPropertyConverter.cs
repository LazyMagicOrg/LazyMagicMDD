using System;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace LazyMagic
{
    public class ArtifactsPropertyConverter: YamlTypeConverterBase, IYamlTypeConverter
    {
        public ArtifactsPropertyConverter() { }   
        public bool Accepts(Type type)
        {
            return type == typeof(Artifacts);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            string key = "";
            try
            {
                var artifacts = new Artifacts();
                parser.Consume<MappingStart>(); // start of Directives
                while (!(parser.Current is MappingEnd))
                {
                    key = parser.Consume<Scalar>().Value; // Directive name
                    switch (parser.Current)
                    {
                        case MappingStart _:
                            var artifact = (ArtifactBase)new ArtifactPropertyConverter().ReadYaml(parser, typeof(ArtifactBase));
                            artifact.Key = key;
                            artifacts.Add(key, artifact);
                            break;
                        default:
                            throw new Exception(" Not an object.");
                    }
                }
                parser.Consume<MappingEnd>(); // end of Artifacts
                return artifacts;
            }
            catch (Exception ex)
            {
                var sep = ex.Message.StartsWith(".") ? "" : " ";
                var msg = $"{key}{sep}{ex.Message}";
                LzLogger.Info(msg);
                throw new Exception(msg);
            }
        }


    }
}
