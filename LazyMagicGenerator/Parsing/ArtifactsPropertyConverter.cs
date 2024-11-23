using System;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using System.Collections.Generic;
//using YamlDotNet.Core.Tokens;

namespace LazyMagic
{
    public class ArtifactsPropertyConverter : YamlTypeConverterBase, IYamlTypeConverter
    {

        /// <summary>
        /// ArtifactTypes must all derive from ArtifactBase
        /// </summary>

        public ArtifactsPropertyConverter() { }
        public bool Accepts(Type type)
        {
            return type == typeof(Artifacts);
        }

        /// <summary>
        /// The Artifacts MappingNode contains zero or 
        /// more Artifact Properties. These artifact properties 
        /// can be one of:
        /// - A Scaler with no default value. This value indicates the type of the MappingNode
        /// - A MappingNode with the MappingNode key indicating
        /// the type of the Mapping Node and the values being properties for that type.
        /// </summary>
        /// <param name="parser"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public object ReadYaml(IParser parser, Type type)
        {
            var artifactTypeName = "unknown";
            try
            {
                var artifacts = new Artifacts();
                parser.Consume<MappingStart>(); // start of artifacts section

                while (!(parser.Current is MappingEnd))
                {
                    artifactTypeName = parser.Consume<Scalar>().Value; // Artifact
                    var artifactType = Type.GetType($"LazyMagic.{artifactTypeName}");
                    switch (parser.Current)
                    {

                        case MappingStart _:
                            var artifact = (ArtifactBase)new ArtifactPropertyConverter().ReadYaml(parser, artifactType);
                            artifacts.Add(artifact.GetType().Name,artifact);
                            break;

                        case Scalar _:
                            var value = ConsumeYamlNode(parser); // throw away value
                            artifacts.Add(artifactTypeName, (ArtifactBase)Activator.CreateInstance(artifactType));
                            break;

                        default:
                            throw new Exception("Invalid Artifact");
                    }
                }

                parser.Consume<MappingEnd>(); // end of list
                return artifacts;
            }
            catch (Exception ex)
            {
                var sep = ex.Message.StartsWith(".") ? "" : " ";
                var msg = $"{artifactTypeName}{sep}{ex.Message}";
                LzLogger.Info(msg);
                throw new Exception(msg);
            }
        }

    }
}
