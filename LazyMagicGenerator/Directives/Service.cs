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
            ServiceValidator.Validate(this, directives);
        }
    }

    public class ServiceValidator : AbstractValidator<Service>
    {
        public static void Validate(Service service, Directives directives)
        {
            var missingApis = service.Apis
                .Where(api => !directives.ContainsKey(api))
                .ToList();

            if (missingApis.Any())
            {
                throw new ArgumentException(
                    $"Directive File Validator Error: Service: {service.Key} references missing api(s): {string.Join(", ", missingApis)}");
            }
        }
    }

}