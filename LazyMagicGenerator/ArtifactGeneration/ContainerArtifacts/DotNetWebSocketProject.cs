﻿using System.Threading.Tasks;
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

    public class DotNetWebSocketProject : DotNetProjectBase
    {
        #region Properties
        #endregion
        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var projectName = "";
            try
            {
                Container directive = (Container)directiveArg;

                // Set the project name and namespace
                projectName = ProjectName ?? directive.Key;
                projectName += NameSuffix ?? "";
                var nameSpace = Namespace ?? projectName;
                await InfoAsync($"Generating {directive.Key} {projectName}");

                // Set the containers prefix - this is used to create unique paths. ex: {prefix}/yada => /api/yada
                // Note that we do not use the artifcat ProjectName because the prefix needs to be consistent
                // at the Service level. See usage in DotNetLocalWebApiProject for example.
                var prefix = directive.ApiPrefix ?? directive.Key;

                // Get controller Dependencies

                // Get Dependencies

                // Copy the template project to the target project. Removes *.g.* files.
                var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, Template);
                var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(OutputFolder, projectName));
                var filesToExclude = new List<string> { "Lambda.csproj", "User.props", "SRCREADME.md", "ConfigureSvc.g.cs" };
                CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

                // Create/Update the Repo.csproj file.
                File.Copy(
                    Path.Combine(sourceProjectDir, "NotificationsWebSocket.csproj"),
                    Path.Combine(targetProjectDir, projectName + ".csproj"),
                    overwrite: true);

                GenerateCommonProjectFiles(sourceProjectDir, targetProjectDir);

                GenerateConfigureSvcsFile(projectName, nameSpace, Path.Combine(targetProjectDir, "ConfigureSvcs.g.cs"));

                // Exports
                ExportedName = projectName;
                ProjectFilePath = Path.Combine(OutputFolder, projectName, projectName + ".csproj");
                ExportedGlobalUsings = GlobalUsings;
                ExportedGlobalUsings.Add(nameSpace);
                ExportedGlobalUsings = ExportedGlobalUsings.Distinct().ToList(); // note: used by DotNetClientSDKProject

            } catch (Exception ex)
            {
                throw new Exception($"Error generating DotNetWebSocketProject: {projectName}, {ex.Message}");
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
;