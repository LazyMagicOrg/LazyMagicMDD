using FluentValidation.Results;
using Namotion.Reflection;
using NJsonSchema;
using NSwag;
using System.Collections.Generic;
using System.Linq;

namespace LazyMagic
{
    public static class OpenApiExtensions
    {
        public static List<string> GetUsedEntities(OpenApiDocument document)
        {
            var referencedSchemas = new HashSet<string>();

            // Iterate through all paths and operations
            foreach (var path in document.Paths)
            {
                foreach (var operation in path.Value)
                {
                    // Check request body schemas
                    if (operation.Value.RequestBody?.Content != null)
                    {
                        foreach (var content in operation.Value.RequestBody.Content.Values)
                        {
                            var schema = content.Schema.ActualSchema;
                            if(schema != null)
                            {
                                var schemaname = document.Components.Schemas.Where(x => object.ReferenceEquals(x.Value, schema)).FirstOrDefault().Key;
                                if (!string.IsNullOrEmpty(schemaname))
                                {
                                    referencedSchemas.Add(schemaname);
                                }
                            }
                            //var content = contentKVP.Value;
                            //if (content.Schema?.Reference != null)
                            //{
                            //    var schemaname = document.Components.Schemas.Where(x => object.ReferenceEquals(x.Value, content.Schema)).FirstOrDefault().Key;
                            //    if(!string.IsNullOrEmpty(schemaname))
                            //    {
                            //        referencedSchemas.Add(schemaname);
                            //    }

                            //}
                        }
                    }

                    // Check response schemas
                    foreach (var response in operation.Value.Responses)
                    {
                        if (response.Value.Content != null)
                        {
                            foreach (var content in response.Value.Content.Values)
                            {
                                if (content.Schema?.Reference != null)
                                {
                                    referencedSchemas.Add(content.Schema.Reference.Id);
                                }
                            }
                        }
                    }

                    // Check parameter schemas
                    foreach (var parameter in operation.Value.Parameters)
                    {
                        if (parameter.Schema?.Reference != null)
                        {
                            referencedSchemas.Add(parameter.Schema.Reference.Id);
                        }
                    }
                }
            }

            // Iterate through components and find all schemas with references
            foreach (var schema in document.Components.Schemas)
            {
                if (schema.Value.Reference != null)
                {
                    referencedSchemas.Add(schema.Value.Reference.Id);
                }
            }

            return referencedSchemas.ToList();
        }
    }
}
