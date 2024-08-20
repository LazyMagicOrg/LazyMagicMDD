using FluentValidation;
using NSwag;
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
            base.Validate(directives);
        }
    }
    public class ModuleValidator : AbstractValidator<Module>
    {
        public ModuleValidator()
        {
        }
    }
}