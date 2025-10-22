using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LazyMagic
{
    public class Api : DirectiveBase
    {
        public Api() { }

        #region Properties
        public List<string> OpenApiSpecs { get; set; } = new List<string>();
        public List<string> Containers { get; set; } = new List<string>();
        public string Authentication { get; set; } = null; // Deprecated: Use Authenticators for multi-auth support
        public List<string> Authenticators { get; set; } = new List<string>();
        #endregion

        public async Task ProcessAsync( LzSolution solution)
        {
            await Task.Delay(0);
        }
        public override void AssignDefaults(Directives directives) => AssignDefaults(directives, typeof(Api));

        public override void Validate(Directives directives)
        {
            ApiValidator.Validate(this, directives);
        }


    }
    public class ApiValidator : AbstractValidator<Api>
    {
        public static void Validate(Api api, Directives directives)
        {
            var missingContainers = api.Containers
                .Where(container => !directives.ContainsKey(container))
                .ToList();

            if (missingContainers.Any())
            {
                throw new ArgumentException(
                    $"Directive File Validator Error: Api: {api.Key} references missing containers: {string.Join(", ", missingContainers)}");
            }
        }
    }
}