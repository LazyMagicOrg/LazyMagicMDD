using System;
using System.Collections.Generic;
using System.Text;
using NSwag;
using NSwag.CodeGeneration.OperationNameGenerators;

namespace LazyMagic
{
    public class LzOperationNameGenerator : IOperationNameGenerator
    {
        public bool SupportsMultipleClients { get; } = true;

        public string GetClientName(OpenApiDocument document, string path, string httpMethod, OpenApiOperation operation)
        {
            return string.Empty;
        }

        public string GetOperationName(OpenApiDocument document, string path, string httpMethod, OpenApiOperation operation)
        {
            var operationId = operation.OperationId;
            if (string.IsNullOrEmpty(operationId))
            {
                var name = ToUpperFirstChar(httpMethod.ToLower()); //ex: GET to Get
                var parts = path.Split('/');
                foreach (var part in parts)
                {
                    var namePart = part;
                    namePart = namePart.Replace("{", "");
                    namePart = namePart.Replace("}", "");
                    name += ToUpperFirstChar(namePart);
                }
                return name;
            }
            else
                return ToUpperFirstChar(operationId);
        }
        public static string ToUpperFirstChar(string str)
        {
            if (str.Length == 0)
                return str;
            else
            if (str.Length == 1)
                return str.ToUpper();
            else
                return str[0].ToString().ToUpper() + str.Substring(1);
        }
    }
}
