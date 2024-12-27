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
            Container container = this;
            ContainerValidator validator = new ContainerValidator(directives);
            validator.ValidateAndThrow(container);
        }
    }

    public class ContainerValidator : AbstractValidator<Container>
    {
        private readonly Directives _directives;

        public ContainerValidator(Directives directives)
        {
            _directives = directives;

            RuleFor(container => container.Modules)
                .Must(modules => modules.All(module => _directives.ContainsKey(module)))
                .WithMessage((container, modules) =>
                    $"Container: {container.Key} references missing modules: {string.Join(", ", modules.Where(module => !_directives.ContainsKey(module)))}");
        }
    }
}