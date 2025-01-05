using FluentValidation;
using LazyMagicGenerator.Directives;
using NSwag;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace LazyMagic
{
    public class Module : DirectiveBase
    {
        public Module() { }

        #region Properties
        public List<string> OpenApiSpecs { get; set; } = new List<string>();
        public string OpenApiSpec { get; set; } = "";
        public List<string> Schemas { get; set; } = new List<string>();
        #endregion
        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());
        public override void Validate(Directives directives)
        {
            Module module = this;
            ModuleValidator validator = new ModuleValidator(directives);
            validator.ValidateAndThrow(module);
        }
    }
    public class ModuleValidator : AbstractValidator<Module>
    {
        private readonly Directives _directives;

        public ModuleValidator(Directives directives)
        {
            _directives = directives;

            //Validate Artifacts
            RuleFor(module => module.Artifacts)
                .Custom((artifacts, context) =>
                {
                    foreach (var artifact in artifacts.Values)
                    {
                        if (!(artifact is IModuleArtifact))
                        {
                            context.AddFailure($"Artifact {artifact.GetType().Name} is not a valid Module artifact. Module Artifacts must implement the IModuleArtifact marker interface");
                        }
                    }
                });
        }
    }
}