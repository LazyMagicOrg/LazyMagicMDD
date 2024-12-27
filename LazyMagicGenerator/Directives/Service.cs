using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LazyMagic
{
    public class Service : DirectiveBase
    {
        public Service()  { }

        #region Properties
        public List<string> Apis { get; set; } = new List<string>();
        public string WSApi { get; set; } = "";
        public string Name { get; set; }
        #endregion

        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());

        public override void Validate(Directives directives)
        {
            Service service = this;
            ServiceValidator validator = new ServiceValidator(directives);
            validator.ValidateAndThrow(service);
        }
    }

    public class ServiceValidator : AbstractValidator<Service>
    {
        private readonly Directives _directives;

        public ServiceValidator(Directives directives)
        {
            _directives = directives;

            RuleFor(service => service.Apis)
                .Must(apis => apis.All(api => _directives.ContainsKey(api)))
                .WithMessage((service, apis) =>
                    $"Service: {service.Key} references missing apis: {string.Join(", ", apis.Where(api => !_directives.ContainsKey(api)))}");
        }
    }

}