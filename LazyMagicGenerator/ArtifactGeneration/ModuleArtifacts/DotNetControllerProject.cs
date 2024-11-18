using NSwag.CodeGeneration.CSharp;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.OpenApiUtils;
using static LazyMagic.OpenApiExtensions;
using NSwag;
using FluentValidation.Results;
using System.Xml.Schema;

namespace LazyMagic
{
    public class DotNetControllerProject : DotNetProjectBase
    {
        #region Properties
        public override string ProjectFilePath => ExportedProjectPath;
        public string ExportedOpenApiSpec { get; set; } = "";    

        #endregion
        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {

            var projectName = directiveArg.Key + NameSuffix ?? "";

            try
            {
                Module directive = (Module)directiveArg;

                // Set the project name and namespace
                var nameSpace = projectName;
                await InfoAsync($"Generating {directive.Key} {projectName}");

                var openApiSpecs = directive.OpenApiSpecs ?? new List<string>();
                var schemas = directive.Schemas;
                var openApiSpec = directive.OpenApiSpec;
                OpenApiDocument openApiDocument = await ParseOpenApiYamlContent(openApiSpec);

                //  Get artifact dependencies
                var interfaces = new List<string>() { $"I{projectName}Authorization" };
                var dependantRepoArtifacts = solution.Directives.GetArtifactsByTypeName(schemas, "DotNetRepo");
                foreach (var dotNetRepoArtifact in dependantRepoArtifacts)
                {
                    var dotNetRepoProject = dotNetRepoArtifact as DotNetRepoProject;
                    ProjectReferences.Add(dotNetRepoProject.ExportedProjectPath);
                    PackageReferences.AddRange(dotNetRepoProject.ExportedPackages);
                    ServiceRegistrations.AddRange(dotNetRepoProject.ExportedServiceRegistrations);
                    GlobalUsings.AddRange(dotNetRepoProject.ExportedGlobalUsings);
                    interfaces.AddRange(dotNetRepoProject.ExportedInterfaces);
                }
                ProjectReferences = ProjectReferences.Distinct().ToList();  
                PackageReferences = PackageReferences.Distinct().ToList();
                ServiceRegistrations = ServiceRegistrations.Distinct().ToList();
                GlobalUsings = GlobalUsings.Distinct().ToList();
                interfaces = interfaces.Distinct().ToList();

                // Copy the template project to the target project. Removes *.g.* files.
                var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, Template);
                var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(OutputFolder, projectName));
                var csprojFileName = GetCsprojFile(sourceProjectDir);
                var filesToExclude = new List<string> { csprojFileName, "User.props", "SRCREADME.md"};
                CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

                // Create/Update the Repo.csproj file.
                File.Copy(
                    Path.Combine(sourceProjectDir, csprojFileName),
                    Path.Combine(targetProjectDir, projectName + ".csproj"),
                    overwrite: true);

                GenerateCommonProjectFiles(sourceProjectDir, targetProjectDir);

                // Generate classes using NSwag 
                // Note that NSWAG is hardcoded to add 'Controller' to the classname.
                var nswagSettings = new CSharpControllerGeneratorSettings
                {
                    UseActionResultType = true,
                    ClassName = projectName,
                    ControllerTarget = NSwag.CodeGeneration.CSharp.Models.CSharpControllerTarget.AspNetCore,
                    //ControllerBaseClass = "Microsoft.AspNetCore.Mvc.Controller",
                    ControllerBaseClass = $"I{projectName}Controller",
                    CSharpGeneratorSettings =
                {
                    Namespace = nameSpace,
                }
                };
                var nswagGenerator = new CSharpControllerGenerator(openApiDocument, nswagSettings);
                var code = nswagGenerator.GenerateFile();
                var root = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();
                root = RemoveGeneratedSchemaClasses(root); // strip out schema

                GenerateControllerClass(ref root, openApiDocument, interfaces, projectName, Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, projectName) + ".g.cs");

