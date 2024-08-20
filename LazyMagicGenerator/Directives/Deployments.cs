using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace LazyMagic
{
    public class Deployment : DirectiveBase
    {
        public Deployment()  { }

        #region Properties
        public string Service { get; set; }
        public List<string> Tenancies { get; set; } = new List<string>();
        public string Environment { get; set; }
        #endregion
        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());
        public override void Validate(Directives directives)
        {
            base.Validate(directives);
        }
    }
    public class DeploymentValidator : AbstractValidator<Deployment>
    {
        public DeploymentValidator()
        {

        }
    }
}
