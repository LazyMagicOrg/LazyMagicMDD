using System.Collections.Generic;
using YamlDotNet.Serialization;
using System.Threading.Tasks;
using System.Linq;
using System;
using static LazyMagic.LzLogger;
using static LazyMagic.OpenApiUtils;
using NSwag.CodeGeneration.OperationNameGenerators;
using System.Runtime.InteropServices;
using NSwag;

namespace LazyMagic
{
    /// <summary>
    /// This Dictionary holds the Directvies for the solution. 
    /// </summary>
    public class Directives : Dictionary<string, DirectiveBase>
    {
        public Directives()
        {
        }

        public List<string> GetProjectFilePaths(string family, string type)
        {
            var projectFilePaths = new List<string>();  
            foreach(var directive in this.Values)
            {
                if (directive.Type.Equals(type) && directive.IsDefault == false)
                {
                    foreach(var kvp in directive.Artifacts)
                    {
                        if (kvp.Value.Family.Equals(family))
                        {
                            projectFilePaths.Add(kvp.Value.ProjectFilePath);
                        }
                    }
                }
            }
            return projectFilePaths;
        }

        private void AssignDefaults()
        {
            foreach (var key in this.Keys.ToList())
            {
                this[key].AssignDefaults(this);
            }
        }
        public void Validate()
        {
            AssignDefaults();
            foreach (var key in this)
            {
                key.Value.Validate(this);
            }
        }
        public async Task ProcessAsync(SolutionBase solution)
        {
            // Note that the order in which we process the Directive Types is important
            await ProcessSchemasAsync(solution);
            await ProcessModulesAsync(solution);
            await ProcessAsync(solution, "Container");
            await ProcessAsync(solution, "Api");
            await ProcessAsync(solution, "Authentication");
            await ProcessAsync(solution, "Service");
            await ProcessAsync(solution, "WebApp");
            await ProcessAsync(solution, "Tenancy");
            await ProcessAsync(solution, "Deployment");

            await Task.Delay(0);    
        }
        private async Task ProcessAsync(SolutionBase solution, string type)
        {
            await InfoAsync($"Generating {type} Artifacts:");
            foreach (var directive in this.Values)
            {
                if (directive.IsDefault) continue;
                if (!directive.Type.Equals(type)) continue;
                await directive.GenerateAsync(solution);
            }
        }

        private async Task ProcessSchemasAsync(SolutionBase solution)
        {
            var msg = "";
            // Schema directives may have dependencies on other Schema directives 
            // so we have to resolve the dependeny map and process the items in the 
            // order ensuring dependent schemas are processed before those that depend on them.
            // We do this at this level instead of at the artifact processing level because 
            // this is common handling across all Schema artifacts.
            var schemas = this.Values
                .Where(x => x.Type.Equals("Schema") && !x.IsDefault)
                .Select(x => (Schema)x)
                .ToList();

            await InfoAsync("Finding Schema Entities:");
            // Get Schema dependencies
            foreach (var schema in schemas)
            {
                await InfoAsync($"Schema: {schema.Key}");
                var openApiSpecs = schema.OpenApiSpecs ?? new List<string>();
                var yamlSpec = await MergeApiFilesAsync(solution.SolutionRootFolderPath, openApiSpecs);
                var schemaEntities = GetComponentSchemaEntities(yamlSpec);
                schema.Entities = schemaEntities;
                msg = $"\tDefined: {string.Join(",", schemaEntities)}";
                await InfoAsync(msg);

                var referencedEntities = GetReferencedEntities(yamlSpec);
                referencedEntities.RemoveAll(item => schemaEntities.Contains(item));
                referencedEntities = referencedEntities.Distinct().ToList();   
                schema.ReferencedEntities = referencedEntities;
                msg = $"\tReferenced: {string.Join(",", referencedEntities)}";
                await InfoAsync(msg);

            }
            await InfoAsync("");
            await InfoAsync("Finding Schema Dependencies:");
            foreach(var schema in schemas)
            {
                await InfoAsync($"Schema: {schema.Key}");
                var schemaRefs = new List<string>();
                foreach (var entity in schema.ReferencedEntities)
                {
                    var schemaRef = schemas.FirstOrDefault(x => x.Entities.Contains(entity));
                    if (schemaRef != null)
                    {
                        schemaRefs.Add(schemaRef.Key);
                    }
                }
                schemaRefs = schemaRefs.Distinct().ToList();    
                schema.Schemas = schemaRefs;
                msg = $"\tSchemas: {string.Join(",", schemaRefs)}";
                await InfoAsync(msg);

            }

            await InfoAsync("");
            await InfoAsync("Finding Schema processing order:");

            schemas = OrderSchemasByReferences(schemas);
            var schemaNames = schemas.Select(x => x.Key).ToList();
            msg = $"Schema Order: {string.Join(",", schemaNames)}";
            await InfoAsync(msg);
            
            await InfoAsync("");
            await InfoAsync("Generating Schema Artifacts:");
            foreach(var schemaName in schemaNames)
            {
                var schema = (Schema)this[schemaName];
                await schema.GenerateAsync(solution);
            }



        }

