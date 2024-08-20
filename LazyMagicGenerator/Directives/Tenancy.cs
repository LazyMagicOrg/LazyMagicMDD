using FluentValidation;
using System;
using System.Collections.Generic;
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
            base.Validate(directives);
        }
    }
    public class TenancyValidator : AbstractValidator<Tenancy>
    {
        public TenancyValidator()
        {

        }
    }
}
