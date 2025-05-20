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

namespace LazyMagic
{
    public class DotNetSchemaProject : DotNetProjectBase
    {
        #region Properties
        public override string ProjectFilePath
        {
            get => ExportedProjectPath;
            set => ExportedProjectPath = value;
        }
        public override string NameSuffix { get; set; } = "";
        public override string Template { get; set; } = "ProjectTemplates/Schema";
        public override string OutputFolder { get; set; } = "Schemas";

        public List<string> ExportedEntities { get; set; } = new List<string>();
       
        #endregion

        /// <summary>
        /// This process generates a schema project from an OpenApi document.
        /// In general, any files from the template project will be copied to the 
        /// target project, overwriting files in the target project. Then the process
        /// will generate DTOs, DTOValidators, Models, and ModelValidators classes
        /// for each schema defined in the OpenApi document.
        /// 
        /// The Schema.csproj file has special handling:
        /// - Renamed to match the project name. ex: EmployeeSchema.csproj
        /// - Overwrites the existing .csproj file if it exists.
        /// The csroj file imports `<Import Project="Schema.g.props" />` which contains 
        /// generated csproj properties. 
        /// 
        /// The csproj file also imports `<Import Project="User.props" />` which contains
        /// user defined csproj properties. The project template may contain a User.props file, 
        /// but it will not be copied to the target project. The user should make changes to the
        /// User.props file in the target project. An empty User.props file is created in the 
        /// target project if one does not exist.
        /// 
        /// In addition, the process generates DTOs, Models, and Validators classeDeclarations from the OpenApi document.
        /// Each of these generated classeDeclarations have a *.g.cs extension and are partial classeDeclarations. The user 
        /// is expcted to create a non-generated class with the same name and add additional code to the
        /// class where needed.
        /// 
        /// Limitations: When the OpenApi document is updated such that a schema object is renamed or 
        /// removed, the previously generated classeDeclarations will be removed. However, any user created 
        /// classeDeclarations will not be removed. This prevents the loss of user code when the schema changes. 
        /// Such "user created" classeDeclarations are easily identified as they will not have a corresponding 
        /// generated class.
        /// 
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="directiveArg"></param>
        /// <returns></returns>

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var projectName = directiveArg.Key + NameSuffix ?? "";
            try
            {
                Schema directive = (Schema)directiveArg;

                // Set the project name and namespace

                var nameSpace = projectName;
                await InfoAsync($"Generating {directive.Key} {projectName}");

                // Read OpenApi specifications
                var openApiSpecs = directive.OpenApiSpecs ?? new List<string>();
                var yamlSpec = await MergeApiFilesAsync(solution.SolutionRootFolderPath, openApiSpecs); 
                var schemaEntities = GetSchemaNames(yamlSpec);
                var openApiDocument = solution.AggregateSchemas;

                // Get Dependencies 
                var dependantArtifacts = solution.Directives.GetArtifactsByType<DotNetSchemaProject>(directive.Schemas);
                foreach(var dotNetSchemaArtifact in dependantArtifacts)
                {
                    var dotNetSchemaProject = dotNetSchemaArtifact as DotNetSchemaProject;
                    ProjectReferences.Add(dotNetSchemaProject.ExportedProjectPath);
                    GlobalUsings.AddRange(dotNetSchemaProject.ExportedGlobalUsings);
                }

                // Copy the template project to the target project. Removes *.g.* files.           
                var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, Template);
                var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(OutputFolder, projectName));
                var csprojFileName = GetCsprojFile(sourceProjectDir);
                var filesToExclude = new List<string> { csprojFileName, "User.props", "SRCREADME.md"};
                CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

                // Create/Update the Schema.csproj file.
                File.Copy(
                    Path.Combine(sourceProjectDir, csprojFileName),
                    Path.Combine(targetProjectDir, projectName + ".csproj"),
                    overwrite: true);

                GenerateCommonProjectFiles(sourceProjectDir, targetProjectDir);

