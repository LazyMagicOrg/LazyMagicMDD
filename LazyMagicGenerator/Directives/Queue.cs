using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;
using NSwag;
using System.Threading.Tasks;

namespace LazyMagic
{
    public class Queue : DirectiveBase
    {
        public Queue() { }

        #region Properties
        public List<string> Containers { get; set; } = new List<string>();
        #endregion
        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, this.GetType());
        public override void Validate(Directives directives)
        {
            base.Validate(directives);
        }
    }
    public class QueueValidator : AbstractValidator<Queue>
    {
        public QueueValidator()
        {
        }
    }
}