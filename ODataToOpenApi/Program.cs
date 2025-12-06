using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OpenApi;
using Microsoft.OpenApi.OData;
using YamlDotNet.RepresentationModel;

namespace ODataToOpenApi;

class Program
{
    static async Task Main(string[] args)
    {
        // Default paths using WSL-compatible paths
        var inputPath = args.Length > 0 
            ? args[0] 
            : "/mnt/c/Users/TimothyMay/repos/_Dev/Monro/Service/smart-store-odata.xml";
        
        var outputPath = args.Length > 1 
            ? args[1] 
            : "/mnt/c/Users/TimothyMay/repos/_Dev/Monro/Service/openapi.shop.yaml";

        Console.WriteLine($"OData to OpenAPI Converter");
        Console.WriteLine($"==========================");
        Console.WriteLine($"Input:  {inputPath}");
        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine();

        try
        {
            // 1. Load the OData CSDL metadata
            Console.WriteLine("Loading OData metadata...");
            using var fileStream = File.OpenRead(inputPath);
            using var xmlReader = XmlReader.Create(fileStream);
            var edmModel = CsdlReader.Parse(xmlReader);
            Console.WriteLine($"  Loaded EDM model with {edmModel.SchemaElements.Count()} schema elements");

            // 2. Configure conversion settings
            Console.WriteLine("Configuring conversion settings...");
            var settings = new OpenApiConvertSettings
            {
                // Key setting: Use /entity/{key} instead of /entity({key})
                EnableKeyAsSegment = true,
                
                // Generate operation IDs
                EnableOperationId = true,
                
                // Target OpenAPI 3.1
                OpenApiSpecVersion = OpenApiSpecVersion.OpenApi3_1,
                
                // Path prefix to match existing spec (without leading slash to avoid double slashes)
                PathPrefix = "odata/v1",
                
                // Include navigation property paths
                EnableNavigationPropertyPath = true,
                
                // Include OData operations (functions/actions)
                EnableOperationPath = true,
                EnableOperationImportPath = true,
                
                // Pagination and count
                EnablePagination = false,
                EnableCount = false,
                EnableDollarCountPath = false,
                
                // Service root
                ServiceRoot = new Uri("https://smartstore.example.com"),
                
                // Remove namespace prefix (Default.) from operations
                EnableUnqualifiedCall = true
            };

            // 3. Convert to OpenAPI
            Console.WriteLine("Converting to OpenAPI...");
            var openApiDocument = edmModel.ConvertToOpenApi(settings);
            Console.WriteLine($"  Generated {openApiDocument.Paths.Count} paths");

            // 4. Add x-lz-odatapath extension to all operations (preserves original OData path)
            Console.WriteLine("Adding x-lz-odatapath extensions...");
            var extensionCount = AddODataPathExtensions(openApiDocument);
            Console.WriteLine($"  Added x-lz-odatapath to {extensionCount} operations");

            // 5. Post-process function paths to convert OData syntax to REST-style
            // This also updates x-lz-odatapath for paths that get modified
            Console.WriteLine("Post-processing function paths...");
            var convertedCount = PostProcessFunctionPaths(openApiDocument);
            Console.WriteLine($"  Converted {convertedCount} function/action paths to REST-style");

            // 6. Serialize to YAML (v2.0.0 async API)
            Console.WriteLine("Serializing to YAML...");
            var yaml = await openApiDocument.SerializeAsYamlAsync(OpenApiSpecVersion.OpenApi3_1);

            // 7. Simplify component names in the serialized YAML
            Console.WriteLine("Simplifying component names...");
            var (simplifiedYaml, renamedCount) = PostProcessComponentNamesInYaml(yaml);
            Console.WriteLine($"  Renamed {renamedCount} components");

            // 8. Extract inline schemas to components
            Console.WriteLine("Extracting inline schemas to components...");
            var (extractedYaml, extractedCount, reusedCount) = ExtractInlineSchemasToComponents(simplifiedYaml);
            Console.WriteLine($"  Extracted {extractedCount} inline schemas, reused {reusedCount} existing components");

            // 9. Remove examples section from components
            Console.WriteLine("Removing examples section...");
            var noExamplesYaml = RemoveExamplesSection(extractedYaml);
            Console.WriteLine($"  Removed examples section");

            // 10. Convert OpenAPI 3.1 type arrays to 3.0 style nullable
            Console.WriteLine("Converting type arrays to nullable syntax...");
            var convertedYaml = ConvertTypeArraysToNullable(noExamplesYaml);
            Console.WriteLine($"  Converted type arrays to nullable syntax");

            // 11. Simplify oneOf declarations to single type
            Console.WriteLine("Simplifying oneOf declarations...");
            var (simplifiedYaml2, oneOfCount) = SimplifyOneOfDeclarations(convertedYaml);
            Console.WriteLine($"  Simplified {oneOfCount} oneOf declarations");

            // 12. Rename conflicting schema names (e.g., StreamContent -> ODataStreamContent)
            Console.WriteLine("Renaming conflicting schema names...");
            var finalYaml = RenameConflictingSchemas(simplifiedYaml2);
            Console.WriteLine($"  Renamed conflicting schemas");

            // 13. Write to file
            await File.WriteAllTextAsync(outputPath, finalYaml);
            Console.WriteLine($"  Written to: {outputPath}");

            Console.WriteLine();
            Console.WriteLine("Conversion complete!");
            
            // Print some stats
            Console.WriteLine();
            Console.WriteLine("Statistics:");
            Console.WriteLine($"  Paths: {openApiDocument.Paths.Count}");
            Console.WriteLine($"  Schemas: {openApiDocument.Components?.Schemas?.Count ?? 0}");
            
            var operationCount = openApiDocument.Paths
                .Where(p => p.Value?.Operations != null)
                .SelectMany(p => p.Value!.Operations)
                .Count();
            Console.WriteLine($"  Operations: {operationCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Adds x-lz-odatapath extension to all operations, preserving the original OData path.
    /// This allows downstream tools to know the original OData path for flow-through operations.
    /// </summary>
    /// <returns>Number of operations that received the extension</returns>
    static int AddODataPathExtensions(OpenApiDocument openApiDocument)
    {
        var count = 0;
        
        foreach (var pathEntry in openApiDocument.Paths)
        {
            var path = pathEntry.Key;
            var pathItem = pathEntry.Value;
            
            if (pathItem?.Operations == null) continue;
            
            foreach (var operation in pathItem.Operations.Values)
            {
                if (operation == null) continue;
                operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                operation.Extensions["x-lz-odatapath"] = new JsonNodeExtension(JsonValue.Create(path));
                count++;
            }
        }
        
        return count;
    }

    /// <summary>
    /// Post-processes the OpenAPI document to convert OData function paths to REST-style paths.
    /// Updates x-lz-odatapath extension to preserve original OData paths for converted paths.
    /// 
    /// Examples:
    ///   /odata/v1/DeliveryTimes/GetDeliveryDate(Id={Id}) -> /odata/v1/DeliveryTimes/GetDeliveryDate/{Id}
    ///   /odata/v1/Foo/Bar(A={A},B={B}) -> /odata/v1/Foo/Bar/{A}/{B}
    ///   /odata/v1/Foo/DoSomething() -> /odata/v1/Foo/DoSomething
    /// </summary>
    /// <returns>Number of paths converted</returns>
    static int PostProcessFunctionPaths(OpenApiDocument openApiDocument)
    {
        // Regex to detect OData function syntax: FunctionName(Params) at end of path
        var functionPattern = new Regex(@"^(.+/)(\w+)\(([^)]*)\)$");
        
        var pathsToAdd = new Dictionary<string, IOpenApiPathItem>();
        var pathsToRemove = new List<string>();
        
        foreach (var pathEntry in openApiDocument.Paths)
        {
            var originalPath = pathEntry.Key;
            var pathItem = pathEntry.Value;
            
            if (pathItem == null) continue;
            
            var match = functionPattern.Match(originalPath);
            if (match.Success)
            {
                var basePath = match.Groups[1].Value;      // e.g., "/odata/v1/DeliveryTimes/"
                var functionName = match.Groups[2].Value;  // e.g., "GetDeliveryDate"
                var paramsStr = match.Groups[3].Value;     // e.g., "Id={Id}" or "A={A},B={B}" or ""
                
                // Convert to REST-style path
                var newPath = ConvertODataFunctionPath(basePath, functionName, paramsStr);
                
                // Add x-lz-odatapath extension to each operation
                if (pathItem.Operations != null)
                {
                    foreach (var operation in pathItem.Operations.Values)
                    {
                        operation.Extensions["x-lz-odatapath"] = new JsonNodeExtension(JsonValue.Create(originalPath));
                    }
                }
                
                pathsToRemove.Add(originalPath);
                pathsToAdd[newPath] = pathItem;
            }
            // Also handle actions with empty parens at end: /path/DoSomething()
            else if (originalPath.EndsWith("()"))
            {
                var newPath = originalPath[..^2]; // Remove trailing ()
                
                // Add x-lz-odatapath extension to each operation
                if (pathItem.Operations != null)
                {
                    foreach (var operation in pathItem.Operations.Values)
                    {
                        operation.Extensions["x-lz-odatapath"] = new JsonNodeExtension(JsonValue.Create(originalPath));
                    }
                }
                
                pathsToRemove.Add(originalPath);
                pathsToAdd[newPath] = pathItem;
            }
        }
        
        // Remove old paths and add new ones
        foreach (var path in pathsToRemove)
        {
            openApiDocument.Paths.Remove(path);
        }
        foreach (var (path, item) in pathsToAdd)
        {
            openApiDocument.Paths.Add(path, item);
        }
        
        return pathsToRemove.Count;
    }

    /// <summary>
    /// Converts OData function path syntax to REST-style path.
    /// 
    /// Examples:
    ///   ("/odata/v1/DeliveryTimes/", "GetDeliveryDate", "Id={Id}") -> "/odata/v1/DeliveryTimes/GetDeliveryDate/{Id}"
    ///   ("/odata/v1/Foo/", "Bar", "A={A},B={B}") -> "/odata/v1/Foo/Bar/{A}/{B}"
    ///   ("/odata/v1/Foo/", "Bar", "Name='{Name}'") -> "/odata/v1/Foo/Bar/{Name}"
    ///   ("/odata/v1/Foo/", "Bar", "A=@A") -> "/odata/v1/Foo/Bar/{A}"
    ///   ("/odata/v1/Foo/", "DoSomething", "") -> "/odata/v1/Foo/DoSomething"
    /// </summary>
    static string ConvertODataFunctionPath(string basePath, string functionName, string paramsStr)
    {
        var newPath = basePath + functionName;
        
        if (string.IsNullOrEmpty(paramsStr))
        {
            return newPath;
        }
        
        // Parse parameters: "Id={Id}" or "A={A},B={B}" or "Name='{Name}'" or "A=@A"
        // Pattern matches: ParamName={ParamName} or ParamName='{ParamName}' or ParamName=@ParamName
        var paramPattern = new Regex(@"(\w+)='?\{?@?(\w+)\}?'?");
        var matches = paramPattern.Matches(paramsStr);
        
        foreach (Match match in matches)
        {
            var paramName = match.Groups[2].Value;  // Extract the parameter name
            newPath += $"/{{{paramName}}}";
        }
        
        return newPath;
    }

    /// <summary>
    /// Simplifies component names in the serialized YAML by removing namespace prefixes where possible.
    /// Uses camelCase to join parts when disambiguation is needed.
    /// 
    /// This approach works on the serialized YAML text to avoid issues with the OpenAPI library's
    /// reference resolution that causes stack overflows with circular references.
    /// 
    /// Examples:
    ///   Smartstore.Core.Common.Address -> Address (no conflict)
    ///   Smartstore.Core.Catalog.Products.Product -> Product (no conflict)
    ///   Default.ODataErrors.ODataError -> ODataError (no conflict)
    ///   If there were two different "Product" types, they might become:
    ///     Smartstore.Core.Catalog.Products.Product -> CatalogProductsProduct
    ///     Smartstore.Other.Product -> OtherProduct
    /// </summary>
    /// <returns>Tuple of (processed YAML, number of components renamed)</returns>
    static (string yaml, int renamedCount) PostProcessComponentNamesInYaml(string yaml)
    {
        // Parse YAML to extract schema names
        using var reader = new StringReader(yaml);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);
        
        if (yamlStream.Documents.Count == 0)
            return (yaml, 0);
        
        var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;
        
        // Navigate to components.schemas
        if (!root.Children.TryGetValue(new YamlScalarNode("components"), out var componentsNode))
            return (yaml, 0);
        
        var components = componentsNode as YamlMappingNode;
        if (components == null || !components.Children.TryGetValue(new YamlScalarNode("schemas"), out var schemasNode))
            return (yaml, 0);
        
        var schemas = schemasNode as YamlMappingNode;
        if (schemas == null)
            return (yaml, 0);
        
        // Get all schema names
        var originalNames = schemas.Children.Keys
            .OfType<YamlScalarNode>()
            .Select(n => n.Value!)
            .Where(n => n != null)
            .ToList();
        
        // Build mapping from original name to simplified name
        var nameMapping = BuildSimplifiedNameMapping(originalNames);
        
        // Count how many are actually being renamed
        var renamedCount = nameMapping.Count(kvp => kvp.Key != kvp.Value);
        
        if (renamedCount == 0)
            return (yaml, 0);
        
        // Apply renaming using string replacement
        // We need to be careful to replace in the right context:
        // 1. Schema definition keys under components/schemas
        // 2. $ref references like '#/components/schemas/OldName'
        
        var result = yaml;
        
        // Sort by length descending to avoid partial replacements (e.g., replacing "Address" before "AddressInfo")
        var sortedMappings = nameMapping
            .Where(kvp => kvp.Key != kvp.Value)
            .OrderByDescending(kvp => kvp.Key.Length)
            .ToList();
        
        foreach (var (oldName, newName) in sortedMappings)
        {
            // Replace $ref references for schemas - these are safe to replace globally
            var oldRef = $"'#/components/schemas/{oldName}'";
            var newRef = $"'#/components/schemas/{newName}'";
            result = result.Replace(oldRef, newRef);
            
            // Also handle without quotes (some serializers don't quote)
            oldRef = $"\"#/components/schemas/{oldName}\"";
            newRef = $"\"#/components/schemas/{newName}\"";
            result = result.Replace(oldRef, newRef);
            
            // Replace $ref references for responses
            oldRef = $"'#/components/responses/{oldName}'";
            newRef = $"'#/components/responses/{newName}'";
            result = result.Replace(oldRef, newRef);
            
            oldRef = $"\"#/components/responses/{oldName}\"";
            newRef = $"\"#/components/responses/{newName}\"";
            result = result.Replace(oldRef, newRef);
            
            // Replace $ref references for examples
            oldRef = $"'#/components/examples/{oldName}'";
            newRef = $"'#/components/examples/{newName}'";
            result = result.Replace(oldRef, newRef);
            
            oldRef = $"\"#/components/examples/{oldName}\"";
            newRef = $"\"#/components/examples/{newName}\"";
            result = result.Replace(oldRef, newRef);
        }
        
        // Now rename the schema/response keys themselves
        // We need to find lines that are definitions (indented appropriately)
        // and rename them in both schemas: and responses: sections
        var lines = result.Split('\n');
        var inTargetSection = false;
        var sectionIndent = -1;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;
            
            // Detect when we enter a target section (schemas:, responses:, or examples: under components)
            if (trimmed == "schemas:" || trimmed == "responses:" || trimmed == "examples:")
            {
                inTargetSection = true;
                sectionIndent = indent;
                continue;
            }
            
            // Detect when we leave the section (same or less indent, and not empty/comment)
            if (inTargetSection && trimmed.Length > 0 && !trimmed.StartsWith("#") && indent <= sectionIndent)
            {
                inTargetSection = false;
                continue;
            }
            
            // If we're in a target section and this line is a key (one level deeper)
            if (inTargetSection && indent == sectionIndent + 2)
            {
                // Check if this line is a schema/response key
                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex > 0)
                {
                    var keyName = trimmed.Substring(0, colonIndex);
                    if (nameMapping.TryGetValue(keyName, out var newName) && keyName != newName)
                    {
                        lines[i] = new string(' ', indent) + newName + trimmed.Substring(colonIndex);
                    }
                }
            }
        }
        
        result = string.Join('\n', lines);
        
        return (result, renamedCount);
    }

