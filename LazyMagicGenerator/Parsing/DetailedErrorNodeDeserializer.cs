using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;


namespace LazyMagic
{
    public class DetailedErrorNodeDeserializer : INodeDeserializer
    {
        private readonly INodeDeserializer _innerDeserializer;

        public DetailedErrorNodeDeserializer(INodeDeserializer innerDeserializer)
        {
            _innerDeserializer = innerDeserializer;
        }

        public bool Deserialize(IParser parser, Type expectedType, Func<IParser, Type, object> nestedObjectDeserializer, out object value)
        {
            var bufferingParser = new ParserWithBuffer(parser);

            try
            {
                return _innerDeserializer.Deserialize(bufferingParser, expectedType, nestedObjectDeserializer, out value);
            }
            catch (YamlException ex)
            {
                string propertyPath = GetPropertyPathAtError(bufferingParser.Events, ex.Start);
                //string errorMessage = $"Error deserializing {expectedType.Name}:\n" +
                //                      $"  At line {ex.Start.Line}, column {ex.Start.Column}\n" +
                //                      $"  {ex.Message}\n" +
                //                      $"  The error occurred in the property: {propertyPath}";
                string errorMessage = $"{propertyPath} Error, " + ex.Message;

                if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                {
                    errorMessage += $": {ex.InnerException.Message}";
                }

                throw new DetailedYamlException(ex.Start, ex.End, errorMessage, ex);
            }
        }

        private string GetPropertyPathAtError(IEnumerable<ParsingEvent> events, Mark errorPosition)
        {
            var propertyPath = new List<string>();
            string currentProperty = null;
            int depth = 0;

            foreach (var evt in events)
            {
                if (evt is MappingStart)
                {
                    depth++;
                }
                else if (evt is MappingEnd)
                {
                    depth--;
                    if (depth == 0 && propertyPath.Count > 0)
                    {
                        propertyPath.RemoveAt(propertyPath.Count - 1);
                    }
                }
                else if (evt is Scalar scalar)
                {
                    if (currentProperty == null)
                    {
                        currentProperty = scalar.Value;
                    }
                    else
                    {
                        if (scalar.Start.Line == errorPosition.Line && scalar.Start.Column == errorPosition.Column)
                        {
                            propertyPath.Add(currentProperty);
                            return string.Join(".", propertyPath);
                        }
                        if (depth > 0)
                        {
                            propertyPath.Add(currentProperty);
                        }
                        currentProperty = null;
                    }
                }
            }

            if (currentProperty != null)
            {
                propertyPath.Add(currentProperty);
            }
            return string.Join(".", propertyPath);
        }
    }

    public class DetailedYamlException : YamlException
    {
        public DetailedYamlException(Mark start, Mark end, string message, Exception innerException)
            : base(start, end, message, innerException)
        {
            ;
        }
    }

    public class ParserWithBuffer : IParser
    {
        private readonly IParser _baseParser;
        private readonly List<ParsingEvent> _events = new List<ParsingEvent>();

        public ParserWithBuffer(IParser baseParser)
        {
            _baseParser = baseParser;
        }

        public ParsingEvent Current => _baseParser.Current;

        public bool MoveNext()
        {
            var result = _baseParser.MoveNext();
            if (result)
            {
                _events.Add(_baseParser.Current);
            }
            return result;
        }

        public IEnumerable<ParsingEvent> Events => _events;
    }
}
