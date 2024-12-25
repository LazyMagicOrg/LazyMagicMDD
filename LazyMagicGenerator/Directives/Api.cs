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
            Api api = this;
            ApiValidator validator = new ApiValidator(directives);
            validator.ValidateAndThrow(api);
        }


    }
    public class ApiValidator : AbstractValidator<Api>
    {
        private readonly Directives _directives;

        public ApiValidator(Directives directives)
        {
            _directives = directives;

            RuleFor(api => api.Containers)
                .Must(containers => containers.All(container => _directives.ContainsKey(container)))
                .WithMessage((api, containers) =>
                    $"Api: {api.Key} references missing containers: {string.Join(", ", containers.Where(container => !_directives.ContainsKey(container)))}");
        }
    }
}