    /// <summary>
    /// Builds a mapping from original multi-part names to simplified names.
    /// Uses the simple name (last part) when unique, otherwise adds prefix parts as camelCase.
    /// </summary>
    static Dictionary<string, string> BuildSimplifiedNameMapping(List<string> originalNames)
    {
        var mapping = new Dictionary<string, string>();
        
        // Group by simple name (last part after splitting by '.')
        var grouped = originalNames
            .GroupBy(name => GetSimpleName(name))
            .ToDictionary(g => g.Key, g => g.ToList());
        
        foreach (var group in grouped)
        {
            var simpleName = group.Key;
            var fullNames = group.Value;
            
            if (fullNames.Count == 1)
            {
                // No conflict - use simple name
                mapping[fullNames[0]] = simpleName;
            }
            else
            {
                // Conflict - need to disambiguate
                var disambiguated = DisambiguateNames(fullNames);
                foreach (var (original, simplified) in disambiguated)
                {
                    mapping[original] = simplified;
                }
            }
        }
        
        return mapping;
    }

    /// <summary>
    /// Gets the simple name (last part) from a dot-separated name.
    /// </summary>
    static string GetSimpleName(string fullName)
    {
        var parts = fullName.Split('.');
        return parts[^1];
    }

    /// <summary>
    /// Disambiguates a list of names that would otherwise have the same simple name.
    /// Adds enough prefix parts (as camelCase) to make each unique.
    /// </summary>
    static List<(string original, string simplified)> DisambiguateNames(List<string> fullNames)
    {
        var result = new List<(string original, string simplified)>();
        
        // Split all names into parts
        var allParts = fullNames.Select(name => name.Split('.')).ToList();
        
        // Find the minimum number of parts from the end needed to disambiguate
        var maxParts = allParts.Max(p => p.Length);
        
        for (int numParts = 1; numParts <= maxParts; numParts++)
        {
            var candidates = allParts.Select(parts => 
            {
                var relevantParts = parts.Skip(Math.Max(0, parts.Length - numParts)).ToArray();
                return ToCamelCase(relevantParts);
            }).ToList();
            
            // Check if all candidates are unique
            if (candidates.Distinct().Count() == candidates.Count)
            {
                // Found unique names
                for (int i = 0; i < fullNames.Count; i++)
                {
                    result.Add((fullNames[i], candidates[i]));
                }
                return result;
            }
        }
        
        // Fallback: use full name with dots replaced by camelCase
        foreach (var fullName in fullNames)
        {
            var parts = fullName.Split('.');
            result.Add((fullName, ToCamelCase(parts)));
        }
        
        return result;
    }

