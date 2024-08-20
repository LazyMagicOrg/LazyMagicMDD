using FluentValidation;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.OpenApiUtils;

namespace LazyMagic
{
    public class DotNetProjectBase : ArtifactBase
    {
        public override string Family { get; set; } = "DotNetProject";
        public string ProjectName { get; set; } = null;
        public string Namespace { get; set; } = null;
        public string Runtime { get; set; } = ""; 
        public List<string> PackageReferences { get; set; } = new List<string>();
        public List<string> ProjectReferences { get; set; } = new List<string>();
        public List<string> ServiceRegistrations { get; set; } = new List<string>();
        public List<string> GlobalUsings { get; set; } = new List<string>();
        public List<string> Dependencies { get; set; } = new List<string>();
        public string ExportedProjectPath { get; set; } = "";
        public string ExportedPackage { get; set; } = "";   
        public List<string> ExportedPackages { get; set; } = new List<string>();
        public List<string> ExportedServiceRegistrations { get; set; } = new List<string>();
        public List<string> ExportedInterfaces { get; set; } = new List<string>();
        public List<string> ExportedGlobalUsings { get; set; } = new List<string>();
        public List<string> ExportedOpenApiSpecs { get; set; } = new List<string>();
        public List<(string path,List<string> operations)> ExportedPathOps { get; set; } = new List<(string path, List<string> operations)>();

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
