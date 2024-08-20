using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LazyMagic
{
    public static class ArtifactUtils
    {
        public static List<string> GetModulesForApi(Api api, Directives directives)
        {
            List<string> modules = new List<string>();
            foreach (var container in api.Containers)
            {
                var containerDirective = directives[container] as Container;
                if (containerDirective == null)
                    throw new Exception($"Container {container} not found in directives.");
                modules.AddRange(containerDirective.Modules);
            }
            return modules;
        }

        public static List<string> GetSchemasForApi(Api api, Directives directives)
        {
            List<string> schemas = new List<string>();
            foreach (var container in api.Containers)
            {
                var containerDirective = directives[container] as Container;
                if (containerDirective == null)
                    throw new Exception($"Container {container} not found in directives.");
                foreach (var module in containerDirective.Modules)
                {
                    var moduleDirective = directives[module] as Module;
                    if (moduleDirective == null)
                        throw new Exception($"Module {module} not found in directives.");
                    schemas.AddRange(moduleDirective.Schemas);
                }
            }
            return schemas.Distinct().ToList();
        }
        public static List<string> GetDependentApiSpecs(List<string> containers, Directives directives)
        {
            List<string> dependentApiSpecs = new List<string>();
            foreach (var container in containers)
            {
                var containerDirective = directives[container] as Container;
                if (containerDirective == null)
                    throw new Exception($"Container {container} not found in directives.");
                foreach (var module in containerDirective.Modules)
                {
                    var moduleDirective = directives[module] as Module;
                    if (moduleDirective == null)
                        throw new Exception($"Module {module} not found in directives.");
                    dependentApiSpecs.AddRange(moduleDirective.OpenApiSpecs);
                    foreach (var schema in moduleDirective.Schemas)
                    {
                        var schemaDirective = directives[schema] as Schema;
                        if (schemaDirective == null)
                            throw new Exception($"Schema {schema} not found in directives.");
                        dependentApiSpecs.AddRange(schemaDirective.OpenApiSpecs);
                    }
                }
            }
            return dependentApiSpecs.Distinct().ToList();
        }

        public static List<string> GetApisForContainer(SolutionBase solution, string containerKey)
        {
            var apis = new List<string>();
            foreach (var directive in solution.Directives.Values)
            {
                if (directive is Api)
                {
                    var api = directive as Api;
                    if (api.Containers.Contains(containerKey))
                        apis.Add(api.Key);
                }
            }
            return apis.Distinct().ToList();
        }

        public static List<string> GetContainers(SolutionBase solution, Service directive)
        {
            var lambdas = new List<string>();
            foreach (var apiKey in directive.Apis)
                if (solution.Directives.TryGetValue(apiKey, out DirectiveBase apiDirective))
                {
                    if (!(apiDirective is Api))
                        throw new Exception($"Error generating AwsService: {directive.Key}, {apiKey} is not an Api.");

                    foreach (var containerKey in ((Api)apiDirective).Containers)
                    {
                        if (solution.Directives.TryGetValue(containerKey, out DirectiveBase container))
                        {
                            if (!(container is Container))
                                throw new Exception($"Error generating AwsService: {directive.Key}, {containerKey} is not a Container.");
                            lambdas.Add(containerKey);
                        }
                    }
                }
                else
                    throw new Exception($"Error generating AwsService: {directive.Key}, Api {apiKey} not found.");
            return lambdas.Distinct().ToList();
        }
    }
}
