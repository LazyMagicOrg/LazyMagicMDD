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
