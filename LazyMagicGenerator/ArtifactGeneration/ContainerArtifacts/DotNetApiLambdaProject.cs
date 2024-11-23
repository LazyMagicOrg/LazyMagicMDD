using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.OpenApiUtils;
using System.Text;

namespace LazyMagic
{

    public class DotNetApiLambdaProject : DotNetProjectBase
    {
        #region Properties
        public override string Template { get; set; } = "ProjectTemplates/ApiLambda";
        public string ExportedApiPrefix { get; set; } = "";    
        public string ExportedOpenApiSpec { get; set; } = "";   
        #endregion
        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var projectName = "";
            try
            {
                Container directive = (Container)directiveArg;

                // Set the project name and namespace
                projectName = directive.Key + NameSuffix ?? "";
                var nameSpace = projectName;
                await InfoAsync($"Generating {directive.Key} {projectName}");

                // Set the containers prefix - this is used to create unique paths. ex: {prefix}/yada => /api/yada
                // Note that we do not use the artifcat ProjectName because the prefix needs to be consistent
                // at the Service level. See usage in DotNetLocalWebApiProject for example.
                var prefix = directive.ApiPrefix ?? directive.Key;

                // Get controller Dependencies
                var controllerArtifacts = solution.Directives.GetArtifactsByType<DotNetControllerProject>(directive.Modules).ToList<ArtifactBase>(); 

                // Get Dependencies
                ProjectReferences.AddRange(GetExportedProjectReferences(controllerArtifacts));
                // PackageReferences.AddRange(GetExportedPackageReferences(controllerArtifacts));
                GlobalUsings.AddRange(GetExportedGlobalUsings(controllerArtifacts));
                ServiceRegistrations.AddRange(GetExportedServiceRegistrations(controllerArtifacts));

                // Copy the template project to the target project. Removes *.g.* files.
                var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, Template);
                var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(OutputFolder, projectName));
                var csprojFileName = GetCsprojFile(sourceProjectDir);
                var filesToExclude = new List<string> { csprojFileName, "User.props", "SRCREADME.md", "ConfigureSvc.g.cs" };
                CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

                // Create/Update the Repo.csproj file.
                
                File.Copy(
                    Path.Combine(sourceProjectDir, csprojFileName),
                    Path.Combine(targetProjectDir, projectName + ".csproj"),
                    overwrite: true);

                GenerateCommonProjectFiles(sourceProjectDir, targetProjectDir);

                var controllerProjects = GetControllerProjects(directive, solution.Directives);

                // Generate the openapi.yaml file with {prefix} replaced with the api prefix
                var openAipSpecs = new List<string>();
                foreach (var controllerProject in controllerProjects)
                    if (!string.IsNullOrEmpty(controllerProject.ExportedOpenApiSpec))
                        openAipSpecs.Add(controllerProject.ExportedOpenApiSpec);
                var openApiSpec = await MergeApiFilesAsync(solution.SolutionRootFolderPath, openAipSpecs);
                openApiSpec = openApiSpec.Replace("{prefix}", prefix);    
                File.WriteAllText(Path.Combine(targetProjectDir, "openapi.g.yaml"), openApiSpec);

                GenerateConfigureSvcsFile(projectName, nameSpace, Path.Combine(targetProjectDir, "ConfigureSvcs.g.cs"));

                // Exports
                ProjectFilePath = Path.Combine(OutputFolder, projectName, projectName + ".csproj");
                ExportedGlobalUsings = GlobalUsings;
                foreach(var contollerProject in controllerProjects)
                {
                    ExportedOpenApiSpecs.AddRange(contollerProject.ExportedOpenApiSpecs);
                    ExportedPathOps.AddRange(contollerProject.ExportedPathOps);
                }
                ExportedGlobalUsings.Add(nameSpace);
                ExportedGlobalUsings = ExportedGlobalUsings.Distinct().ToList(); // note: used by DotNetClientSDKProject
                ExportedOpenApiSpecs = ExportedOpenApiSpecs.Distinct().ToList(); // note: used by DotNetClientSDKProject
                foreach (var controllerArtificat in controllerArtifacts)
                    ExportedPathOps.AddRange(((DotNetControllerProject)controllerArtificat).ExportedPathOps);

                ExportedApiPrefix = prefix;
                ExportedName = projectName; // TODO: Check usage. This may be redundant.
                ExportedOpenApiSpec = Path.Combine(targetProjectDir, "openapi.g.yaml");

            } catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} for {projectName}, {ex.Message}");
            }
        }
        private List<DotNetControllerProject> GetControllerProjects(Container directive, Directives directives)
        {
            var projects = new List<DotNetControllerProject>(); 
            foreach(var moduleName in directive.Modules)
            {
                var module = (Module)directives[moduleName];
                foreach(var artifact in module.Artifacts.Values.Where(x => x is DotNetControllerProject))
                {
                    projects.Add((DotNetControllerProject)artifact);
                }
            }
            return projects;
        }

        private void GenerateConfigureSvcsFile(string projectName, string nameSpace, string filePath)
        {
            var registrations = new List<string>();
            ServiceRegistrations.ForEach(x => registrations.Add($"services.{x}();"));
            var template = $@"
// Generated by LazyMagic - modifications will be overwritten
namespace LambdaFunc;
public partial class Startup
{{
    public void ConfigureSvcs(IServiceCollection services)
    {{
        {string.Join("\r\n\t\t", registrations)}
    }}
}}
";

            File.WriteAllText(filePath, template);
        }
    }
}
;