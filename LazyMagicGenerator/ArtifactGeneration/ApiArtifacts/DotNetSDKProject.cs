using NSwag.CodeGeneration.CSharp;
using System;   
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.OpenApiUtils;
using static LazyMagic.ArtifactUtils;
using NSwag;
using System.ComponentModel;
using NSwag.Collections;

namespace LazyMagic
{
    public class DotNetSDKProject : DotNetProjectBase
    {
        #region Properties
        public override string ProjectFilePath => ExportedProjectPath;
        #endregion

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            try
            {
                await Task.Delay(0);
                Api directive = (Api)directiveArg;
                var apiPrefix = directive.ApiPrefix ?? directive.Key;   
                // Set the project name and namespace
                var projectName = ProjectName ?? directive.Key;
                projectName += NameSuffix ?? "";    
                var nameSpace = Namespace ?? projectName;
                Info($"Generating {directive.Key} {projectName}");

                // The SDK project dependences are interesting:
                // The Api directive contains a list of containers. Each container contains a list of modules.
                // Each moudle has a list of ExportedOpenApiSpecs.
                // Each Module contains a list of schemas. Each schema has a list of ExportedOpenApiSpecs.
                // The OpenApiSpecs for this project are the union of the OpenApiSpecs for the containers and schemas.
                // This is also true for the other types of dependences.
                // Note that there is no direct reference to the Containers in the Api directive. This is because the 
                // Containers are purely Host side and are only used in this project to determine the dependences.

                // Get Aggregate Modules and Schemas for the Api
                var controllers = GetModulesForApi(directive, solution.Directives);
                var schemas = GetSchemasForApi(directive, solution.Directives);

                // Get Artificat Depenencies
                // var controllerArtifacts = solution.Directives.GetArtifactsByType(controllers, "DotNetController");
                var schemaArtifacts = solution.Directives.GetArtifactsByType(schemas, "DotNetSchema");

                // Get Dependencies
                ProjectReferences.AddRange(GetExportedProjectReferences(schemaArtifacts));

                // PackageReferences.AddRange(GetExportedPackageReferences(controllerArtifacts));
                PackageReferences = PackageReferences.Distinct().ToList();

                GlobalUsings.AddRange(GetExportedGlobalUsings(schemaArtifacts));
                // GlobalUsings.AddRange(GetExportedGlobalUsings(controllers, "DotNetController", solution.Directives));
                GlobalUsings = GlobalUsings.Distinct().ToList();

                ServiceRegistrations.AddRange(GetExportedServiceRegistrations(schemaArtifacts));
                //ServiceRegistrations.AddRange(GetExportedServiceRegistrations(controllerArtifacts));
                ServiceRegistrations = ServiceRegistrations.Distinct().ToList();

                // Copy the template project to the target project. Removes *.g.* files.
                var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, Template);
                var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(OutputFolder, projectName));
                var filesToExclude = new List<string> { "ClientSDK.csproj", "User.props", "SRCREADME.md" };
                CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

                // Create/Update the Repo.csproj file.
                File.Copy(
                    Path.Combine(sourceProjectDir, "ClientSDK.csproj"),
                    Path.Combine(targetProjectDir, projectName + ".csproj"),
                    overwrite: true);

                GenerateCommonProjectFiles(sourceProjectDir, targetProjectDir);

                var lambdaProjects = GetLambdaProjects(directive, solution.Directives); 
                var openApiSpecs = new List<string>();  
                foreach(var lambdaProject in lambdaProjects)
                    if(!string.IsNullOrEmpty(lambdaProject.ExportedOpenApiSpec))
                        openApiSpecs.Add(lambdaProject.ExportedOpenApiSpec);
                var openApiSpec = await MergeApiFilesAsync(solution.SolutionRootFolderPath, openApiSpecs);
                File.WriteAllText(Path.Combine(targetProjectDir, "openapi.g.yaml"), openApiSpec);

                OpenApiDocument openApiDocument = await ParseOpenApiYamlContent(openApiSpec);
                // Add the apiPrefix to each path 
                var paths = openApiDocument.Paths;
                foreach (var path in openApiDocument.Paths.Keys.ToList())
                {
                    var value = openApiDocument.Paths[path];
                    paths.Remove(path);
                    paths.Add($"/{apiPrefix}{path}", value);
                }

                // Generate classes using NSwag 
                var nswagSettings = new CSharpClientGeneratorSettings
                {
                    ClassName = projectName,
                    UseBaseUrl = false,
                    HttpClientType = "ILzHttpClient",
                    GenerateClientInterfaces = true,
                    GenerateDtoTypes = false,
                    CSharpGeneratorSettings =
                    {
                        Namespace = nameSpace,
                        GenerateDataAnnotations = false,
                        ClassStyle = NJsonSchema.CodeGeneration.CSharp.CSharpClassStyle.Inpc,
                        //HandleReferences = true
                    },
                    OperationNameGenerator = new LzOperationNameGenerator()
                };
                var nswagGenerator = new CSharpClientGenerator(openApiDocument, nswagSettings);
                var code = nswagGenerator.GenerateFile();

                GenerateClientSDKClass(code, projectName, Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, projectName + ".g.cs"));

                // Exports
                ExportedName = projectName;
                ExportedProjectPath = Path.Combine(OutputFolder, projectName, projectName + ".csproj");
            } catch (Exception ex)
            {
                throw new Exception("Error generating DotNetSDKProject: " + ex.Message);    
            }
        }
        private void GenerateClientSDKClass(string code, string projectName, string filePath)
        {
            // Generate the client SDK
            var root = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();
            root = RemoveGeneratedSchemaClasses(root, new List<string> { "ApiException" }); // strip out schema
            //root = RemoveLambdaEndpointsMethods(solutionModel, root); // remove 
            File.WriteAllText(filePath, ReplaceLineEndings(code));
        }

        private List<DotNetLambdaProject> GetLambdaProjects(Api directive, Directives directives)
        {
            var projects = new List<DotNetLambdaProject>(); 
            foreach(var containerName in directive.Containers)
            {
                var container = (Container)directives[containerName];
                foreach(var artifact in container.Artifacts.Values.Where(x => x.Type.Equals("DotNetLambda")))
                {
                    projects.Add((DotNetLambdaProject)artifact);
                }
            }
            return projects;    

        }

    }
}
