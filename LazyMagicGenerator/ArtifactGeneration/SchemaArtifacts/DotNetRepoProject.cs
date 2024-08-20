using NSwag.CodeGeneration.CSharp;
using System.Threading.Tasks;
using System;   
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.OpenApiUtils;

using NSwag;
using Microsoft.CodeAnalysis;
using NSwag.CodeGeneration;

namespace LazyMagic
{

    public class DotNetRepoProject : DotNetProjectBase  
    {

        #region Properties
        public string SchemaProject { get; set; } ="SchemaProject";
        public override string ProjectFilePath => ExportedProjectPath;
        public List<string> ExportedEntities { get; set; } = new List<string>();
        #endregion

        /// <summary>
        /// This process generates a repo project from an OpenApi document.
        /// In general, any files from the propsfilecontent project will be copied to the 
        /// target project, overwriting files in the target project. Then the process
        /// will generate repo classes for each schema defined in the OpenApi document.
        /// 
        /// A service registration class is also written that will register each repo 
        /// for DI.
        /// 
        /// The Repo.csproj file has special handling:
        /// - Renamed to match the project name. ex: EmployeeRepo.csproj
        /// - Overwrites the existing .csproj file if it exists.
        /// The csroj file imports `<Import Project="Repo.g.props" />` which contains 
        /// generated csproj properties. 
        /// 
        /// The csproj file also imports `<Import Project="User.props" />` which contains
        /// user defined csproj properties. The project template may contain a User.props file, 
        /// but it will not be copied to the target project. The user should make changes to the
        /// User.props file in the target project. An empty User.props file is created in the 
        /// target project if one does not exist.
        /// 
        /// In addition, the process generates repository classes from the OpenApi document.
        /// Each of these generated classes have a *.g.cs extension and are partial classes. The user 
        /// is expcted to create a non-generated class with the same name and add additional code to the
        /// class where needed.
        /// 
        /// Limitations: When the OpenApi document is updated such that a schema object is renamed or 
        /// removed, the previously generated classes will be removed. However, any user created 
        /// classes will not be removed. This prevents the loss of user code when the schema changes. 
        /// Such "user created" classes are easily identified as they will not have a corresponding 
        /// generated class.
        /// 
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="directiveArg"></param>
        /// <returns></returns>
        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            try
            {
                Schema directive = (Schema)directiveArg;

                // Read OpenApi specifications
                var openApiSpecs = directive.OpenApiSpecs ?? new List<string>();
                var yamlSpec = await MergeApiFilesAsync(solution.SolutionRootFolderPath, openApiSpecs);
                var schemaItems = GetSchemaNames(yamlSpec);

                //OpenApiDocument openApiDocument = await LoadOpenApiFilesAsync(solution.SolutionRootFolderPath, directive.OpenApiSpecs);
                OpenApiDocument openApiDocument = solution.AggregateSchemas;

                // Set project name and namespace
                var projectName = ProjectName ?? directive.Key;
                projectName += NameSuffix ?? ""; // Usually "Repo"
                var nameSpace = Namespace ?? projectName;
                await InfoAsync($"Generating {directive.Key} {projectName}");

                // Get Schema Project - each DotNetRepo project is paired with a DotNetSchema project.
                var dotnetSchemaProject = directive.Artifacts.Where(x => x.Value.Type.Equals("DotNetSchema")).FirstOrDefault().Value;

                // Get Dependencies
                // A DotNetRepoProject depends on a DotNetSchemaProject. Get the schema project exports.
                var dependantSchemaArtifacts = solution.Directives.GetArtifactsByType(directive.Schemas, "DotNetSchema");
                dependantSchemaArtifacts.Add(dotnetSchemaProject);
                foreach (var dotNetSchemaArtifact in dependantSchemaArtifacts)
                {
                    var dotNetSchemaProject = dotNetSchemaArtifact as DotNetSchemaProject;
                    ProjectReferences.Add(dotNetSchemaProject.ExportedProjectPath);
                    GlobalUsings.AddRange(dotNetSchemaProject.ExportedGlobalUsings);
                    ExportedEntities.AddRange(dotNetSchemaProject.ExportedEntities);
                }
                var dependantRepoArtifacts = solution.Directives.GetArtifactsByType(directive.Schemas, "DotNetRepo");
                foreach (var dotNetRepoArtifact in dependantRepoArtifacts)
                {
                    var dotNetRepoProject = dotNetRepoArtifact as DotNetRepoProject;
                    ProjectReferences.Add(dotNetRepoProject.ExportedProjectPath);
                    GlobalUsings.AddRange(dotNetRepoProject.ExportedGlobalUsings);
                    ServiceRegistrations.AddRange(dotNetRepoProject.ExportedServiceRegistrations);
                }

                // Copy the _template project to the target project. Removes *.g.* files.
                var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, Template);
                var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(OutputFolder, projectName));
                var filesToExclude = new List<string> { "Repo.csproj", "User.props", "SRCREADME.md", "GlobalUsing.g.cs" };
                CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

                // Create/Update the Repo.csproj file.
                File.Copy(
                    Path.Combine(sourceProjectDir, "Repo.csproj"),
                    Path.Combine(targetProjectDir, projectName + ".csproj"),
                    overwrite: true);

                GenerateCommonProjectFiles(sourceProjectDir, targetProjectDir);

                Directory.CreateDirectory(Path.Combine(targetProjectDir, "Repos")); 

