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

            // Api Artifacts
            {"AwsHttpApiResource", typeof(AwsHttpApiResource) },
            {"AwsWSApiResource", typeof(AwsWSApiResource) },
            {"DotNetHttpApiSDKProject", typeof(DotNetHttpApiSDKProject) },
            {"DotNetWSApiSDKProject", typeof(DotNetHttpApiSDKProject) },

            // Authentication Artifacts
            {"AwsCognitoResource", typeof(AwsCognitoResource) },

            // Authorization Artifacts
            {"DotNetAuthorizationProject", typeof(DotNetAuthorizationProject) },

            // Container Artifacts
            {"AwsApiLambdaResource", typeof(AwsApiLambdaResource) },
            {"AwsSQSLambdaResource", typeof(AwsSQSLambdaResource) },
            {"AwsWSApiLambdaResource", typeof(AwsWSApiLambdaResource) },
            {"DotNetApiLambdaProject", typeof(DotNetApiLambdaProject) },
            {"DotNetWSApiLambdaProject", typeof(DotNetWSApiLambdaProject) },

            // Deployment Artifacts
            {"AwsDeploymentStackTemplate", typeof(AwsDeploymentStackTemplate)},

            // Module Artifacts
            {"DotNetControllerProject", typeof(DotNetControllerProject) },

            // Queue Artifacts
            {"AwsSQSResource",typeof(AwsSQSResource) },

            // Schema Artifacts
            {"DotNetRepoProject", typeof(DotNetRepoProject) },
            {"DotNetSchemaProject", typeof(DotNetSchemaProject) },
          
            // Service Artifacts
            {"AwsServiceStackTemplate",typeof(AwsServiceStackTemplate) },
            {"DotNetLocalWebApiProject", typeof(DotNetLocalWebApiProject) },

            // Tenancy Artifacts
            {"AwsTenancyStackTemplate",typeof(AwsTenancyStackTemplate) },


            // General
            {"DotNetProject", typeof(DotNetProject) },

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
