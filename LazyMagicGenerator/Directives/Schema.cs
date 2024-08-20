using FluentValidation;
using NSwag;
using System.Collections.Generic;


namespace LazyMagic
{
    /// <summary>
    /// The Schema defaultDirective generates:
    /// Repository Project
    /// Schema Project
    ///     - DTOs
    ///     - Models
    ///         - model class derived from DTO 
    ///         - model validator class for model class
    ///     - Validators
    /// Directive Properties:
    /// OpenApiSpecs: List of OpenApiSpecs to load
    /// Package: Package name for the schema project
    /// 
    /// Yaml Example:
    /// Directives:
    ///   SchemaDefault:
    ///     Type: SchemaDefault
    ///     Artifacts:
    ///       SchemaProject:
    ///         Type: DotNetSchema
    ///         Template: ./ProjectTemplates/Schema
    ///         OutputFolder: ./Schemas
    ///       RepoProject:
    ///         Type: DotNetRepo
    ///         Template: ./ProjectTemplates/Repo
    ///         OutputFolder: ./Repos
    ///         
    ///   NotificationsSchema:        
    ///     Type: Schema
    ///     Defaults: SchemaDefault
    ///     OpenApiSpecs:
    ///     - ./openapi.notifications-schema.yaml
    ///       
    /// </summary>
    public class Schema : DirectiveBase
    {
        public Schema() { }
        public List<string> OpenApiSpecs { get; set; } = new List<string>();
        public List<string> Schemas { get; set; } = new List<string>();
        public List<string> Entities { get; set; } = new List<string>();  
        public List<string> ReferencedEntities { get; set; } = new List<string>();
        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());
        public override void Validate(Directives directives)
        {
            base.Validate(directives);
        }
        
    }
    public class SchemaValidator : AbstractValidator<Schema>
    {
        public SchemaValidator() { }
    }

}