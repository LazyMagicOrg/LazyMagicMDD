using FluentValidation;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace LazyMagic
{
    public class Container : DirectiveBase
    {
        public Container() { }

        #region Properties 
        public List<string> Modules { get; set; } = new List<string>();
        public string Runtime { get; set; }
        public string ApiPrefix { get; set; }  
        #endregion

        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());    

        public override void Validate(Directives directives)
        {
            base.Validate(directives);
        }
    }

    public class ContainerValidator : AbstractValidator<Container>
    {
        public ContainerValidator()
        {

        }
    }
}