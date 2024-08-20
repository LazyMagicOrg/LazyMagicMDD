using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;

namespace LazyMagic
{
    public class Authentication : DirectiveBase
    {
        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());
        public override void Validate(Directives directives)
        {
            base.Validate(directives);
        }
    }

    public class AuthenticationValidator : AbstractValidator<Authentication>
    {
        public AuthenticationValidator() { }
    }
}
