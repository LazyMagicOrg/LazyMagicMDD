using FluentValidation;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;


namespace LazyMagic
{
    public class Container : DirectiveBase
    {
        public Container() { }

        #region Properties 
        public List<string> Modules { get; set; } = new List<string>();
        public string Runtime { get; set; }
        public string ApiPrefix { get; set; }
        #endregion

        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());    

        public override void Validate(Directives directives)
        {
            ContainerValidator.Validate2(this, directives);
        }
    }

    public class ContainerValidator : AbstractValidator<Container>
    {
        
        public static void Validate(Container container, Directives directives)
        {

            foreach (string module in container.Modules)
            {
                if(!directives.ContainsKey(module))
                    throw new ArgumentException($"Module referenced by Container Key:{container.Key} not found in Directives file");
            }
        }
        public static void Validate2(Container container, Directives directives)
        {
            var missingModules = container.Modules
                .Where(module => !directives.ContainsKey(module))
                .ToList();

            if (missingModules.Any())
            {
                throw new ArgumentException(
                    $"Directive File Validator Error: Container: {container.Key} references missing module(s): {string.Join(", ", missingModules)}");
            }
        }
    }
}