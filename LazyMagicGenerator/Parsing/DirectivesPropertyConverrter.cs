using System;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace LazyMagic
{
    public class DirectivesPropertyConverter : YamlTypeConverterBase, IYamlTypeConverter
    {
        public DirectivesPropertyConverter() { }
        public bool Accepts(Type type)
        {
            return type == typeof(Directives);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            string key = "";
            try
            {
                var directives = new Directives();
                parser.Consume<MappingStart>(); // start of Directives
                while (!(parser.Current is MappingEnd))
                {
                    key = parser.Consume<Scalar>().Value; // Directive name
                    switch (parser.Current)
                    {
                        case MappingStart _:
                            var directive =(DirectiveBase) new DirectivePropertyConverter().ReadYaml(parser, typeof(DirectiveBase));
                            directive.Key = key;
                            directives.Add(key, directive);
                            break;
                        default:
                            throw new Exception(" Not an object.");
                    }
                }
                parser.Consume<MappingEnd>(); // end of Directives2
                return directives;
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


