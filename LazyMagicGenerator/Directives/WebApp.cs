using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LazyMagic
{
    public class WebApp : DirectiveBase
    {
        public WebApp()  { }

        #region Properties
        public List<string> Apis { get; set; } = new List<string>();
        public string Path { get; set; }    
        #endregion
        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());
        public override void Validate(Directives directives)
        {
            WebApp webapp = this;
            WebAppValidator validator = new WebAppValidator(directives);
            validator.ValidateAndThrow(webapp);
        }
    }
    public class WebAppValidator : AbstractValidator<WebApp>
    {
        private readonly Directives _directives;

        public WebAppValidator(Directives directives)
        {
            _directives = directives;

            RuleFor(webapp => webapp.Apis)
                .Must(apis => apis.All(api => _directives.ContainsKey(api)))
                .WithMessage((webapp, apis) =>
                    $"WebApp: {webapp.Key} references missing apis: {string.Join(", ", apis.Where(api => !_directives.ContainsKey(api)))}");
        }
    }
}
