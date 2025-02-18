using LazyMagic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using NSwag;
using NJsonSchema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using static LazyMagic.LzLogger;
using static LazyMagic.YamlUtil;
using static LazyMagic.OSUtils;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.RepresentationModel;


namespace LazyMagic
{
    public static class OpenApiUtils
    {
        public static async Task<OpenApiDocument> LoadOpenApiFilesAsync(string solutionRootFolder, List<string> openApiFilePaths)
        {
            var yamlContent = await MergeApiFilesAsync(solutionRootFolder, openApiFilePaths);
            return await ParseOpenApiYamlContent(yamlContent);
        }

        public static async Task<OpenApiDocument> ParseOpenApiYamlContent(string content)
        {
            try
            {
                var openApiDocument = await OpenApiYamlDocument.FromYamlAsync(content);
                return openApiDocument;
            }
            catch (Exception ex)
            {
                await InfoAsync($"\n {ex.Message}");
                throw new Exception($"ParseOpenApiYamlContent failed. {ex.Message}");
            }
        }

        public static async Task<string> MergeApiFilesAsync(string solutionRootFolder, List<string> openApiFilePaths)
        {
            JObject jsonOpenApiDoc = JObject.Parse("{}");
            var count = 0;
            foreach (var path in openApiFilePaths)
            {
                string yamlText;

                try { yamlText = File.ReadAllText(Path.Combine(solutionRootFolder, path)); }
                catch (Exception ex)
                {
                    await InfoAsync($"\n {ex.Message}");
                    throw new Exception($"Read of OpenApi file failed. {ex.Message}");
                }
                var deserializer = new DeserializerBuilder().Build();
                var yamlObject = deserializer.Deserialize<object>(yamlText);
                var jsonContent = JsonConvert.SerializeObject(yamlObject, Formatting.Indented); // using indentation in case I want to use debug to view content
                var jsonObject = JObject.Parse(jsonContent);

                if (count == 0)
                    jsonOpenApiDoc = jsonObject;
                else
                    jsonOpenApiDoc.Merge(jsonObject, new JsonMergeSettings() { MergeArrayHandling = MergeArrayHandling.Union });

                count++;
            }

            var jsonText = JsonConvert.SerializeObject(jsonOpenApiDoc, Formatting.Indented);
            var yamlContent = ConvertJsonToYaml(jsonText);

            return yamlContent;
        }

        public static async Task<string> MergeYamlAsync(string solutionRootFolder, List<string> yamlList)
        {
            await Task.Delay(0);
            JObject jsonOpenApiDoc = JObject.Parse("{}");
            var count = 0;
            foreach (var yamlText in yamlList)
            {
                var deserializer = new DeserializerBuilder().Build();
                var yamlObject = deserializer.Deserialize<object>(yamlText);
                var jsonContent = JsonConvert.SerializeObject(yamlObject, Formatting.Indented); // using indentation in case I want to use debug to view content
                var jsonObject = JObject.Parse(jsonContent);

                if (count == 0)
                    jsonOpenApiDoc = jsonObject;
                else
                    jsonOpenApiDoc.Merge(jsonObject, new JsonMergeSettings() { MergeArrayHandling = MergeArrayHandling.Union });

                count++;
            }

            var jsonText = JsonConvert.SerializeObject(jsonOpenApiDoc, Formatting.Indented);
            var yamlContent = ConvertJsonToYaml(jsonText);

            return yamlContent;
        }

        private class InternalOpenApiDocument
        {
            public InternalComponents Components { get; set; }
        }

        private class InternalComponents
        {
            public Dictionary<string, InternalSchemaObject> Schemas { get; set; }
        }

        private class InternalSchemaObject
        {
            public string Type { get; set; }
            public Dictionary<string, InternalSchemaObject> Properties { get; set; }
            public InternalSchemaObject Items { get; set; }
            public List<string> Enum { get; set; }
            public string Format { get; set; }
            // Add other properties as needed
        }


        public static List<string> GetComponentSchemaEntities(string yamlContent)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var entities = new List<string>();

            var openApiDoc = deserializer.Deserialize<InternalOpenApiDocument>(yamlContent);
            if (openApiDoc?.Components?.Schemas == null) return entities;
            foreach(var schemaEntry in openApiDoc.Components.Schemas)
            {
                entities.Add(schemaEntry.Key);
            }   
            return entities;    

        }
        public static List<string> GetSchemaNamesForEntities(SolutionBase solution, List<string> entities)
        {
            return solution.Directives
                .Where(d => d.Value is Schema && ((Schema)d.Value).Entities.Intersect(entities).Any())
                .Select(s => s.Key)
                .ToList();
        }
        public static List<string> GetReferencedEntities(string yamlContent)
        {
            var yaml = new YamlStream();
            using (var reader = new StringReader(yamlContent))
            {
                yaml.Load(reader);
            }

            var rootNode = yaml.Documents[0].RootNode;
            return FindRefNodes(rootNode);
        }

