using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.OpenApiUtils;

namespace LazyMagic
{
    public class DotNetAuthorizationProject : DotNetProjectBase
    {
        public override string Template { get; set; } = "DotNetTemplates/Authorization";


        /// <summary>
        /// This process generates an authorization project.
        /// In general, any files from the template project will be copied to the 
        /// target project, overwriting files in the target project. The project 
        /// will contain a Authorization.g.cs file which defines a 
        /// partial class called Authorization. You will have to create  
        /// a Authorization.cs file containing a partial Authorization class 
        /// that overrides the following methods:
        /// GetUserPermissionsAsync()
        /// LoadPermissionsAsync()
        /// 
        /// The csproj file imports `<Import Project="User.props" />` which contains
        /// user defined csproj properties. The project template may contain a User.props file, 
        /// but it will not be copied to the target project. The user should make changes to the
        /// User.props file in the target project. An empty User.props file is created in the 
        /// target project if one does not exist.
        /// 
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="directiveArg"></param>
        /// <returns></returns>

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            Authorization directive = (Authorization)directiveArg;

            // Set the project name and path
            var projectName = directive.Key + NameSuffix ?? "";
            var nameSpace = projectName;
            try
            {
                ExportedProjectPath = Path.Combine(OutputFolder, projectName, projectName) + ".csproj";
                ExportedGlobalUsings = new List<string> { nameSpace };
                var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, Template);
                var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(OutputFolder, projectName));
                var csprojFileName = GetCsprojFile(sourceProjectDir);
                await InfoAsync($"Generating {directive.Key} {projectName}");

                // Copy the template project to the target project. Removes *.g.* files.
                var filesToExclude = new List<string> { csprojFileName, "User.props", "SRCREADME.md" };
                CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

                // Create/Update the csproj file.
                File.Copy(
                    Path.Combine(sourceProjectDir, csprojFileName),
                    Path.Combine(targetProjectDir, projectName + ".csproj"),
                    overwrite: true);

                // Projects.g.props - contains generated project reference dependencies
                var projectPropsPath = Path.Combine(targetProjectDir, "Projects.g.props");
                var projectPropsPathList = ProjectReferences ?? new List<string>();
                GenerateProjectsPropsFile(projectPropsPathList, projectPropsPath);

                // Packages.g.props - contains generated package dependencies
                var packagePropsPath = Path.Combine(targetProjectDir, "Packages.g.props");
                var packagePropsPathList = PackageReferences ?? new List<string>();
                GeneratePackagesPropsFile(packagePropsPathList, packagePropsPath);

                // GlobalUsing.g.cs file.
                var globalUsingText = File.ReadAllText(Path.Combine(sourceProjectDir, "GlobalUsing.t.cs"));
                var globlaUsingPath = Path.Combine(targetProjectDir, "GlobalUsing.g.cs");
                var usings = GlobalUsings ?? new List<string>();
                GenerateGlobalUsingFile(usings, globalUsingText, globlaUsingPath);

                // User.props file if it does not exist. We create it to remind the user
                // they can use it to extend their project.
                var userPropsPath = Path.Combine(targetProjectDir, "User.props");
                GenerateUserPropsFile("", userPropsPath);

                // LICENSE.TXT file if it does not exist. We create it to remind the user
                // they should add a License file to their project.
                var licensePath = Path.Combine(targetProjectDir, "LICENSE.TXT");
                GenerateLicenseFile("", licensePath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name}: {projectName}, {ex.Message}");
            }
        }
    }
}
