using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Core.Events;

namespace LazyMagic
{
    public class ArtifactPropertyConverter : YamlTypeConverterBase, IYamlTypeConverter
    {
        public ArtifactPropertyConverter() {}

        private Dictionary<string, Type> ArtifactTypes = new Dictionary<string, Type>
        {
            {"DotNetSchema", typeof(DotNetSchemaProject) },
            {"DotNetRepo", typeof(DotNetRepoProject) },
            {"DotNetController", typeof(DotNetControllerProject) },
            {"DotNetLambda", typeof(DotNetLambdaProject) },
            {"DotNetWebSocket", typeof(DotNetWebSocketProject) },   
            {"DotNetLocalWebApi", typeof(DotNetLocalWebApiProject) },
            {"DotNetAuthorization", typeof(DotNetAuthorizationProject) },
            {"DotNetSDK", typeof(DotNetSDKProject) },
            {"AwsLambdaResource", typeof(AwsLambdaResource) },
            {"AwsHttpApiResource", typeof(AwsHttpApiResource) },
            {"AwsWebSocketApiResource", typeof(AwsWebSocketApiResource) },
            {"AwsCognitoResource", typeof(AwsCognitoResource) },
            {"AwsServiceStackTemplate",typeof(AwsServiceStackTemplate) },
            {"AwsDeploymentStackTemplate", typeof(AwsDeploymentStackTemplate)},
            {"AwsTenancyStackTemplate",typeof(AwsTenancyStackTemplate) },
            //{"AwsWebAppStack",typeof(AwsWebAppStackResource) }
        };
        public bool Accepts(Type type)
        {
            return type == typeof(ArtifactBase);
        }

        /// <summary>
        /// This custom converter reads the Directives property from the YAML file and
        /// calls the Parse method of the Directives class to parse the Directives property
        /// into the type specified in the Directive.Type property. 
        /// </summary>
        /// <param name="parser"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public object ReadYaml(IParser parser, Type typeArg)
        {
            var mappingNode = new YamlMappingNode();
            var key = "";

            parser.Consume<MappingStart>();  // Consume the MappingStart event
            string type = null;
            try
            {
                while (!(parser.Current is MappingEnd))
                {
                    key = ConsumeYamlNode(parser).ToString();
                    switch (key)
                    {
                        case "Type":
                            type = ConsumeTypeNode(parser);
                            if (!ArtifactTypes.ContainsKey(type)) throw new Exception($" = {type} Unknown Type.");
                            mappingNode.Add(key, type);
                            break;
                        default:
                            var value = ConsumeYamlNode(parser);
                            mappingNode.Add(key, value);
                            break;
                    }
                    key = null;
                }
                if (type == null) throw new Exception(" Type property is required.");
                parser.Consume<MappingEnd>();  // Consume the MappingEnd event
                var artifactString = serializer.Serialize(mappingNode);
                var artifact = (ArtifactBase)deserializer.Deserialize(artifactString, ArtifactTypes[type]);
                return artifact;

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