        private static List<string> FindRefNodes(YamlNode node)
        {
            var refNodes = new List<string>();

            switch (node)
            {
                case YamlMappingNode mappingNode:
                    foreach (var child in mappingNode.Children)
                    {
                        if (child.Key is YamlScalarNode keyNode && keyNode.Value == "$ref")
                        {
                            var reference = ((YamlScalarNode)child.Value).Value;
                            if (!string.IsNullOrEmpty(reference))
                            {
                                var lastSlashIndex = reference.LastIndexOf('/');
                                reference = lastSlashIndex >= 0 ? reference.Substring(lastSlashIndex + 1) : reference;
                                refNodes.Add(reference);
                            }
                        }
                        refNodes.AddRange(FindRefNodes(child.Value));
                    }
                    break;

                case YamlSequenceNode sequenceNode:
                    foreach (var item in sequenceNode.Children)
                    {
                        refNodes.AddRange(FindRefNodes(item));
                    }
                    break;
            }

            return refNodes;
        }
        // TODO: need to search Paths as well
        //public static List<string> GetReferencedEntities(string yamlContent)
        //{
        //    var yaml = new YamlStream();
        //    using (var reader = new StringReader(yamlContent))
        //    {
        //        yaml.Load(reader);
        //    }

        //    var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;

        //    // Navigate to the components.schemas section
        //    if (rootNode.Children.TryGetValue("components", out var componentsNode))
        //    {
        //        var componentsMapping = (YamlMappingNode)componentsNode;
        //        if (componentsMapping.Children.TryGetValue("schemas", out var schemasNode))
        //        {
        //            var schemasMapping = (YamlMappingNode)schemasNode;
        //            return FindRefNodes(schemasMapping);
        //        }
        //    }

        //    return new List<string>();

        //}
        //private static List<string> FindRefNodes(YamlMappingNode node)
        //{
        //    var refNodes = new List<string>();

        //    foreach (var child in node.Children)
        //    {
        //        if (child.Key is YamlScalarNode keyNode && keyNode.Value == "$ref")
        //        {
        //            var reference = ((YamlScalarNode)child.Value).Value;
        //            if (string.IsNullOrEmpty(reference)) continue;
        //            var lastSlashIndex = reference.LastIndexOf('/');
        //            reference = lastSlashIndex >= 0 ? reference.Substring(lastSlashIndex + 1) : reference;
        //            refNodes.Add(reference);
        //        }
        //        else if (child.Value is YamlMappingNode childMapping)
        //        {
        //            refNodes.AddRange(FindRefNodes(childMapping));
        //        }
        //        else if (child.Value is YamlSequenceNode sequenceNode)
        //        {
        //            foreach (var item in sequenceNode.Children)
        //            {
        //                if (item is YamlMappingNode itemMapping)
        //                {
        //                    refNodes.AddRange(FindRefNodes(itemMapping));
        //                }
        //            }
        //        }
        //    }

        //    return refNodes;
        //}

        public static List<string> GetSchemaNames(string yamlContent)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var openApiDoc = deserializer.Deserialize<InternalOpenApiDocument>(yamlContent);

            if (openApiDoc?.Components?.Schemas == null)
            {
                return new List<string>();
            }

            var schemaNames = new HashSet<string>();
            foreach (var schemaEntry in openApiDoc.Components.Schemas)
            {
                schemaNames.Add(schemaEntry.Key);
                FindNestedSchemas(schemaEntry.Key, schemaEntry.Value, schemaNames);
            }

            return schemaNames.ToList();
        }

        private static void FindNestedSchemas(string parentName, InternalSchemaObject schema, HashSet<string> schemaNames)
        {
            if (schema.Enum != null && schema.Enum.Any())
            {
                schemaNames.Add($"{parentName}");
            }

            if (schema.Properties != null)
            {
                foreach (var property in schema.Properties)
                {
                    if (property.Value.Enum != null && property.Value.Enum.Any())
                    {
                        var name = property.Key.Substring(0, 1).ToUpper() + property.Key.Substring(1);
                        schemaNames.Add($"{parentName}{name}");
                    }
                    else if (property.Value.Type == "object" || property.Value.Type == "array")
                    {
                        FindNestedSchemas($"{parentName}{property.Key}", property.Value, schemaNames);
                    }
                }
            }

            if (schema.Items != null)
            {
                FindNestedSchemas($"{parentName}.Items", schema.Items, schemaNames);
            }
        }



    }

}