        private async Task ProcessModulesAsync(SolutionBase solution)
        {
            await InfoAsync($"Generating Module Artifacts:");


            // Module directives may have dependencies on Schema directives 
            // so we have to resolve the schema dependencies here. We do this 
            // at this level instead of at the artifact processing level because
            // this is common handling across all Module artifacts.
            var modules = this.Values
                .Where(x => x.Type.Equals("Module") && !x.IsDefault)
                .Select(x => (Module)x)
                .ToList();
            foreach (var module in modules)
            {
                // Merge OpenApi specs - this contains the paths for this controller + the entire aggregated schema
                // Note: It's necessary to use the aggregated schema because NSWAG will fail if it can't find a schema
                // object. We won't use this aggregated schema as its' contents are already handled by the 
                // DotNetSchema and DotNetRepo projects generated from Schema directives.
                var openApiSpecs = module.OpenApiSpecs ?? new List<string>();
                var openApiSpecsYaml = await MergeApiFilesAsync(solution.SolutionRootFolderPath, openApiSpecs);
                openApiSpecsYaml = await MergeYamlAsync(
                    solution.SolutionRootFolderPath,
                    new List<string> { openApiSpecsYaml, solution.AggregateSchemas.ToYaml() }
                    );
                module.OpenApiSpec = openApiSpecsYaml;
                // Get a list of the entities referenced in the paths
                var schemaEntities = GetReferencedEntities(openApiSpecsYaml);
                // get a list of the minimal set of Schema directive references required to provide the required
                // schema entities
                var schemas = GetSchemaNamesForEntities(solution, schemaEntities);
                module.Schemas = schemas;
            }
            foreach (var module in modules)
            {
                await module.GenerateAsync(solution);   
            }
        }

        public async Task Report()
        {
            var serializer = new SerializerBuilder()
                .Build();

            foreach (var kvp in this)
            {
                var directive = kvp.Value;
                var key = kvp.Key;
                if (directive.IsDefault) continue;
                var yamlText = serializer.Serialize(directive);
                await LzLogger.InfoAsync($"{key}\n{yamlText}");
            }
        }
        public List<ArtifactBase> GetArtifactsByType(string directiveKey, string artifactType)
        {
            var directive = this.FirstOrDefault(x => x.Key.Equals(directiveKey));

            return directive.Value.Artifacts?
                .Where(x => x.Value.Type.Equals(artifactType))
                .Select(x => x.Value)
                .Distinct()
                .ToList()
                ?? new List<ArtifactBase>();
        }
        public List<ArtifactBase> GetArtifactsByType(List<string> directiveKeys, string artifactType)
        {
            if (directiveKeys == null || !directiveKeys.Any())
            {
                return new List<ArtifactBase>();
            }

            return directiveKeys
                .SelectMany(x => GetArtifactsByType(x, artifactType))
                .Distinct()
                .ToList();
        }

        public List<DirectiveBase> GetDirectives(List<string> directives)
        {
            return directives
                .Select(x => this[x])
                .ToList();
        }

        public static List<Schema> OrderSchemasByReferences(List<Schema> items)
        {
            var graph = new Dictionary<string, HashSet<string>>();
            var inDegree = new Dictionary<string, int>();

            // Build the graph and calculate in-degrees
            foreach (var item in items)
            {
                if (!graph.ContainsKey(item.Key))
                {
                    graph[item.Key] = new HashSet<string>();
                    inDegree[item.Key] = 0;
                }

                foreach (var reference in item.Schemas)
                {
                    if (!graph.ContainsKey(reference))
                    {
                        graph[reference] = new HashSet<string>();
                        inDegree[reference] = 0;
                    }
                    graph[reference].Add(item.Key);
                    inDegree[item.Key]++;
                }
            }

            // Perform topological sort
            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var result = new List<Schema>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var item = items.Find(i => i.Key == current);
                if (item != null)
                {
                    result.Add(item);
                }

                foreach (var neighbor in graph[current])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Check for cyclic dependencies
            if (result.Count != items.Count)
            {
                throw new InvalidOperationException("Cyclic dependencies detected");
            }

            return result;
        }

    }


}
