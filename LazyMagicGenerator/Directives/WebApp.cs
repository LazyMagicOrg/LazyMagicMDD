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
            WebAppValidator.Validate(this, directives);
        }
    }
    public class WebAppValidator : AbstractValidator<WebApp>
    {
        public static void Validate(WebApp webapp, Directives directives)
        {
            var missingApis = webapp.Apis
                .Where(api => !directives.ContainsKey(api))
                .ToList();

            if (missingApis.Any())
            {
                throw new ArgumentException(
                    $"Directive File Validator Error: WebApp: {webapp.Key} references missing api(s): {string.Join(", ", missingApis)}");
            }
        }
    }
}