                GenerateAuthorizationClass(ref root, openApiDocument, projectName, nameSpace, Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, $"{projectName}Authorization") + ".g.cs");

                GenerateImplClassGenFile(projectName, interfaces, Dependencies, Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, $"{projectName}Impl") + ".g.cs"); // This is a partial class containing the constructor with the repo arguments. Suffix: 'Impl.g.cs'

                GenerateServiceRegistrationsClass(projectName, nameSpace, ServiceRegistrations, Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, $"{projectName}Registrations") + ".g.cs"); // This class contains the extension methods to register necesssry services

                // Exports
                // Write Modified OpenApi specs to file
                var exportedOpenApiSpec = Path.Combine(OutputFolder, projectName, "openapi.g.yaml");
                File.WriteAllText(Path.Combine(solution.SolutionRootFolderPath, exportedOpenApiSpec), openApiSpec);

                ExportedName = projectName;
                ExportedProjectPath = Path.Combine(OutputFolder, projectName, projectName) + ".csproj";
                ExportedServiceRegistrations = new List<string> { $"Add{projectName}" };
                ExportedGlobalUsings = GlobalUsings.Distinct().ToList();
                ExportedGlobalUsings.Add(nameSpace);
                ExportedOpenApiSpecs = openApiSpecs;
                ExportedOpenApiSpec = exportedOpenApiSpec;
                foreach (var path in openApiDocument.Paths)
                    ExportedPathOps.Add((path.Key, path.Value.Keys.ToList()));

            } 
            catch (Exception ex)
            {
                throw new Exception($"Error Generating {GetType().Name} for {projectName}");
            }
        }
        private static List<string> GetRequiredSchemas(SolutionBase solution, List<string> schemaEntities)
        {
            var schemas = solution.Directives.Where(d => d.Value is Schema).ToDictionary(d => d.Key, d => d.Value as Schema);
            var result = new List<string>();
            var remainingEntities = new HashSet<string>(schemaEntities);
            foreach(var schemaKVP in schemas)
            {
                if (remainingEntities.Count == 0) break;
                foreach(var artifact in schemaKVP.Value.Artifacts)
                {
                    if(remainingEntities.Count == 0) break;
                    if (artifact.Value.Type.Equals("DotNetRepo"))
                    {
                        if(remainingEntities.Count == 0) break;
                        var dotNetRepoProject = artifact.Value as DotNetRepoProject;
                        if (dotNetRepoProject.ExportedEntities.Any(e => remainingEntities.Contains(e)))
                        {
                            result.Add(schemaKVP.Key);
                            remainingEntities.ExceptWith(dotNetRepoProject.ExportedEntities);
                        }
                    }
                }
            }
                    
            return result;
        }

        private static void GenerateAuthorizationClass(ref CompilationUnitSyntax root, OpenApiDocument openApiDocument, string projectName, string nameSpace, string filePath)
        {
            var code = $@"  
//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
//     Create your own cs file for this partial class and implement any endpoint methods 
// </auto-generated>
//----------------------

namespace {nameSpace};

/// <summary>
/// You can override the default behavior of the authorization class by implementing this interface and providing your own implementation.
/// You then register your implementation BEFORE calling this projects service registration method. Note that we use TryAddSingleton to avoid
/// registering multiple implementations of the same interface; the first registration wins.
/// </summary>
public interface I{projectName}Authorization : ILzAuthorization 
{{ 
    public async Task<List<string>> GetUserPermissionsAsync(string lzUserId, string userName, string table)
    {{
        // Since default methods can't access instance state, we call the helper method that can.
        return await GetUserDefaultPermissionsAsync(lzUserId, userName, table);
    }}
    public Task<List<string>> GetUserDefaultPermissionsAsync(string lzUserId, string userName, string table);

    public async Task LoadPermissionsAsync()
    {{
        // Since default methods can't access instance state, we call the helper method that can.
        await LoadDefaultPermissionsAsync();    
    }}   
    public Task LoadDefaultPermissionsAsync();
}}

/// <summary>
/// Implement the GetUserPermisionsAsync and LoadPermissionsAsync methods to override the default behavior
/// provided by the methods defined in the interface definition. Since this is a partial class, you define 
/// these methods in a separate file (not having the *.g.cs suffix). You can call the helper methods, 
/// GetUserDefaultPermissionsAsync and LoadDefaultPermissionsAsync, to get the default behavior and then 
/// modify it as needed.
/// </summary>
public partial class {projectName}Authorization : LzAuthorization, I{projectName}Authorization
{{
    
    public virtual async Task<List<string>> GetUserDefaultPermissionsAsync(string lzUserId, string userName, string table)
    {{
        // TODO: generated code here
        return await Task.FromResult(new List<string>());
    }}

    public virtual async Task LoadDefaultPermissionsAsync()
    {{
        // TODO: generatedcode here
        await Task.CompletedTask;
    }}
}}
";
            File.WriteAllText(filePath, ReplaceLineEndings(code)); // Write the controller class file
        }
        private static void GenerateControllerClass(ref CompilationUnitSyntax root, OpenApiDocument openApiDocument, List<string> interfaces, string projectName, string filePath)
        {
            AddAbstractModifier(ref root);

            RemoveAsyncFromInterfaceMethodNames(ref root); // Remove Async from interface method names

            InsertPragma(ref root, "1998", "Disable async warning."); // Disable async warning

            RemoveConstructor(ref root); // Remove constructor 

            RemoveMember(ref root, "_implementation"); // Remove _implementation field

            InsertControllerBaseClass(ref root); // Inherit from Microsoft.AspNetCore.Mvc.Controller

            InsertRepoVars(ref root, interfaces);

            MarkMethodsVirtualAsync(ref root); // Make all methods virtual async 

            UpdateControllerMethodBodies(ref root, openApiDocument, projectName); // Use x-lz-gencall attributes to generate method bodies   

            InsertMethodIntoClass(ref root, "\r\n\t\tprotected virtual void Init() { }"); // Add Init method

            var code = root.ToFullString();
            
            FixNswagSyntax(code); // NSwag seems to have a _template bug. Microsoft.AspNetCore.Mvc.HttpGET should be Microsoft.AspNetCore.Mvc.HttpGet

            File.WriteAllText(filePath, ReplaceLineEndings(code)); // Write the controller class file
        }
        private static void AddAbstractModifier(ref CompilationUnitSyntax root)
        {
            var targetClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            var updatedModifiers = targetClass.Modifiers.Where(m => !m.IsKind(SyntaxKind.AbstractKeyword)).ToList();

            if (!targetClass.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            {
                updatedModifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.PublicKeyword));
            }

            int publicIndex = updatedModifiers.FindIndex(m => m.IsKind(SyntaxKind.PublicKeyword));
            updatedModifiers.Insert(publicIndex + 1, SyntaxFactory.Token(SyntaxKind.AbstractKeyword).WithTrailingTrivia(SyntaxFactory.Space));

            var updatedClass = targetClass.WithModifiers(SyntaxFactory.TokenList(updatedModifiers));
            root = root.ReplaceNode(targetClass, updatedClass);

        }
        private static void InsertClassAttribute(ref CompilationUnitSyntax root, string attribute)
        {
            var targetClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            if (targetClass == null)
                return;

            var nonControllerAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attribute));
            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(nonControllerAttribute));
            var updatedClass = targetClass.AddAttributeLists(attributeList);
            root = root.ReplaceNode(targetClass, updatedClass);

        }
        private static void RemoveAsyncFromInterfaceMethodNames(ref CompilationUnitSyntax root)
        {
            root = root.ReplaceNodes(
                root.DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>()
                    .SelectMany(i => i.DescendantNodes().OfType<MethodDeclarationSyntax>()),
                (originalMethod, updatedMethod) =>
                {
                    var originalName = originalMethod.Identifier.Text;
                    if (originalName.EndsWith("Async"))
                    {
                        var newName = originalName.Substring(0, originalName.Length - "Async".Length);
                        return updatedMethod.WithIdentifier(SyntaxFactory.Identifier(newName));
                    }
                    return updatedMethod;
                });
        }
        private static void InsertPragma(ref CompilationUnitSyntax root, string warningCode, string message = "")
        {
            // Insert Disable pragma
            var disablePragmaStr = $"#pragma warning disable {warningCode} // Disable \"CS{warningCode} {message}\"";
            // ParseAndAdd the pragma Directives to get SyntaxTriviaList.
            var disablePragmaTriviaList = SyntaxFactory.ParseLeadingTrivia(disablePragmaStr);
            //disablePragmaTriviaList = SyntaxFactory.AddRange(disablePragmaTriviaList).TriviaList(SyntaxFactory.CarriageReturnLineFeed);
            disablePragmaTriviaList = disablePragmaTriviaList.Add(SyntaxFactory.CarriageReturnLineFeed);

            // Find the last #pragma warning disable directive and the first #pragma warning restore directive.
            var lastDisablePragma = root.DescendantTrivia()
                .LastOrDefault(trivia => trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia)
                                        && trivia.GetStructure() is PragmaWarningDirectiveTriviaSyntax t
                                        && t.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword));
            // Insert the new disable pragma after the last one.
            if (lastDisablePragma != default)
                root = root.InsertTriviaAfter(lastDisablePragma, disablePragmaTriviaList);

            // Insert Restore pragma
            var restorePragmaStr = $"#pragma warning restore {warningCode}";
            var lastRestorePragma = root.DescendantTrivia()
                .LastOrDefault(trivia => trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia)
                                        && trivia.GetStructure() is PragmaWarningDirectiveTriviaSyntax t
                                        && t.DisableOrRestoreKeyword.IsKind(SyntaxKind.RestoreKeyword));
            var restorePragmaTriviaList = SyntaxFactory.ParseLeadingTrivia(restorePragmaStr);
            restorePragmaTriviaList = SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed).AddRange(restorePragmaTriviaList);
            // Insert the new restore pragma before the first restore.
            if (lastRestorePragma != default)
                root = root.InsertTriviaAfter(lastRestorePragma, restorePragmaTriviaList);
        }
        private static void RemoveConstructor(ref CompilationUnitSyntax root)
        {
            var constructor = root
                 ?.DescendantNodes().OfType<ConstructorDeclarationSyntax>()
                 .FirstOrDefault();
            root = root.RemoveNodes(new[] { constructor }, SyntaxRemoveOptions.KeepNoTrivia);
        }
        private static void RemoveMember(ref CompilationUnitSyntax root, string memberName)
        {
            // First, try to find fields that match the member name
            var fieldsToRemove = root
                ?.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .Where(field => field.Declaration.Variables.Any(v => v.Identifier.ValueText.Equals(memberName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (fieldsToRemove != null && fieldsToRemove.Count > 0)
            {
                // If the field declaration has multiple variables, only remove the target variable
                foreach (var field in fieldsToRemove)
                {
                    if (field.Declaration.Variables.Count > 1)
                    {
                        var newDeclaration = field.Declaration.RemoveNode(field.Declaration.Variables.First(v => v.Identifier.ValueText.Equals(memberName, StringComparison.OrdinalIgnoreCase)), SyntaxRemoveOptions.KeepNoTrivia);
                        root = root.ReplaceNode(field, field.WithDeclaration(newDeclaration));
                    }
                    else
                    {
                        root = root.RemoveNode(field, SyntaxRemoveOptions.KeepNoTrivia);
                    }
                }
                return;
            }

            // If not a field, then check for methods, properties, etc.
            var member = root
                ?.DescendantNodes()
                .OfType<MemberDeclarationSyntax>()
                .Where(x =>
                    (x is MethodDeclarationSyntax method && method.Identifier.ValueText.Equals(memberName, StringComparison.OrdinalIgnoreCase)) ||
                    (x is PropertyDeclarationSyntax prop && prop.Identifier.ValueText.Equals(memberName, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault();

            if (member != null)
            {
                root = root.RemoveNode(member, SyntaxRemoveOptions.KeepNoTrivia);
            }
        }
        private static void InsertControllerBaseClass(ref CompilationUnitSyntax root)
        {
            root = root.ReplaceNodes(
                root.DescendantNodes().OfType<ClassDeclarationSyntax>(),
                (originalClass, updatedClass) =>
                {
                    // Create a new simple base class syntax for the specified class.
                    var baseClassSyntax = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("Microsoft.AspNetCore.Mvc.Controller"));

                    // Add or update the base class.
                    var baseList = updatedClass.BaseList;

                    if (baseList == null)
                        return updatedClass.WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseClassSyntax)));

                    // If there are already base types (i.e., a base class or interfaces), 
                    // insert the new base class at the beginning of the list.
                    var existingBaseTypes = updatedClass.BaseList.Types;
                    var newBaseTypes = SyntaxFactory.SeparatedList<BaseTypeSyntax>()
                        .Add(baseClassSyntax)
                        .AddRange(existingBaseTypes);

                    return updatedClass.WithBaseList(updatedClass.BaseList.WithTypes(newBaseTypes));
                });
        }
        private static void InsertRepoVars(ref CompilationUnitSyntax root, List<string> interfaces, List<string> dependencies = null)
        {
            var varDeclarations = string.Empty;
            interfaces.ForEach(x => varDeclarations += $"\r\n\t\tprotected {x} {DownCaseFirstChar(x.Substring(1))};");
            if(dependencies != null)
                dependencies.ForEach(x => varDeclarations += $"\r\n\t\tprotected {x};");
            InsertIntoClass(ref root, varDeclarations);

        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "<Pending>")]
        private static void MarkMethodsVirtualAsync(ref CompilationUnitSyntax root)
        {
            root = root.ReplaceNodes(
                root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .SelectMany(c => c.DescendantNodes().OfType<MethodDeclarationSyntax>()),
                (originalMethod, updatedMethod) =>
                {
                    // Check if the method already has 'virtual', 'override', 'sealed', 'abstract', or 'static' modifiers.
                    if (originalMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword) ||
                                                            m.IsKind(SyntaxKind.OverrideKeyword) ||
                                                            m.IsKind(SyntaxKind.SealedKeyword) ||
                                                            m.IsKind(SyntaxKind.AbstractKeyword) ||
                                                            m.IsKind(SyntaxKind.StaticKeyword)))
                    {
                        return originalMethod;
                    }

                    // Add the 'virtual' modifier.
                    return updatedMethod
                        .AddModifiers(SyntaxFactory.Token(SyntaxKind.VirtualKeyword).WithTrailingTrivia(SyntaxFactory.Whitespace(" ")))
                        .AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword).WithTrailingTrivia(SyntaxFactory.Whitespace(" ")));

                });
        }
        private static void InsertConstructor(ref CompilationUnitSyntax root, string projectName, List<string> repos)
        {

            var constArguments = new List<string>();
            repos.ForEach(x => constArguments.Add($"I{char.ToUpper(x[0]) + x.Substring(1)} {x}"));

            var argumentAssignments = new List<string>();
            repos.ForEach(x => argumentAssignments.Add($"this.{x} = {x};"));

            var varDeclarations = new List<string>();
            repos.ForEach(x => varDeclarations.Add($"protected I{char.ToUpper(x[0]) + x.Substring(1)} {x};"));

            var code =
$@"
        public {projectName}Controller(
            {string.Join(",\r\t\t\t", constArguments)}
            ) 
        {{
            {string.Join("\r\t\t\t", argumentAssignments)}
        }}
            {string.Join("\r\t\t\t", varDeclarations)}
";
            InsertIntoClass(ref root, code);

        }
        private static Dictionary<string, Dictionary<string, string>> MethodExtensionsData(OpenApiDocument openApiDocument)
        {
            //Todo: Need a new process?
            var map = new Dictionary<string, Dictionary<string, string>>();
            //var endpointkeys = directive.EndPoints
            //    .Where(x => x.Value.TagName.Equals(projectName, StringComparison.CurrentCultureIgnoreCase))
            //    .Select(x => x.Key)
            //    .ToList();

            // Use x-* keys for operationId
            foreach (var path in openApiDocument.Paths)
                foreach (var operation in path.Value.Values)
                    if (operation.OperationId != null /* && endpointkeys.Contains(UpCaseFirstChar(operation.OperationId)) */)
                    {
                        if (!map.ContainsKey(operation.OperationId))
                            map[operation.OperationId] = new Dictionary<string, string>();
                        var extList = map[operation.OperationId];
                        if (operation.ExtensionData != null)
                            foreach (var extension in operation.ExtensionData)
                                extList.Add(extension.Key, extension.Value.ToString());
                    }
            return map;
        }
        private static void UpdateControllerMethodBodies(ref CompilationUnitSyntax root, OpenApiDocument openApiDocument, string projectName)
        {
            var methodExtensions = MethodExtensionsData(openApiDocument); // Dictionary<operationId, Dictionary<extensionKey, extensionValue>>   

            root = root.ReplaceNodes(
                root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .SelectMany(i => i.DescendantNodes().OfType<MethodDeclarationSyntax>()),
                        (originalMethod, updatedMethod) =>
                        {
                            string body = string.Empty;
                            string indent = "            ";
                            var methodName = originalMethod.Identifier.Text;
                            if (methodExtensions.TryGetValue(DownCaseFirstChar(methodName), out var extensions))
                            {
                                if (extensions.ContainsKey("x-lz-gencall"))
                                {
                                    body = $"{indent}var callerInfo = await {DownCaseFirstChar(projectName)}Authorization.GetCallerInfoAsync(this.Request);";
                                    body += $"\r\n{indent}return await {extensions["x-lz-gencall"]};";
                                }
                            }

                            if (string.IsNullOrEmpty(body))
                                body = $"{indent}throw new NotImplementedException();";

                            var dummyMethod = $@"      void DummyMethod() 
        {{
{body}
        }}";
                            var newBodySyntax = SyntaxFactory.ParseStatement(dummyMethod)
                                .DescendantNodes()
                                .OfType<BlockSyntax>().First();
                            return updatedMethod.WithBody(newBodySyntax);
                        });
        }
        private static string FixNswagSyntax(string code)
        {
            code = code.Replace("Microsoft.AspNetCore.Mvc.HttpGET", "Microsoft.AspNetCore.Mvc.HttpGet");
            code = code.Replace("Microsoft.AspNetCore.Mvc.HttpPUT", "Microsoft.AspNetCore.Mvc.HttpPut");
            code = code.Replace("Microsoft.AspNetCore.Mvc.HttpPOST", "Microsoft.AspNetCore.Mvc.HttpPost");
            code = code.Replace("Microsoft.AspNetCore.Mvc.HttpUPDATE", "Microsoft.AspNetCore.Mvc.HttpUpdate");
            code = code.Replace("Microsoft.AspNetCore.Mvc.HttpDELETE", "Microsoft.AspNetCore.Mvc.HttpDelete");
            return code;
        }
        private static string UpCaseFirstChar(string token)
        {
            if (string.IsNullOrEmpty(token))
                return token;
            return token.Substring(0, 1).ToUpper() + token.Substring(1);
        }
        private static string DownCaseFirstChar(string token)
        {
            if (string.IsNullOrEmpty(token))
                return token;
            return token.Substring(0, 1).ToLower() + token.Substring(1);
        }
        public static void InsertMethodIntoClass(ref CompilationUnitSyntax root, string methodSignatureText)
        {
            // ParseAndAdd the text to insert
            //var methodSyntax = SyntaxFactory.ParseMemberDeclaration(methodSignatureText) as MethodDeclarationSyntax;
            var methodTree = CSharpSyntaxTree.ParseText(methodSignatureText);
            var methodRoot = methodTree.GetRoot();
            var methodSyntax = methodRoot.DescendantNodes()
                                         .OfType<MethodDeclarationSyntax>()
                                         .FirstOrDefault();

            var classDeclaration = root.DescendantNodes()
                                       .OfType<ClassDeclarationSyntax>()
                                       .First();

            // Create a new class with the inserted members
            var updatedClass = classDeclaration.AddMembers(methodSyntax);

            // Replace the old class with the updated class in the syntax tree
            root = root.ReplaceNode(classDeclaration, updatedClass);
        }
        public static void InsertIntoClass(ref CompilationUnitSyntax root, string textToInsert)
        {
            // ParseAndAdd the text to insert
            var parsedMembers = SyntaxFactory.ParseCompilationUnit(textToInsert)
                                             .DescendantNodes()
                                             .OfType<MemberDeclarationSyntax>()
                                             .ToList();

            var classDeclaration = root.DescendantNodes()
                                       .OfType<ClassDeclarationSyntax>()
                                       .First();

            // Create a new class with the inserted members
            var updatedClass = classDeclaration.AddMembers(parsedMembers.ToArray());

            // Replace the old class with the updated class in the syntax tree
            root = root.ReplaceNode(classDeclaration, updatedClass);
        }
        private static void GenerateImplClassGenFile(string projectName, List<string> interfaces, List<string> dependencies, string filePath)
        {
            // Generate constructor arguments of the form "IClassName className"
            var constructorArguments = new List<string>();
            interfaces.ForEach(x => constructorArguments.Add($"{x} {DownCaseFirstChar(x.Substring(1))}")); // Iterfaces have the form "IClassName"
            dependencies.ForEach(x => constructorArguments.Add(x)); // Dependencies have the form "IClassName className"

            var constructorAssignments = new List<string>();
            interfaces.ForEach(x => constructorAssignments.Add($"this.{DownCaseFirstChar(x.Substring(1))} = {DownCaseFirstChar(x.Substring(1))};"));
            dependencies.ForEach(x =>
            {
                var parts = x.Split(' ');
                if (parts.Length != 2)
                    throw new Exception($"Invalid dependency: {x}. Missing variable name. Ex: ICICD cicd");
                var varname = parts[1];
                constructorAssignments.Add($"this.{varname} = {varname};");
            });

            var classbody = $@"
//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
//     Create your own cs file for this partial class and implement any endpoint methods 
//     not having an x-lz-gencall attribute.
//     Note that NSWAG is hardcoded to add 'Controller' to the classname.
// </auto-generated>
//----------------------
namespace {projectName}
{{
    public partial class {projectName}ControllerImpl : {projectName}Controller
    {{
        public {projectName}ControllerImpl(
            {string.Join(",\r\n\t\t\t", constructorArguments.Select(x => x))}
            ) 
        {{
            {string.Join("\r\n\t\t\t", constructorAssignments.Select(x => x))}

            Init();
        }}
    }}
}}
";
            File.WriteAllText(filePath, classbody);
        }
        private static void GenerateServiceRegistrationsClass(string projectName, string nameSpace, List<string> interfaces, string filePath)
        {
            var registrations = new List<string>();
            interfaces.ForEach(x => registrations.Add($"services.{x}();")); 

            var classbody = $@"
//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
//     Implement another class for registrations not directly generated by LazyMagic.
// </auto-generated>
//----------------------
namespace {nameSpace}
{{
    public static partial class {projectName}Registrations 
    {{
        public static IServiceCollection Add{projectName}(this IServiceCollection services) 
        {{
            services.TryAddSingleton<I{projectName}Authorization, {projectName}Authorization>();
            services.TryAddSingleton<I{projectName}Controller, {projectName}ControllerImpl>();
            {string.Join("\r\n\t\t\t", registrations.Select(x => x))}
            CustomConfigurations(services);
            return services;            
        }}
        static partial void CustomConfigurations(IServiceCollection sdervices);
    }}
}}
";
            File.WriteAllText(filePath, classbody);
        }
    }
}
