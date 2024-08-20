using FluentValidation;
using System;
using System.Collections.Generic;
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
            base.Validate(directives);
        }
    }
    public class WebAppValidator : AbstractValidator<WebApp>
    {
        public WebAppValidator()
        {

        }
    }
}
