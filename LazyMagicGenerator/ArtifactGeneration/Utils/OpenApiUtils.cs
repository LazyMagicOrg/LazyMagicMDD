using LazyMagic;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NSwag;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static LazyMagic.LzLogger;
using static LazyMagic.OSUtils;
using static LazyMagic.YamlUtil;


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
        /// <summary>
        /// Given a list of entity names, find which Schema directive(s) define those entities.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="entities"></param>
        /// <returns></returns>
        public static List<string> GetSchemaNamesForEntities(SolutionBase solution, List<string> entities)
        {
            return solution.Directives
                .Where(d => d.Value is Schema && ((Schema)d.Value).Entities.Intersect(entities).Any())
                .Select(s => s.Key)
                .ToList();
        }
        /// <summary>
        /// Finds all schema entities transitively referenced from the paths section of an OpenAPI spec.
        /// Starts with entities directly referenced in paths, then recursively follows references
        /// within components/schemas to find all transitive dependencies.
        /// </summary>
        public static List<string> GetReferencedEntities(string yamlContent)
        {
            var yaml = new YamlStream();
            using (var reader = new StringReader(yamlContent))
            {
                yaml.Load(reader);
            }

            var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;

            YamlNode pathsNode = null;
            YamlNode schemasNode = null;

            // Find both paths and components/schemas sections
            foreach (var child in rootNode.Children)
            {
                if (child.Key is YamlScalarNode keyNode)
                {
                    if (keyNode.Value == "paths")
                    {
                        pathsNode = child.Value;
                    }
                    else if (keyNode.Value == "components" && child.Value is YamlMappingNode componentsMap)
                    {
                        foreach (var componentChild in componentsMap.Children)
                        {
                            if (componentChild.Key is YamlScalarNode compKeyNode && compKeyNode.Value == "schemas")
                            {
                                schemasNode = componentChild.Value;
                                break;
                            }
                        }
                    }
                }
            }

            if (pathsNode == null)
            {
                return new List<string>();
            }

            // Find all refs directly in paths
            var seedRefs = FindRefNodes(pathsNode);

            // If no schemas section, return just the seed refs
            if (schemasNode == null)
            {
                return seedRefs;
            }

            // Recursively find all transitive dependencies
            return FindTransitiveDependencies(seedRefs, (YamlMappingNode)schemasNode);
        }

        /// <summary>
        /// Given a set of seed entity names, recursively find all entities they transitively depend on
        /// by following $ref nodes within the schemas section.
        /// </summary>
        private static List<string> FindTransitiveDependencies(List<string> seedEntities, YamlMappingNode schemasNode)
        {
            var allEntities = new HashSet<string>();
            var toProcess = new Queue<string>(seedEntities);
            var processed = new HashSet<string>();

            while (toProcess.Count > 0)
            {
                var entity = toProcess.Dequeue();

                if (processed.Contains(entity))
                {
                    continue;
                }

                processed.Add(entity);
                allEntities.Add(entity);

                // Find the schema definition for this entity
                foreach (var schemaEntry in schemasNode.Children)
                {
                    if (schemaEntry.Key is YamlScalarNode keyNode && keyNode.Value == entity)
                    {
                        // Find all refs within this schema definition
                        var referencedEntities = FindRefNodes(schemaEntry.Value);

                        // Add newly discovered entities to the processing queue
                        foreach (var refEntity in referencedEntities)
                        {
                            if (!processed.Contains(refEntity))
                            {
                                toProcess.Enqueue(refEntity);
                            }
                        }
                        break;
                    }
                }
            }

            return allEntities.ToList();
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

            return refNodes.Distinct().ToList();
        }

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

        public static string GenerateOperationId(string op, string path)
        {
            if (op == null || path == null) return "";
            op = op.ToLower(); // Normalize operation to lowercase
            op = char.ToUpper(op[0]) + op.Substring(1); // Capitalize first letter of operation

            // /yada/bada/{id} -> YadaBadaId

            // Remove leading/trailing slashes and split by '/'
            var parts = path.Trim('/').Split('/');

            // Convert each part to proper case and concatenate
            op = op + string.Concat(parts.Select(part =>
            {
                // Remove curly braces if present
                part = part.Trim('{', '}');

                // Convert to proper case
                return string.IsNullOrEmpty(part) ? "" :
                    char.ToUpper(part[0]) + part.Substring(1).ToLower();
            }));

           return op.Replace('.', '_').Replace('-','_'); // Replace charaters that are not valid in C# identifiers
        }
    }
}