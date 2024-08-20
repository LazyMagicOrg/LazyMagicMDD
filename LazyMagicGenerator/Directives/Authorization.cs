using FluentValidation;
using System.Collections.Generic;

namespace LazyMagic
{
    public class Authorization : DirectiveBase
    {
        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());
        public override void Validate(Directives directives)
        {
            base.Validate(directives);
        }
    }
    public class AuthorizationValidator : AbstractValidator<Authorization>
    {
        public AuthorizationValidator() { }
    }   

}
