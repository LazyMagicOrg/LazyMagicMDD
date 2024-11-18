using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LazyMagic
{
    public class Api : DirectiveBase
    {
        public Api() { }

        #region Properties
        public List<string> OpenApiSpecs { get; set; } = new List<string>();
        public List<string> Containers { get; set; } = new List<string>();
        public string Authentication { get; set; } = null;
        #endregion

        public async Task ProcessAsync( LzSolution solution)
        {
            await Task.Delay(0);
        }
        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, typeof(Api));

        public override void Validate(Directives directives)
        {
            base.Validate(directives);
        }


    }
    public class ApiValidator : AbstractValidator<Api>
    {
        public ApiValidator()
        {

        }
    }
}