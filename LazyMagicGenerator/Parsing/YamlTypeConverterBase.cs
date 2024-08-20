using System;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization.NodeDeserializers;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Core.Events;

namespace LazyMagic
{
    public abstract class YamlTypeConverterBase
    {
        public YamlTypeConverterBase()
        {
            serializer = new SerializerBuilder()
               .Build();
            deserializer = new DeserializerBuilder()
                .WithNodeDeserializer(inner => new DetailedErrorNodeDeserializer(inner), s => s.InsteadOf<ObjectNodeDeserializer>())
               .Build();
        }
        protected ISerializer serializer;
        protected IDeserializer deserializer;

        protected string ConsumeTypeNode(IParser parser)
        {
            return parser.Consume<Scalar>().Value;
        }
        protected YamlMappingNode ConsumeYamlMappingNode(IParser parser)
        {
            var mappingNode = new YamlMappingNode();

            parser.Consume<MappingStart>();  // Consume the MappingStart event

            while (!(parser.Current is MappingEnd))
            {
                var key = ConsumeYamlNode(parser);
                var value = ConsumeYamlNode(parser);
                mappingNode.Add(key, value);
            }

            parser.Consume<MappingEnd>();  // Consume the MappingEnd event

            return mappingNode;
        }

        protected YamlNode ConsumeYamlNode(IParser parser)
        {
            switch (parser.Current)
            {
                case Scalar _:
                    return new YamlScalarNode(parser.Consume<Scalar>().Value);
                case SequenceStart _:
                    return ConsumeYamlSequenceNode(parser);
                case MappingStart _:
                    return ConsumeYamlMappingNode(parser);
                default:
                    throw new YamlException($" Unexpected node type: {parser.Current.GetType().Name}");
            }
        }

        protected YamlSequenceNode ConsumeYamlSequenceNode(IParser parser)
        {
            var sequenceNode = new YamlSequenceNode();

            parser.Consume<SequenceStart>();

            while (!(parser.Current is SequenceEnd))
            {
                sequenceNode.Add(ConsumeYamlNode(parser));
            }

            parser.Consume<SequenceEnd>();

            return sequenceNode;
        }
        public virtual void WriteYaml(IEmitter emitter, object value, Type type)
        {
            ;
        }


    }
}
