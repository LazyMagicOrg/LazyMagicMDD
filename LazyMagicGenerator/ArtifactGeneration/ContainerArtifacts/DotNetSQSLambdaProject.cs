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

    public class DotNetSQSLambdaProject : DotNetProjectBase
    {
        #region Properties
        public override string Template { get; set; } = "ProjectTemplates/SQSLambda";
        #endregion
        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var projectName = directiveArg.Key + NameSuffix ?? "";
            try
            {
                Container directive = (Container)directiveArg;
               
                var nameSpace = projectName;
                await InfoAsync($"Generating {projectName}");

                // Get controller Dependencies

                // Get Dependencies

                // Copy the template project to the target project. Removes *.g.* files.
                var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, Template);
                var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(OutputFolder, projectName));
                var csprojFileName = GetCsprojFile(sourceProjectDir);
                var filesToExclude = new List<string> { csprojFileName, "User.props", "SRCREADME.md" };
                CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

                // Create/Update the Repo.csproj file.
                File.Copy(
                    Path.Combine(sourceProjectDir, csprojFileName),
                    Path.Combine(targetProjectDir, projectName + ".csproj"),
                    overwrite: true);

                GenerateCommonProjectFiles(sourceProjectDir, targetProjectDir);

                // Exports
                ProjectFilePath = Path.Combine(OutputFolder, projectName, projectName + ".csproj");
                ExportedGlobalUsings = GlobalUsings;
                ExportedGlobalUsings.Add(nameSpace);
                ExportedGlobalUsings = ExportedGlobalUsings.Distinct().ToList(); // note: used by DotNetClientSDKProject

            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name}: {projectName}, {ex.Message}");
            }
        }
    }
}
