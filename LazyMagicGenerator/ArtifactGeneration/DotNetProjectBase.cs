using FluentValidation;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static LazyMagic.DotNetUtils;

namespace LazyMagic
{
    public class DotNetProjectBase : ArtifactBase
    {
        
        public override string OutputFolder { get; set; } = "Containers";
        public virtual List<string> PackageReferences { get; set; } = new List<string>();
        public virtual List<string> ProjectReferences { get; set; } = new List<string>();
        public virtual List<string> ServiceRegistrations { get; set; } = new List<string>();
        public virtual List<string> GlobalUsings { get; set; } = new List<string>();
        public virtual List<string> Dependencies { get; set; } = new List<string>();
        public virtual string ExportedProjectPath { get; set; } = "";
        public virtual string ExportedPackage { get; set; } = "";   
        public virtual List<string> ExportedPackages { get; set; } = new List<string>();
        public virtual List<string> ExportedServiceRegistrations { get; set; } = new List<string>();
        public virtual List<string> ExportedInterfaces { get; set; } = new List<string>();
        public virtual List<string> ExportedGlobalUsings { get; set; } = new List<string>();
        public virtual List<string> ExportedOpenApiSpecs { get; set; } = new List<string>();
        public virtual List<(string path,List<string> operations)> ExportedPathOps { get; set; } = new List<(string path, List<string> operations)>();

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {             
            await base.GenerateAsync(solution, directiveArg);
        }
        protected void GenerateCommonProjectFiles(string sourceProjectDir, string targetProjectDir)
        {

            GenerateProjectsPropsFile(
                ProjectReferences,
                Path.Combine(targetProjectDir, "Projects.g.props"));

            GeneratePackagesPropsFile(
                PackageReferences,
                Path.Combine(targetProjectDir, "Packages.g.props"));

            // GlobalUsing.g.cs file 
            GenerateGlobalUsingFile(
                GlobalUsings,
                File.ReadAllText(Path.Combine(sourceProjectDir, "GlobalUsing.g.cs")),
                Path.Combine(targetProjectDir, "GlobalUsing.g.cs"));

            // User.props file if it does not exist. We create it to remind the user
            // they can use it to extend their project.
            GenerateUserPropsFile(
                "",
                Path.Combine(targetProjectDir, "User.props"));

            // LICENSE.TXT file if it does not exist. We create it to remind the user
            // they should add a License file to their project.
            GenerateLicenseFile(
                "",
                Path.Combine(targetProjectDir, "LICENSE.TXT"));
        }

    }

    public class  DotNetProjectValidator : AbstractValidator<DotNetProjectBase>
    {
        
    }
}
