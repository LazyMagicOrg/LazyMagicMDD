using FluentValidation;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LazyMagic
{
    public class Service : DirectiveBase
    {
        public Service()  { }

        #region Properties
        public List<string> Apis { get; set; } = new List<string>();
        public string Name { get; set; }
        #endregion

        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());   

        public override void Validate(Directives directives)
        {
            base.Validate(directives);
        }
    }

    public class ServiceValidator : AbstractValidator<Service>
    {
        public ServiceValidator()
        {

        }
    }

}