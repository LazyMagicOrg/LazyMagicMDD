using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LazyMagic
{
    public class Tenancy : DirectiveBase
    {
        public Tenancy()  { }

        #region Properties
        public List<string> WebApps { get; set; } = new List<string>(); 
        public string Service { get; set; }
        public string TenantKey { get; set; }
        public string RootDomain { get; set; }  
        public string AcmCertificateArn { get; set; }   

        #endregion
        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());
        public override void Validate(Directives directives)
        {
            Tenancy tenancy = this;
            TenancyValidator validator = new TenancyValidator(directives);
            validator.ValidateAndThrow(tenancy);
        }
    }
    public class TenancyValidator : AbstractValidator<Tenancy>
    {
        private readonly Directives _directives;

        public TenancyValidator(Directives directives)
        {
            _directives = directives;

            RuleFor(tenancy => tenancy.WebApps)
                .Must(webapps => webapps.All(webapp => _directives.ContainsKey(webapp)))
                .WithMessage((tenancy, webapps) =>
                    $"Tenancy: {tenancy.Key} references missing webapps: {string.Join(", ", webapps.Where(webapp => !_directives.ContainsKey(webapp)))}");
        }
    }
}
