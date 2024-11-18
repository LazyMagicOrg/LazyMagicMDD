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

    public class DotNetWSApiLambdaProject : DotNetProjectBase
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
                projectName =  directive.Key + NameSuffix ?? "";
                var nameSpace = projectName;
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
                ExportedName = projectName;
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
