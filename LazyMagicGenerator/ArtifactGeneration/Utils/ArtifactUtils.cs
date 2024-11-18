using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LazyMagic
{
    public static class ArtifactUtils
    {
        public static T GetDirective<T>(Directives directives, string key) where T : class
        {
            return (directives.TryGetValue(key, out var directive)
                ? directive as T
                    ?? throw new Exception($"Directive '{key}' found but directive is not of type {typeof(T).Name}")
                : throw new Exception($"Directive '{key}' not found in directives"));
        }

        public static List<string> GetModulesForApi(Api api, Directives directives)
        {
            return api.Containers
                .Select(container => directives.TryGetValue(container, out var directive)
                    ? directive as Container
                    ?? throw new InvalidCastException($"Directive for container {container} is not of type Container.")
                    : throw new KeyNotFoundException($"Container {container} not found in directives."))
                .SelectMany(container => container.Modules)
                .ToList();
        }
        public static List<string> GetSchemasForApi(Api api, Directives directives)
        {
            return api.Containers
                .Select(container => directives.TryGetValue(container, out var directive)
                    ? directive as Container
                    : throw new KeyNotFoundException($"Container {container} not found in directives."))
                .Where(containerDirective => containerDirective != null)
                .SelectMany(containerDirective => containerDirective.Modules
                    .Select(module => directives.TryGetValue(module, out var moduleDirective)
                        ? moduleDirective as Module
                        : throw new KeyNotFoundException($"Module {module} not found in directives.")))
                .Where(moduleDirective => moduleDirective != null)
                .SelectMany(moduleDirective => moduleDirective.Schemas)
                .Distinct()
                .ToList();
        }
        public static List<string> GetDependentApiSpecs(List<string> containers, Directives directives)
        {
            return containers
                .Select(container => GetDirective<Container>(directives, container))
                .SelectMany(container => container.Modules)
                .Select(module => GetDirective<Module>(directives, module))
                .SelectMany(module => module.OpenApiSpecs.Concat(
                    module.Schemas
                        .Select(schema => GetDirective<Schema>(directives, schema))
                        .SelectMany(schema => schema.OpenApiSpecs)
                ))
                .Distinct()
                .ToList();
        }
    }
}