                // Generate classeDeclarations using NSwag 
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
                        ClassStyle = NJsonSchema.CodeGeneration.CSharp.CSharpClassStyle.Inpc
                        //HandleReferences = true
                    },
                    OperationNameGenerator = new LzOperationNameGenerator()
                };

                var nswagGenerator = new CSharpClientGenerator(openApiDocument, nswagSettings);
                var code = nswagGenerator.GenerateFile();
                var root = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();

                // Remove the API class so we only have schema classes
                root = RemoveClass(root, projectName); // The API class is named the same as the projectName.

                // Find all schema classes included in currentOpenApiDocument
                var classeDeclarations = root
                    .DescendantNodes().OfType<NamespaceDeclarationSyntax>()
                    .First()
                        ?.DescendantNodes().OfType<ClassDeclarationSyntax>()
                        .Where(x => schemaEntities.Contains(x.Identifier.ValueText))
                        .GroupBy(x => x.Identifier.ValueText)
                        .ToList(); // ex: Order

                // Generate DTO, DTOValidator, Model, and ModelValidator 
                foreach (var classDeclaration in classeDeclarations)
                {
                    var className = classDeclaration.Key;
                    Directory.CreateDirectory(Path.Combine(targetProjectDir, "DTOs"));
                    GenerateDTO(classDeclaration, nameSpace, Path.Combine(targetProjectDir, "DTOs", $"{className}.g.cs"));
                    GenerateDTOValidator(classDeclaration, nameSpace, Path.Combine(targetProjectDir, "DTOs", $"{className}Validator.g.cs"));
                    Directory.CreateDirectory(Path.Combine(targetProjectDir, "Models"));
                    GenerateModel(classDeclaration, nameSpace, Path.Combine(targetProjectDir, "Models", $"{className}Model.g.cs"));
                    GenerateModelValidators(classDeclaration, nameSpace, Path.Combine(targetProjectDir, "Models", $"{className}ModelValidator.g.cs"));
                }

                // Find all enum classes
                var enumDecolarations = root
                .DescendantNodes().OfType<NamespaceDeclarationSyntax>()
                .First()
                    ?.DescendantNodes().OfType<EnumDeclarationSyntax>()
                    .Where(x => schemaEntities.Contains(x.Identifier.ValueText))
                    .GroupBy(x => x.Identifier.Value)
                    .ToList(); // ex: OrderController

                // Generate Enum DTOs
                foreach (var enumDeclaration in enumDecolarations)
                {
                    var enumName = enumDeclaration.Key;
                    GenerateEnumDTO(enumDeclaration, nameSpace, Path.Combine(targetProjectDir, "DTOs", $"{enumName}.g.cs"));
                }

                // Exports
                ExportedProjectPath = Path.Combine(OutputFolder, projectName, projectName) + ".csproj";
                ExportedGlobalUsings = new List<string> { nameSpace };
                ExportedOpenApiSpecs = openApiSpecs;
                ExportedEntities = schemaEntities;
            } 
            catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} for {projectName} : {ex.Message}");
            }
        }
        public static bool HasPublicIdProperty(ClassDeclarationSyntax classDeclaration)
        {
            return classDeclaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .Any(property =>
                    property.Identifier.ValueText == "Id" &&
                    property.Modifiers.Any(SyntaxKind.PublicKeyword));
        }
        private void GenerateDTO(IGrouping<string,ClassDeclarationSyntax> classDeclaration, string nameSpace, string filePath)
        {

            var className = classDeclaration.Key;
            var classCode = string.Empty;
            //if (solutionModel.DoNotGenerateSchema.Contains(className))
            //    continue;
            var classFileContent = _template.Replace("__NameSpace__", $"{nameSpace}");
            foreach (var classBody in classDeclaration)
            {
                classCode += HasPublicIdProperty(classBody)
                    ? AddInterfaceToClass(classBody, "IItem").ToString()
                    : classBody.ToString();
            }
            classFileContent = classFileContent.Replace("__Body__", classCode);
            File.WriteAllText(filePath, ReplaceLineEndings(classFileContent));
        }
        private void GenerateDTOValidator(IGrouping<string, ClassDeclarationSyntax> classDeclaration, string nameSpace, string filePath)
        {
                var className = classDeclaration.Key;
                var classCode = string.Empty;
                //if (solutionModel.DoNotGenerateSchema.Contains(className) || File.Exists(filePath))
                //    continue;
                var classFileContent = _lzTemplate.Replace("__NameSpace__", $"{nameSpace}");

                classCode = classCode + @"

public partial class " + className + @"Validator : FluentValidation.AbstractValidator<" + className + @">
{
}
";
                classFileContent = classFileContent.Replace("__Body__", classCode);
                File.WriteAllText(Path.Combine(filePath), ReplaceLineEndings(classFileContent));
        }
        private void GenerateEnumDTO(IGrouping<object, EnumDeclarationSyntax> enumDeclaration, string nameSpace, string filePath)
        {
            var className = enumDeclaration.Key;
            var classCode = string.Empty;
            //if (solutionModel.DoNotGenerateSchema.Contains(className))
            //    continue;
            var classFileContent = _template.Replace("__NameSpace__", $"{nameSpace}");
            foreach (var classBody in enumDeclaration)
                classCode += classBody.ToFullString();
            classFileContent = classFileContent.Replace("__Body__", classCode);
            File.WriteAllText(filePath, ReplaceLineEndings(classFileContent));
        }
        private void GenerateModel(IGrouping<string, ClassDeclarationSyntax> classDeclaration, string nameSpace, string filePath)
        {

                var className = classDeclaration.Key;
                var classCode = string.Empty;
                //if (solutionModel.DoNotGenerateSchema.Contains(className) || File.Exists(filePath))
                //    continue;
                var classFileContent = _lzTemplate.Replace("__NameSpace__", $"{nameSpace}");

                classCode = classCode + @"

public partial class " + className + @"Model : " + className + @",IRegisterObservables
{
}
";
                classFileContent = classFileContent.Replace("__Body__", classCode);
                File.WriteAllText(filePath, ReplaceLineEndings(classFileContent));

        }
        private void GenerateModelValidators(IGrouping<string, ClassDeclarationSyntax> classDeclaration, string nameSpace, string filePath)
        {


                var className = classDeclaration.Key;
                var classCode = string.Empty;
                var fileExists = File.Exists(filePath);
                var classFileContent = _lzTemplate.Replace("__NameSpace__", $"{nameSpace}");

                classCode = classCode + @"
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public partial class " + className + @"ModelValidator : FluentValidation.AbstractValidator<" + className + @"Model>
{
    public " + className + @"ModelValidator()
    {
        Include(new " + className + @"Validator());
        CustomValidation();
    }
    partial void CustomValidation();
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
";
                classFileContent = classFileContent.Replace("__Body__", classCode);
                File.WriteAllText(filePath, ReplaceLineEndings(classFileContent));

        }

        private string _template = @"
//----------------------
// <auto-generated>
//     Generated using the NSwag library. 
//     Refactored into the schema project by LazyMagic.
//     Do not modify, your changes will be overwritten.
//     This is a partial class so you can extend the class in a separate file.
//     Only files with *.g.* extensions will be overwritten.
// </auto-generated>
//----------------------

#pragma warning disable 108 // Disable ""CS0108 '{derivedDto}.ToJson()' hides inherited member '{dtoBase}.ToJson()'.Use the new keyword if hiding was intended.""
#pragma warning disable 114 // Disable ""CS0114 '{derivedDto}.RaisePropertyChanged(String)' hides inherited member 'dtoBase.RaisePropertyChanged(String)'. To make the current member override that implementation, add the override keyword. Otherwise add the new keyword.""
#pragma warning disable 472 // Disable ""CS0472 The result of the expression is always 'false' since a value of type 'Int32' is never equal to 'null' of type 'Int32?'""
#pragma warning disable 1573 // Disable ""CS1573 Parameter '...' has no matching param tag in the XML comment for ...""
#pragma warning disable 1591 // Disable ""CS1591 Missing XML comment for publicly visible type or member ...""
#pragma warning disable 8073 // Disable ""CS8073 The result of the expression is always 'false' since a value of type 'T' is never equal to 'null' of type 'T?'""

namespace __NameSpace__
{
    using System = global::System;
    __Body__
}
#pragma warning restore 1591
#pragma warning restore 1573
#pragma warning restore  472
#pragma warning restore  114
#pragma warning restore  108
";

        private string _lzTemplate = @"
//----------------------
// <auto-generated>
//     Generated by LazyMagic.
//     Do not modify, your changes will be overwritten.
//     This is a partial class so you can extend the class in a separate file.
//     Only files with *.g.* extensions will be overwritten.
// </auto-generated>
//----------------------

namespace __NameSpace__
{
    using System = global::System;
    __Body__
}
";



    }
}
