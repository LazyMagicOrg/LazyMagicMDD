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
    public class AspDotNetProject : DotNetProjectBase
    {
        #region Properties
        public override string Template { get; set; } = "ProjectTemplates/AspDotNetHost";
        public string ExportedApiPrefix { get; set; } = "";
        public string ExportedOpenApiSpec { get; set; } = "";
        public string ExportedImageUri { get; set; } = "";
        public string ExportedDockerfilePath { get; set; } = "";
        public override string ProjectFilePath { get; set; } = "";
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
                var prefix = directive.ApiPrefix ?? directive.Key;

                // Get controller Dependencies
                var controllerArtifacts = solution.Directives.GetArtifactsByType<DotNetControllerProject>(directive.Modules).ToList<ArtifactBase>();

                // Get Dependencies
                ProjectReferences.AddRange(GetExportedProjectReferences(controllerArtifacts));
                GlobalUsings.AddRange(GetExportedGlobalUsings(controllerArtifacts));
                ServiceRegistrations.AddRange(GetExportedServiceRegistrations(controllerArtifacts));

                // Package references are now defined directly in the AppRunner.csproj template

                // Copy the template project to the target project. Removes *.g.* files.
                var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, Template);
                var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(OutputFolder, projectName));
                var csprojFileName = GetCsprojFile(sourceProjectDir);
                var filesToExclude = new List<string> { csprojFileName, "User.props", "SRCREADME.md", "ConfigureSvc.g.cs" };
                CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

                // Create/Update the project file
                File.Copy(
                    Path.Combine(sourceProjectDir, csprojFileName),
                    Path.Combine(targetProjectDir, projectName + ".csproj"),
                    overwrite: true);

                GenerateCommonProjectFiles(sourceProjectDir, targetProjectDir);

                var controllerProjects = GetControllerProjects(directive, solution.Directives);

                // Generate the openapi.yaml file with {prefix} replaced with the api prefix
                var openApiSpecs = new List<string>();
                foreach (var controllerProject in controllerProjects)
                    if (!string.IsNullOrEmpty(controllerProject.ExportedOpenApiSpec))
                        openApiSpecs.Add(controllerProject.ExportedOpenApiSpec);
                var openApiSpec = await MergeApiFilesAsync(solution.SolutionRootFolderPath, openApiSpecs);
                openApiSpec = openApiSpec.Replace("{prefix}", prefix);
                File.WriteAllText(Path.Combine(targetProjectDir, "openapi.g.yaml"), openApiSpec);

                GenerateConfigureSvcsFile(projectName, nameSpace, Path.Combine(targetProjectDir, "ConfigureSvcs.g.cs"));

                // Generate Dockerfile for App Runner
                GenerateDockerfile(targetProjectDir, projectName);

                // Exports
                ProjectFilePath = Path.Combine(OutputFolder, projectName, projectName + ".csproj");
                ExportedGlobalUsings = GlobalUsings;
                foreach (var controllerProject in controllerProjects)
                {
                    ExportedOpenApiSpecs.AddRange(controllerProject.ExportedOpenApiSpecs);
                    ExportedPathOps.AddRange(controllerProject.ExportedPathOps);
                }
                ExportedGlobalUsings.Add(nameSpace);
                ExportedGlobalUsings = ExportedGlobalUsings.Distinct().ToList();
                ExportedOpenApiSpecs = ExportedOpenApiSpecs.Distinct().ToList();
                foreach (var controllerArtifact in controllerArtifacts)
                    ExportedPathOps.AddRange(((DotNetControllerProject)controllerArtifact).ExportedPathOps);

                ExportedApiPrefix = prefix;
                ExportedName = projectName;
                ExportedOpenApiSpec = Path.Combine(targetProjectDir, "openapi.g.yaml");
                ExportedDockerfilePath = Path.Combine(targetProjectDir, "Dockerfile");

                // Set image URI for ECR (will be used with !Sub in CloudFormation)
                // Use parameters instead of AWS::StackName to ensure ECR naming rules (no consecutive hyphens)
                ExportedImageUri = $"${{AWS::AccountId}}.dkr.ecr.${{AWS::Region}}.amazonaws.com/${{SystemKeyParameter}}-${{SystemSuffixParameter}}-${{EnvironmentParameter}}-{projectName.ToLower()}:latest";

            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} for {projectName}, {ex.Message}");
            }
        }

        private List<DotNetControllerProject> GetControllerProjects(Container directive, Directives directives)
        {
            var projects = new List<DotNetControllerProject>();
            foreach (var moduleName in directive.Modules)
            {
                var module = (Module)directives[moduleName];
                foreach (var artifact in module.Artifacts.Values.Where(x => x is DotNetControllerProject))
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

            // Authentication is already configured in Program.g.cs - don't add it here

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

        private void GenerateDockerfile(string targetProjectDir, string projectName)
        {
            // Build context is expected to be the Service directory (parent of Containers folder)
            // So paths must be relative to Service directory
            var containerRelativePath = $"Containers/{projectName}";

            var dockerfileContent = $@"FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared build configuration files
COPY [""Directory.Packages.props"", "".""]
COPY [""Directory.Build.props"", "".""]
COPY [""CommonPackageHandling.targets"", "".""]
COPY [""ServiceVersion.props"", "".""]
COPY [""{containerRelativePath}/Nuget.Config.Docker"", ""./nuget.config""]

# Copy NuGet packages prepared by Deploy-DockerAws
COPY [""DockerPackages/"", ""DockerPackages/""]

# Copy project file and restore
COPY [""{containerRelativePath}/{projectName}.csproj"", ""{projectName}/""]
WORKDIR /src/{projectName}
RUN dotnet restore ""{projectName}.csproj""

# Copy all container source files
WORKDIR /src
COPY [""{containerRelativePath}/"", ""{projectName}/""]

# Copy all referenced projects (Modules, Schemas, etc.)
COPY [""Modules/"", ""Modules/""]
COPY [""Schemas/"", ""Schemas/""]

# Build
WORKDIR /src/{projectName}
RUN dotnet build ""{projectName}.csproj"" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish ""{projectName}.csproj"" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [""dotnet"", ""{projectName}.dll""]";

            File.WriteAllText(Path.Combine(targetProjectDir, "Dockerfile"), dockerfileContent);
        }
    }
}
