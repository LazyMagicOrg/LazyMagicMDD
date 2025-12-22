using System;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;  
using System.Linq.Expressions;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;

namespace LazyMagic
{
    /// <summary>
    /// This class generates a DotNet Local WebApi Project.
    /// The LocalWebApi project is a WebApi project aggregates 
    /// all the Modules (DotNetController) for the Containers 
    /// (DotNetLambda) referenced by the Service.Apis property.
    /// 
    /// It generates a CustomRoutiingMiddleware class that 
    /// handles removing the {prefix} from the request path.
    /// Remember that each 
    /// 
    /// Note that this simple development service does not 
    /// execute code in exactly the same way as the Containers 
    /// executing in other Services. For instance; an AWS ApiGateway 
    /// calling AWS Lambdas will have multiple Lambda instances running 
    /// in the cloud, with each of those making concurrent calls to 
    /// shared resources like DynamoDB. This service will not 
    /// simulate that behavior.
    /// 
    /// </summary>
    public class DotNetLocalWebApiProject : DotNetProjectBase
    {

        #region Properties
        public override string Template { get; set; } = "WebApi";
        public override string OutputFolder { get; set; } = "";
        public override string ProjectFilePath
        {
            get => ExportedProjectPath;
            set => ExportedProjectPath = value;
        }
        #endregion

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var projectName = directiveArg.Key + NameSuffix ?? "";
            try
            {

                Service directive = (Service)directiveArg;
                var outputFolder = OutputFolder ?? "";

                // Set the project name and namespace
                var nameSpace = projectName;
                await InfoAsync($"Generating {directive.Key} {projectName}");

                var apiDirectives = new Dictionary<string, Api>();
                var containerDirectives = new Dictionary<string, Container>();
                var containerPrefixes = new List<string>();
                var moduleDirectives = new Dictionary<string, Module>();
                var controllerArtifacts = new List<DotNetControllerProject>();
                // Drill into the directives to get all the relavant apis, containers, and modules
                // that we can debug locally.
                var apiDirectiveBases = solution.Directives.GetDirectivesByName(directive.Apis);
                foreach (var apiDirectiveBase in apiDirectiveBases)
                {
                    await InfoAsync($"Processing {apiDirectiveBase.Key}");
                    if (apiDirectives.ContainsKey(apiDirectiveBase.Key)) continue;

                    var containerDirectiveBases = solution.Directives.GetDirectivesByName(((Api)apiDirectiveBase).Containers);
                    foreach (var containerDirectiveBase in containerDirectiveBases)
                    {
                        if (containerDirectives.ContainsKey(containerDirectiveBase.Key)) continue;
                        containerPrefixes.Add(((Container)containerDirectiveBase).ApiPrefix ?? containerDirectiveBase.Key);

                        containerDirectives.Add(containerDirectiveBase.Key, (Container)containerDirectiveBase);
                        var moduleDirectiveBases = solution.Directives.GetDirectivesByName(((Container)containerDirectiveBase).Modules);
                        foreach (var moduleDirectiveBase in moduleDirectiveBases)
                        {
                            if (moduleDirectives.ContainsKey(moduleDirectiveBase.Key)) continue;
                            moduleDirectives.Add(moduleDirectiveBase.Key, (Module)moduleDirectiveBase);
                            var _controllerArtifactBases = solution.Directives.GetArtifactsByType<DotNetControllerProject>(moduleDirectiveBase.Key);
                            foreach (var controllerArtifactBase in _controllerArtifactBases)
                                controllerArtifacts.Add((DotNetControllerProject)controllerArtifactBase);
                        }
                    }
                }
                var controllerArtifactBases = controllerArtifacts.Cast<ArtifactBase>().ToList();

                // Get Dependencies
                ProjectReferences.AddRange(GetExportedProjectReferences(controllerArtifactBases));
                PackageReferences.AddRange(GetExportedPackageReferences(controllerArtifactBases));
                GlobalUsings.AddRange(GetExportedGlobalUsings(controllerArtifactBases));
                GlobalUsings = GlobalUsings.Distinct().ToList();
                ServiceRegistrations.AddRange(GetExportedServiceRegistrations(controllerArtifactBases));

                // Copy the template project to the target project. Removes *.g.* files.
                var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, TemplatePath);
                var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(outputFolder, projectName));
                var csprojFileName = GetCsprojFile(sourceProjectDir);
                var filesToExclude = new List<string> { csprojFileName, "User.props", "SRCREADME.md", "ConfigureSvcs.t.cs" };
                CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

                // Create/Update the Repo.csproj file.
                File.Copy(
                    Path.Combine(sourceProjectDir, csprojFileName),
                    Path.Combine(targetProjectDir, projectName + ".csproj"),
                    overwrite: true);

                GenerateCommonProjectFiles(sourceProjectDir, targetProjectDir);
                RenameTemplateFiles(targetProjectDir);

                GenerateConfigureSvcsFile(projectName, nameSpace, Path.Combine(solution.SolutionRootFolderPath, outputFolder, projectName, $"ConfigureSvcs") + ".g.cs");

                // Exports
                ExportedProjectPath = Path.Combine(outputFolder, projectName, projectName + ".csproj");

            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name}: {projectName}, {ex.Message}");
            }
        }

        private void GenerateConfigureSvcsFile(string projectName, string nameSpace, string filePath)
        {
            var registrations = new List<string>();
            ServiceRegistrations.ForEach(x => registrations.Add($"services.{x}();"));
            var template = $@"
// Generated by LazyMagic - modifications will be overwritten

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