    /// <summary>
    /// Converts an array of name parts to camelCase.
    /// First part keeps original case, subsequent parts are capitalized.
    /// Example: ["Core", "Common", "Address"] -> "CoreCommonAddress"
    /// Example: ["Address"] -> "Address"
    /// </summary>
    static string ToCamelCase(string[] parts)
    {
        if (parts.Length == 0) return string.Empty;
        if (parts.Length == 1) return parts[0];
        
        // Join all parts, capitalizing each one
        return string.Concat(parts.Select(p => 
            string.IsNullOrEmpty(p) ? "" : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    /// <summary>
    /// Extracts inline schemas from paths and moves them to components/schemas.
    /// Reuses existing components where the schema structure matches.
    /// </summary>
    /// <returns>Tuple of (processed YAML, count of new schemas extracted, count of existing schemas reused)</returns>
    static (string yaml, int extractedCount, int reusedCount) ExtractInlineSchemasToComponents(string yaml)
    {
        using var reader = new StringReader(yaml);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);
        
        if (yamlStream.Documents.Count == 0)
            return (yaml, 0, 0);
        
        var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;
        
        // Get existing schemas for matching
        var existingSchemas = new Dictionary<string, YamlNode>();
        if (root.Children.TryGetValue(new YamlScalarNode("components"), out var componentsNode))
        {
            var components = componentsNode as YamlMappingNode;
            if (components != null && components.Children.TryGetValue(new YamlScalarNode("schemas"), out var schemasNode))
            {
                var schemas = schemasNode as YamlMappingNode;
                if (schemas != null)
                {
                    foreach (var kvp in schemas.Children)
                    {
                        var key = (kvp.Key as YamlScalarNode)?.Value;
                        if (key != null)
                        {
                            existingSchemas[key] = kvp.Value;
                        }
                    }
                }
            }
        }
        
        // Build a map of schema content hash -> schema name for quick matching
        var schemaHashToName = new Dictionary<string, string>();
        foreach (var kvp in existingSchemas)
        {
            var hash = GetSchemaHash(kvp.Value);
            if (!schemaHashToName.ContainsKey(hash))
            {
                schemaHashToName[hash] = kvp.Key;
            }
        }
        
        // Track new schemas to add and replacements to make
        var newSchemas = new Dictionary<string, YamlNode>();
        var replacements = new List<(YamlNode parent, YamlNode key, string refValue)>();
        int reusedCount = 0;
        int newSchemaCounter = 0;
        
        // Process paths to find inline schemas
        if (root.Children.TryGetValue(new YamlScalarNode("paths"), out var pathsNode))
        {
            var paths = pathsNode as YamlMappingNode;
            if (paths != null)
            {
                foreach (var pathKvp in paths.Children)
                {
                    var pathKey = (pathKvp.Key as YamlScalarNode)?.Value ?? "";
                    var pathItem = pathKvp.Value as YamlMappingNode;
                    if (pathItem == null) continue;
                    
                    // Process each operation (get, post, put, patch, delete, etc.)
                    foreach (var opKvp in pathItem.Children)
                    {
                        var opName = (opKvp.Key as YamlScalarNode)?.Value;
                        if (opName == "description" || opName == "parameters" || opName == "servers" || opName == "summary")
                            continue;
                        
                        var operation = opKvp.Value as YamlMappingNode;
                        if (operation == null) continue;
                        
                        var operationId = GetOperationId(operation);
                        
                        // Check requestBody
                        if (operation.Children.TryGetValue(new YamlScalarNode("requestBody"), out var requestBodyNode))
                        {
                            ProcessContentSchemas(requestBodyNode as YamlMappingNode, operationId, "Request",
                                existingSchemas, schemaHashToName, newSchemas, ref reusedCount, ref newSchemaCounter);
                        }
                        
                        // Check responses
                        if (operation.Children.TryGetValue(new YamlScalarNode("responses"), out var responsesNode))
                        {
                            var responses = responsesNode as YamlMappingNode;
                            if (responses != null)
                            {
                                foreach (var respKvp in responses.Children)
                                {
                                    var statusCode = (respKvp.Key as YamlScalarNode)?.Value ?? "";
                                    var response = respKvp.Value as YamlMappingNode;
                                    if (response == null) continue;
                                    
                                    // Skip $ref responses
                                    if (response.Children.ContainsKey(new YamlScalarNode("$ref")))
                                        continue;
                                    
                                    var suffix = statusCode == "200" || statusCode == "201" ? "Response" : $"Response{statusCode}";
                                    ProcessContentSchemas(response, operationId, suffix,
                                        existingSchemas, schemaHashToName, newSchemas, ref reusedCount, ref newSchemaCounter);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        // Also process components/responses for inline schemas
        if (root.Children.TryGetValue(new YamlScalarNode("components"), out var componentsForResponses))
        {
            var comps = componentsForResponses as YamlMappingNode;
            if (comps != null && comps.Children.TryGetValue(new YamlScalarNode("responses"), out var responsesNode))
            {
                var responses = responsesNode as YamlMappingNode;
                if (responses != null)
                {
                    foreach (var respKvp in responses.Children)
                    {
                        var responseName = (respKvp.Key as YamlScalarNode)?.Value ?? "";
                        var response = respKvp.Value as YamlMappingNode;
                        if (response == null) continue;
                        
                        // Use response name as base for schema name
                        ProcessContentSchemas(response, responseName, "Schema",
                            existingSchemas, schemaHashToName, newSchemas, ref reusedCount, ref newSchemaCounter);
                    }
                }
            }
        }
        
        // Now we need to add new schemas to components/schemas
        if (newSchemas.Count > 0)
        {
            // Get or create components/schemas
            if (!root.Children.TryGetValue(new YamlScalarNode("components"), out var compNode))
            {
                compNode = new YamlMappingNode();
                root.Children[new YamlScalarNode("components")] = compNode;
            }
            var components = compNode as YamlMappingNode;
            
            if (components != null)
            {
                if (!components.Children.TryGetValue(new YamlScalarNode("schemas"), out var schNode))
                {
                    schNode = new YamlMappingNode();
                    components.Children[new YamlScalarNode("schemas")] = schNode;
                }
                var schemas = schNode as YamlMappingNode;
                
                if (schemas != null)
                {
                    foreach (var kvp in newSchemas)
                    {
                        schemas.Children[new YamlScalarNode(kvp.Key)] = kvp.Value;
                    }
                }
            }
        }
        
        if (newSchemas.Count == 0 && reusedCount == 0)
            return (yaml, 0, 0);
        
        // Serialize the modified document
        using var writer = new StringWriter();
        yamlStream.Save(writer, assignAnchors: false);
        var result = writer.ToString();
        
        // Clean up the YAML output (remove document markers if present)
        result = result.Replace("...\n", "").TrimEnd();
        if (result.StartsWith("---\n"))
            result = result.Substring(4);
        
        return (result, newSchemas.Count, reusedCount);
    }

    /// <summary>
    /// Gets the operationId from an operation node.
    /// </summary>
    static string GetOperationId(YamlMappingNode operation)
    {
        if (operation.Children.TryGetValue(new YamlScalarNode("operationId"), out var opIdNode))
        {
            return (opIdNode as YamlScalarNode)?.Value ?? "Unknown";
        }
        return "Unknown";
    }

    /// <summary>
    /// Processes content schemas (in requestBody or response) and extracts inline schemas.
    /// </summary>
    static void ProcessContentSchemas(
        YamlMappingNode? parentNode,
        string operationId,
        string schemaSuffix,
        Dictionary<string, YamlNode> existingSchemas,
        Dictionary<string, string> schemaHashToName,
        Dictionary<string, YamlNode> newSchemas,
        ref int reusedCount,
        ref int newSchemaCounter)
    {
        if (parentNode == null) return;
        
        if (!parentNode.Children.TryGetValue(new YamlScalarNode("content"), out var contentNode))
            return;
        
        var content = contentNode as YamlMappingNode;
        if (content == null) return;
        
        foreach (var mediaTypeKvp in content.Children)
        {
            var mediaType = mediaTypeKvp.Value as YamlMappingNode;
            if (mediaType == null) continue;
            
            if (!mediaType.Children.TryGetValue(new YamlScalarNode("schema"), out var schemaNode))
                continue;
            
            var schema = schemaNode as YamlMappingNode;
            if (schema == null) continue;
            
            // Skip if it's already a $ref
            if (schema.Children.ContainsKey(new YamlScalarNode("$ref")))
                continue;
            
            // Check if this is an inline object schema worth extracting
            if (!IsExtractableSchema(schema))
                continue;
            
            // Try to match with existing schema
            var hash = GetSchemaHash(schema);
            if (schemaHashToName.TryGetValue(hash, out var existingName))
            {
                // Replace with reference to existing schema
                ReplaceWithRef(mediaType, new YamlScalarNode("schema"), existingName);
                reusedCount++;
            }
            else
            {
                // Create new schema name based on operationId
                var newName = GenerateSchemaName(operationId, schemaSuffix, newSchemas);
                newSchemas[newName] = schema;
                schemaHashToName[hash] = newName;
                
                // Replace with reference to new schema
                ReplaceWithRef(mediaType, new YamlScalarNode("schema"), newName);
                newSchemaCounter++;
            }
        }
    }

    /// <summary>
    /// Checks if a schema is worth extracting (not just a simple type or $ref).
    /// </summary>
    static bool IsExtractableSchema(YamlMappingNode schema)
    {
        // Extract if it has properties (object with defined structure)
        if (schema.Children.ContainsKey(new YamlScalarNode("properties")))
            return true;
        
        // Extract if it's an object type
        if (schema.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode))
        {
            var type = (typeNode as YamlScalarNode)?.Value;
            if (type == "object")
                return true;
        }
        
        // Extract if it has anyOf/oneOf/allOf with inline content
        if (schema.Children.ContainsKey(new YamlScalarNode("anyOf")) ||
            schema.Children.ContainsKey(new YamlScalarNode("oneOf")) ||
            schema.Children.ContainsKey(new YamlScalarNode("allOf")))
            return true;
        
        return false;
    }

    /// <summary>
    /// Generates a unique schema name based on operationId and suffix.
    /// </summary>
    static string GenerateSchemaName(string operationId, string suffix, Dictionary<string, YamlNode> existingNewSchemas)
    {
        // Clean up operationId to make a valid schema name
        // e.g., "Categories.Category.ListCategory" -> "CategoriesCategoryListCategory"
        var baseName = string.Concat(operationId.Split('.').Select(p => 
            string.IsNullOrEmpty(p) ? "" : char.ToUpperInvariant(p[0]) + p[1..]));
        
        var name = baseName + suffix;
        
        // Ensure uniqueness
        var counter = 1;
        var originalName = name;
        while (existingNewSchemas.ContainsKey(name))
        {
            name = originalName + counter++;
        }
        
        return name;
    }

    /// <summary>
    /// Replaces a schema node with a $ref to a component.
    /// </summary>
    static void ReplaceWithRef(YamlMappingNode parent, YamlScalarNode key, string schemaName)
    {
        var refNode = new YamlMappingNode(
            new YamlScalarNode("$ref"),
            new YamlScalarNode($"#/components/schemas/{schemaName}")
        );
        parent.Children[key] = refNode;
    }

    /// <summary>
    /// Computes a hash of a schema for matching purposes.
    /// </summary>
    static string GetSchemaHash(YamlNode node)
    {
        using var writer = new StringWriter();
        var yamlStream = new YamlStream(new YamlDocument(node));
        yamlStream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    /// <summary>
    /// Removes the examples section from components.
    /// </summary>
    static string RemoveExamplesSection(string yaml)
    {
        using var reader = new StringReader(yaml);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);
        
        if (yamlStream.Documents.Count == 0)
            return yaml;
        
        var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;
        
        // Find and remove examples from components
        if (root.Children.TryGetValue(new YamlScalarNode("components"), out var componentsNode))
        {
            var components = componentsNode as YamlMappingNode;
            if (components != null)
            {
                var examplesKey = new YamlScalarNode("examples");
                if (components.Children.ContainsKey(examplesKey))
                {
                    components.Children.Remove(examplesKey);
                }
            }
        }
        
        // Serialize the modified document
        using var writer = new StringWriter();
        yamlStream.Save(writer, assignAnchors: false);
        var result = writer.ToString();
        
        // Clean up the YAML output (remove document markers if present)
        result = result.Replace("...\n", "").TrimEnd();
        if (result.StartsWith("---\n"))
            result = result.Substring(4);
        
        return result;
    }

    /// <summary>
    /// Converts OpenAPI 3.1 style type arrays (e.g., type: ['null', 'string']) 
    /// to OpenAPI 3.0 style (type: string, nullable: true).
    /// Also converts anyOf with type: 'null' to nullable: true.
    /// </summary>
    static string ConvertTypeArraysToNullable(string yaml)
    {
        using var reader = new StringReader(yaml);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);
        
        if (yamlStream.Documents.Count == 0)
            return yaml;
        
        var root = yamlStream.Documents[0].RootNode;
        
        // Recursively process all nodes
        ConvertTypeArraysInNode(root);
        
        // Serialize the modified document
        using var writer = new StringWriter();
        yamlStream.Save(writer, assignAnchors: false);
        var result = writer.ToString();
        
        // Clean up the YAML output (remove document markers if present)
        result = result.Replace("...\n", "").TrimEnd();
        if (result.StartsWith("---\n"))
            result = result.Substring(4);
        
        return result;
    }

    /// <summary>
    /// Recursively converts type arrays and anyOf null patterns to nullable: true.
    /// </summary>
    static void ConvertTypeArraysInNode(YamlNode node)
    {
        if (node is YamlMappingNode mapping)
        {
            var typeKey = new YamlScalarNode("type");
            
            // Check if type is an array (OpenAPI 3.1 style: type: ['null', 'string'])
            if (mapping.Children.TryGetValue(typeKey, out var typeNode) && typeNode is YamlSequenceNode typeArray)
            {
                // Extract non-null types and check if null is present
                var types = typeArray.Children
                    .OfType<YamlScalarNode>()
                    .Select(n => n.Value)
                    .Where(v => v != null)
                    .ToList();
                
                var hasNull = types.Contains("null");
                var nonNullTypes = types.Where(t => t != "null").ToList();
                
                if (nonNullTypes.Count == 1)
                {
                    // Replace type array with single type string
                    mapping.Children[typeKey] = new YamlScalarNode(nonNullTypes[0]);
                    
                    // Add nullable: true if null was in the array
                    if (hasNull)
                    {
                        mapping.Children[new YamlScalarNode("nullable")] = new YamlScalarNode("true");
                    }
                }
                else if (nonNullTypes.Count == 0 && hasNull)
                {
                    // Just null type - rare but handle it
                    mapping.Children.Remove(typeKey);
                    mapping.Children[new YamlScalarNode("nullable")] = new YamlScalarNode("true");
                }
                // If multiple non-null types, leave as-is (unusual case)
            }
            
            // Check for anyOf/oneOf with type: 'null' entry
            foreach (var key in new[] { "anyOf", "oneOf" })
            {
                var keyNode = new YamlScalarNode(key);
                if (mapping.Children.TryGetValue(keyNode, out var arrayNode) && arrayNode is YamlSequenceNode sequence)
                {
                    // Find and remove { type: 'null' } entries, add nullable: true instead
                    var nullEntryIndex = -1;
                    for (int i = 0; i < sequence.Children.Count; i++)
                    {
                        if (sequence.Children[i] is YamlMappingNode itemMapping &&
                            itemMapping.Children.Count == 1 &&
                            itemMapping.Children.TryGetValue(typeKey, out var itemType) &&
                            itemType is YamlScalarNode itemTypeScalar &&
                            itemTypeScalar.Value == "null")
                        {
                            nullEntryIndex = i;
                            break;
                        }
                    }
                    
                    if (nullEntryIndex >= 0)
                    {
                        // Remove the null type entry
                        sequence.Children.RemoveAt(nullEntryIndex);
                        
                        // Add nullable: true to the parent mapping
                        mapping.Children[new YamlScalarNode("nullable")] = new YamlScalarNode("true");
                        
                        // If only one item left in anyOf/oneOf, we could simplify further
                        // but for now just leave it as anyOf with one item + nullable
                    }
                }
            }
            
            // Recurse into all children
            foreach (var child in mapping.Children.Values)
            {
                ConvertTypeArraysInNode(child);
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (var child in sequence.Children)
            {
                ConvertTypeArraysInNode(child);
            }
        }
    }

    /// <summary>
    /// Simplifies oneOf declarations by picking a single type based on priority:
    /// $ref > number > string
    /// </summary>
    static (string yaml, int count) SimplifyOneOfDeclarations(string yaml)
    {
        using var reader = new StringReader(yaml);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);
        
        if (yamlStream.Documents.Count == 0)
            return (yaml, 0);
        
        var root = yamlStream.Documents[0].RootNode;
        
        int simplifiedCount = 0;
        SimplifyOneOfInNode(root, ref simplifiedCount);
        
        // Serialize the modified document
        using var writer = new StringWriter();
        yamlStream.Save(writer, assignAnchors: false);
        var result = writer.ToString();
        
        // Clean up the YAML output (remove document markers if present)
        result = result.Replace("...\n", "").TrimEnd();
        if (result.StartsWith("---\n"))
            result = result.Substring(4);
        
        return (result, simplifiedCount);
    }

    /// <summary>
    /// Recursively simplifies oneOf declarations.
    /// Priority: $ref > number > string
    /// </summary>
    static void SimplifyOneOfInNode(YamlNode node, ref int count)
    {
        if (node is YamlMappingNode mapping)
        {
            var oneOfKey = new YamlScalarNode("oneOf");
            
            if (mapping.Children.TryGetValue(oneOfKey, out var oneOfNode) && oneOfNode is YamlSequenceNode oneOfArray)
            {
                // Find the best option based on priority
                YamlMappingNode? refOption = null;
                YamlMappingNode? numberOption = null;
                YamlMappingNode? stringOption = null;
                bool hasNullable = false;
                
                foreach (var item in oneOfArray.Children.OfType<YamlMappingNode>())
                {
                    // Check for $ref
                    if (item.Children.ContainsKey(new YamlScalarNode("$ref")))
                    {
                        refOption = item;
                    }
                    // Check for type
                    else if (item.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode) && 
                             typeNode is YamlScalarNode typeScalar)
                    {
                        var typeValue = typeScalar.Value;
                        if (typeValue == "number" || typeValue == "integer")
                        {
                            numberOption = item;
                        }
                        else if (typeValue == "string")
                        {
                            stringOption = item;
                        }
                    }
                    
                    // Check if any option has nullable
                    if (item.Children.TryGetValue(new YamlScalarNode("nullable"), out var nullableNode) &&
                        nullableNode is YamlScalarNode nullableScalar &&
                        nullableScalar.Value == "true")
                    {
                        hasNullable = true;
                    }
                }
                
                // Pick the best option based on priority
                YamlMappingNode? selectedOption = refOption ?? numberOption ?? stringOption;
                
                if (selectedOption != null)
                {
                    // Remove oneOf
                    mapping.Children.Remove(oneOfKey);
                    
                    // Copy properties from selected option to parent
                    foreach (var kvp in selectedOption.Children)
                    {
                        // Skip nullable if we're going to add it separately
                        if (kvp.Key is YamlScalarNode keyScalar && keyScalar.Value == "nullable")
                            continue;
                        mapping.Children[kvp.Key] = kvp.Value;
                    }
                    
                    // Add nullable if any option had it
                    if (hasNullable)
                    {
                        mapping.Children[new YamlScalarNode("nullable")] = new YamlScalarNode("true");
                    }
                    
                    count++;
                }
            }
            
            // Recurse into all children (use ToList to avoid modification during iteration)
            foreach (var child in mapping.Children.Values.ToList())
            {
                SimplifyOneOfInNode(child, ref count);
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (var child in sequence.Children)
            {
                SimplifyOneOfInNode(child, ref count);
            }
        }
    }

    /// <summary>
    /// Renames schema names that conflict with .NET types.
    /// For example, StreamContent conflicts with System.Net.Http.StreamContent.
    /// </summary>
    static string RenameConflictingSchemas(string yaml)
    {
        // Map of conflicting names to their replacements
        var renames = new Dictionary<string, string>
        {
            { "StreamContent", "ODataStreamContent" },
            { "HttpContent", "ODataHttpContent" },
            { "ByteArrayContent", "ODataByteArrayContent" },
            { "StringContent", "ODataStringContent" },
            { "FormUrlEncodedContent", "ODataFormUrlEncodedContent" },
            { "MultipartContent", "ODataMultipartContent" },
            { "MultipartFormDataContent", "ODataMultipartFormDataContent" }
        };
        
        var result = yaml;
        
        foreach (var (oldName, newName) in renames)
        {
            // Replace schema definitions (e.g., "    StreamContent:")
            result = Regex.Replace(result, $@"^(\s+){oldName}:", $"$1{newName}:", RegexOptions.Multiline);
            
            // Replace $ref references
            result = result.Replace($"'#/components/schemas/{oldName}'", $"'#/components/schemas/{newName}'");
            result = result.Replace($"\"#/components/schemas/{oldName}\"", $"\"#/components/schemas/{newName}\"");
            result = result.Replace($"#/components/schemas/{oldName}", $"#/components/schemas/{newName}");
            
            // Replace title references
            result = result.Replace($"title: {oldName}", $"title: {newName}");
        }
        
        return result;
    }

}