                // Generate classes using NSwag 
                var nswagSettings = new CSharpClientGeneratorSettings
                {
                    ClassName = projectName,
                    UseBaseUrl = false,
                    HttpClientType = "ILzHttpClient",
                    GenerateClientInterfaces = true,
                    GenerateDtoTypes = true,
                    CSharpGeneratorSettings =
                    {
                        Namespace = projectName,
                        GenerateDataAnnotations = false,
                        ClassStyle = NJsonSchema.CodeGeneration.CSharp.CSharpClassStyle.Inpc,
                        //HandleReferences = true
                    },
                    OperationNameGenerator = new LzOperationNameGenerator()
                };

                var nswagGenerator = new CSharpClientGenerator(openApiDocument, nswagSettings);
                var code = nswagGenerator.GenerateFile();
                var root = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();

                // Remove the API class so we only have schema classes
                root = RemoveClass(root, projectName); // The API class is named the same as the projectName.

                // Generate repo class for each dto
                //var classDeclarations = root
                //    .DescendantNodes().OfType<NamespaceDeclarationSyntax>()
                //    .First()
                //        ?.DescendantNodes().OfType<ClassDeclarationSyntax>()
                //        .Where(x => schemaItems.Contains(x.Identifier.ValueText))
                //        .GroupBy(x => x.Identifier.ValueText)
                //        .ToList(); // ex: OrderController
                var classDeclarations = root
                    .DescendantNodes().OfType<NamespaceDeclarationSyntax>()
                    .First()
                        ?.DescendantNodes().OfType<ClassDeclarationSyntax>()
                        .Where(x => schemaItems.Contains(x.Identifier.ValueText) &&
                                    x.Members
                                        .OfType<PropertyDeclarationSyntax>()
                                        .Any(p => p.Identifier.ValueText == "Id" &&
                                                  p.Modifiers.Any(SyntaxKind.PublicKeyword)))
                        .GroupBy(x => x.Identifier.ValueText)
                        .ToList();

                var classes = new List<string>();
                var interfaces = new List<string>();
                foreach (var classDeclaration in classDeclarations)
                {
                    var className = classDeclaration.Key + NameSuffix;
                    classes.Add(className);
                    interfaces.Add("I" + className);
                    var filePath = Path.Combine(targetProjectDir, "Repos", className + ".g.cs");
                    GenerateImplClassGenFile(classDeclaration, projectName, filePath);
                }

                // Generate Service Registrations class
                GenerateServiceRegistrations(classes, ServiceRegistrations, nameSpace, projectName, Path.Combine(targetProjectDir, "ServiceRepoExtensions.g.cs"));

                // Exports
                ExportedName = projectName; 
                ExportedProjectPath = Path.Combine(OutputFolder, projectName, projectName) + ".csproj";
                GlobalUsings.Add(nameSpace);
                ExportedGlobalUsings = GlobalUsings;
                ExportedInterfaces = interfaces;
                ExportedServiceRegistrations = new List<string> { $"Add{projectName}" };
                ExportedEntities = ExportedEntities.Distinct().ToList();
                ExportedPackages = PackageReferences;
            } catch (Exception ex) 
            { 
                throw new Exception($"Error generating DotNetRepoProject: {ex.Message}", ex);   
            }
        }
        private static void GenerateServiceRegistrations(List<string> repos, List<string> services, string nameSpace, string projectName, string filePath)
        {
            var repoRegistrations = $@"
        services.AddAWSService<Amazon.DynamoDBv2.IAmazonDynamoDB>();
"; 

            foreach (var repo in repos)
                repoRegistrations += $"\t\tservices.TryAddSingleton<I{repo}, {repo}>();\r\n";

            var serviceRegistrations = "";
            foreach(var service in services)
                serviceRegistrations += $"\t\tservices.{service}();\r\n";

            var classbody = $@"
//----------------------
// <auto-generated>
//     Generated by LazyMagic. Do not modify, your changes will be overwritten.
//     If you need to register additional services, do it in a separate class.
//     Also, if you need to register a service with a different lifetime, do it in a seprate class.
//     Note that we are using Try* so if you register a service with the same interface first, that 
//     registration will be used.   
//     We very intentionally use Singletons for our Repos. This is because we want to be able to 
//     maintain state for caching etc. when necessary. 
// </auto-generated>
//----------------------
namespace {nameSpace};
public static class {projectName}Extensions
{{
    public static IServiceCollection Add{projectName}(this IServiceCollection services)
    {{
{repoRegistrations}
{serviceRegistrations}

        return services;
    }}
}}
";
            File.WriteAllText(filePath, classbody);
        }
        private static void GenerateImplClassGenFile(IGrouping<string,ClassDeclarationSyntax> classDeclaration, string nameSpace, string filePath)
        {
            var entityName = classDeclaration.Key;

            var classbody = $@"
//----------------------
// <auto-generated>
//  Generated by LazyMagic. Create overrides and partial method implementations in a separate file.
//  See the README.g.md file for best practices for extending these generated classes.
// </auto-generated>
//----------------------
namespace {nameSpace};
public partial class {entityName}Envelope : DataEnvelope<{entityName}>{{}}
public partial interface I{entityName}Repo : IDYDBRepository<{entityName}Envelope, {entityName}> {{}}
public partial class {entityName}Repo : DYDBRepository<{entityName}Envelope, {entityName}>, I{entityName}Repo
{{
    public {entityName}Repo(IAmazonDynamoDB client) : base(client) {{}}
}}
";
            File.WriteAllText(filePath, classbody);
        }
    }
}
