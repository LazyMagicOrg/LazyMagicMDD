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
using System.Text.RegularExpressions;
using System.Xml.Schema;
using DotNet.Globbing;
using Newtonsoft.Json.Linq;

namespace LazyMagic
{
    public class DotNetControllerProject : DotNetProjectBase
    {
        #region Properties
        public override string ProjectFilePath
        {
            get => ExportedProjectPath;
            set => ExportedProjectPath = value;
        }
        public override string Template { get; set; } = "ProjectTemplates/Controller";
        public override string OutputFolder { get; set; } = "Modules";

        public string ExportedOpenApiSpec { get; set; } = "";
        public string ExportedClientInterface { get; set; } = "";
        public string ExportedClientInterfaceName { get; set; } = "";
        public string ExportedClientInterfaceProjectPath { get; set; } = "";
        public string OperationType { get; set; } = "default";
        public string FlowThroughDomain { get; set; } = "";
        public string FlowThroughPort { get; set; } = "";
        public bool AutoGenCall { get; set; } = false;   
        public string FlowThroughHelpersTpl { get; set; } = "ProjectTemplates/Controller/FlowThroughHelpers.tpl";

        public string ControllerLifetime { get; set; } = "Singleton";
        public bool GenerateClientInterface { get; set; } = true;

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

                // OpenApiSpec contains the paths for this module and 
                // schema objects for all modules. This allows schemas to be 
                // easily shared across modules. NSWAG will fail if a path operation
                // references a schema not in the spec.
                var openApiSpec = directive.OpenApiSpec;
                OpenApiDocument openApiDocument = await ParseOpenApiYamlContent(openApiSpec);

                var modulePath = directive.Key.Replace(".","").Replace("-",""); // Replace dots and dashes for C# member name compatibility

                // Here we modify the OpenApi spec paths and operationIds to 
                // use the Module name as a prefix. This allows modules, with 
                // similar paths to be used in the same Container.
                var paths = openApiDocument.Paths;
                var pathKeys = paths.Keys.ToList();
                foreach(var path in pathKeys)
                {
                    var openApiPathItem = paths[path]; // OpenApiPathItem is a Dictionary<string path, OpenApiOperation operation>
                    foreach (var operation in openApiPathItem)
                    {
                        var opKey = operation.Key.ToString();
                        var opValue = operation.Value;
                        var operationId = opValue.OperationId;
                        if (string.IsNullOrEmpty(operationId))
                        {
                            operationId = OpenApiUtils.GenerateOperationId(opKey, path); //ex "get", "/users/{id}" -> "GetUserId"
                        }
                        else
                            operationId = char.ToUpper(operationId[0]) + operationId.Substring(1);

                        operationId = modulePath + operationId; // Prefix the operationId with the module name to avoid conflicts
                        
                        // Append "Async" suffix if not already present to match generated method names
                        if (!operationId.EndsWith("Async"))
                        {
                            operationId += "Async";
                        }
                        
                        opValue.OperationId = operationId;
                    }

                    // Modify the path to include the module name as a prefix
                    var newModulePath = '/' + modulePath;
                    if(!path.StartsWith(newModulePath + '/')) {
                        var newPath = $"{newModulePath}{path}"; // Add the module name as a prefix
                        paths.Remove(path);
                        paths.Add(newPath, openApiPathItem); // Add the modified path
                    }
                }
                var openApiDocumentYanl = openApiDocument.ToYaml();
                
                // Fix NSwag serialization bug: parameter references incorrectly get /schema appended
                // e.g., '#/components/parameters/top/schema' should be '#/components/parameters/top'
                openApiDocumentYanl = FixParameterReferences(openApiDocumentYanl);

                var openApiSpecs = directive.OpenApiSpecs ?? new List<string>();
                var schemas = directive.Schemas;

                //  Get controller dependencies from repo and schema projects
                var interfaces = new List<string>() { $"I{projectName}Authorization" };
                
                // Track which schemas have repo projects
                var schemasWithRepos = new HashSet<string>();
                
                // First, get dependencies from DotNetRepoProject artifacts
                var dependantRepoArtifacts = solution.Directives.GetArtifactsByType<DotNetRepoProject>(schemas);
                foreach (var dotNetRepoArtifact in dependantRepoArtifacts)
                {
                    var dotNetRepoProject = dotNetRepoArtifact as DotNetRepoProject;
                    ProjectReferences.Add(dotNetRepoProject.ExportedProjectPath);
                    PackageReferences.AddRange(dotNetRepoProject.ExportedPackages);
                    ServiceRegistrations.AddRange(dotNetRepoProject.ExportedServiceRegistrations);
                    GlobalUsings.AddRange(dotNetRepoProject.ExportedGlobalUsings);
                    interfaces.AddRange(dotNetRepoProject.ExportedInterfaces);
                }
                
                // Find which schemas have repo projects by checking each schema directive
                foreach (var schemaKey in schemas)
                {
                    if (solution.Directives.TryGetValue(schemaKey, out var schemaDirective) && schemaDirective is Schema schema)
                    {
                        if (schema.Artifacts.Values.Any(a => a is DotNetRepoProject))
                        {
                            schemasWithRepos.Add(schemaKey);
                        }
                    }
                }
                
                // Get dependencies from DotNetSchemaProject artifacts for schemas without repo projects
                var schemasWithoutRepos = schemas.Where(s => !schemasWithRepos.Contains(s)).ToList();
                var schemaOnlyArtifacts = solution.Directives.GetArtifactsByType<DotNetSchemaProject>(schemasWithoutRepos);
                foreach (var dotNetSchemaArtifact in schemaOnlyArtifacts)
                {
                    var dotNetSchemaProject = dotNetSchemaArtifact as DotNetSchemaProject;
                    ProjectReferences.Add(dotNetSchemaProject.ExportedProjectPath);
                    PackageReferences.AddRange(dotNetSchemaProject.ExportedPackages);
                    GlobalUsings.AddRange(dotNetSchemaProject.ExportedGlobalUsings);
                }
                
                ProjectReferences = ProjectReferences.Distinct().ToList();  
                PackageReferences = PackageReferences.Distinct().ToList();
                ServiceRegistrations = ServiceRegistrations.Distinct().ToList();
                GlobalUsings = GlobalUsings.Distinct().ToList();
                interfaces = interfaces.Distinct().ToList();

                // Add Polly global usings for flowthrough operations
                if (OperationType == "flowthrough")
                {
                    GlobalUsings.Add("Polly");
                    GlobalUsings.Add("Polly.Extensions.Http");
                    GlobalUsings.Add("Microsoft.Extensions.Http");
                    GlobalUsings.Add("System.Net.Http.Json");
                    PackageReferences.Add("Microsoft.Extensions.Http.Polly");
                }

                // Copy the template project to the target project. Removes *.g.* files.
                var flowThroughHelpersTplPath = CombinePath(solution.SolutionRootFolderPath, FlowThroughHelpersTpl);
                var flowThroughHelpersTpl = File.ReadAllText(flowThroughHelpersTplPath);
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
               // We only use the NSWAG generated code as a starting point. It is not 
               // very well suited for generated interface overriding.
               var nswagSettings = new CSharpControllerGeneratorSettings
               {
                   UseActionResultType = true,
                   ClassName = projectName,
                   ControllerTarget = NSwag.CodeGeneration.CSharp.Models.CSharpControllerTarget.AspNetCore,
                   CSharpGeneratorSettings =
                       {
                            Namespace = nameSpace,
                            GenerateOptionalPropertiesAsNullable = true,
                            GenerateNullableReferenceTypes = false,
                            GenerateDefaultValues = true,
                       },
                   GenerateOptionalParameters = true,
               };
                var nswagGenerator = new CSharpControllerGenerator(openApiDocument, nswagSettings);
                var code = nswagGenerator.GenerateFile();

                // OK, now we start munging the NSWAG generated code with Roslyn
                var root = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();

                // We don't need the schema classes so strip them out
                root = RemoveGeneratedSchemaClasses(root); 

                // Clean it up to make it readable.
                var scratchpad = root.ToFullString();
                code = scratchpad.Replace($"partial class {projectName}Controller",$"abstract partial class {projectName}ControllerBase")
                    .Replace(": Microsoft.AspNetCore.Mvc.ControllerBase",$": Controller, I{projectName}Controller")
                    .Replace("Microsoft.AspNetCore.Mvc.","") // Just a cosmetic. Improves readability.
                    .Replace("System.Threading.Tasks.",""); // Just a cosmetic. Improves readability.

                /// Remove the NSWAG #pragma lines
                code = Regex.Replace(code,
                    @"^\s*#pragma.*$[\r\n]?",
                    "",
                    RegexOptions.Multiline);

                root = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();

                // Transform methods with x-lz-fromform to use [FromForm] parameter
                var fromFormOperations = GetFromFormOperations(openApiDocument);
                if (fromFormOperations.Count > 0)
                {
                    TransformFromFormMethods(ref root, fromFormOperations, modulePath, openApiDocument);
                }

                // Extract and save the Interface file
                // RemoveAsyncFromInterfaceMethodNames(ref root); // Removed to keep Async suffix for proper inheritance with client SDK
                var interfaceCode = GetInterfaceCode(root);
                File.WriteAllText(Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, $"I{projectName}Controller") + ".g.cs", interfaceCode);

                // Client interface generation moved to DotNetHttpApiSDKProject

                // Remove the Interface from the compilation unit
                RemoveInterface(ref root);

                // Finalize the {projectName}ControllerBase class 
                code = root.ToFullString();
                code =
@"// NSWAG code refactored by LazyMagic.
// We use NSWAG to generate a baseclass, partial class and interface. 
// I{ModuleName}Controller.g.cs - defines a partial interface
// {ModuleName}ControllerBase.g.cs - defines the base class
// {ModuleName}Controller.g.cs - defines a partial class that inherits the base class
// To add or override class behavior, create a new partial class file
// {projectName}Controller.cs - overrides methods in the base class
// Dependency Injection system.
// Note: We also generate some helper classes 
// {ModuleName}Authorization.g.cs - Partial class for Authorization system
// {ModuleName}Registration.g.cs - Registers classes with the DI system
//
"
                    + code;

                root = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();

                GenerateBaseClass(ref root, openApiDocument, interfaces, Dependencies, projectName, Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, $"{projectName}ControllerBase") + ".g.cs", OperationType, AutoGenCall, flowThroughHelpersTpl);

                GenerateAuthorizationClass(ref root, openApiDocument, projectName, nameSpace, Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, $"{projectName}Authorization") + ".g.cs");

                GenerateServiceRegistrationsClass(projectName, nameSpace, ServiceRegistrations, Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, $"{projectName}Registrations") + ".g.cs", ControllerLifetime, OperationType); // This class contains the extension methods to register necesssry services

                // Write the partial class
                var classCode =
$@"
namespace {projectName};
public partial class {projectName}Controller : {projectName}ControllerBase {{}}
";
                root = CSharpSyntaxTree.ParseText(classCode).GetCompilationUnitRoot();
                InsertConstructor(ref root, projectName + "Controller", interfaces, Dependencies, OperationType);
                classCode = root.ToFullString();
                File.WriteAllText(Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, $"{projectName}Controller") + ".g.cs", classCode);


                // Exports
                // Write Modified OpenApi specs to file
                var exportedOpenApiSpec = Path.Combine(OutputFolder, projectName, "openapi.g.yaml");
                File.WriteAllText(Path.Combine(solution.SolutionRootFolderPath, exportedOpenApiSpec), openApiDocumentYanl);

                ExportedProjectPath = Path.Combine(OutputFolder, projectName, projectName) + ".csproj";
                ExportedServiceRegistrations = new List<string> { $"Add{projectName}" };
                
                // Export global usings, but exclude flowthrough-specific ones (they're module-internal)
                var flowthroughUsings = new HashSet<string> { "Polly", "Polly.Extensions.Http", "Microsoft.Extensions.Http", "System.Net.Http.Json" };
                ExportedGlobalUsings = GlobalUsings.Where(u => !flowthroughUsings.Contains(u)).Distinct().ToList();
                ExportedGlobalUsings.Add(nameSpace);
                ExportedOpenApiSpecs = openApiSpecs;
                ExportedOpenApiSpec = exportedOpenApiSpec;
                ExportedClientInterface = Path.Combine(OutputFolder, projectName, $"I{projectName}Client.g.cs");
                ExportedClientInterfaceName = $"I{projectName}Client";
                foreach (var path in openApiDocument.Paths)
                    ExportedPathOps.Add((path.Key, path.Value.Keys.ToList()));

                // Generate client interface project if enabled
                if (GenerateClientInterface)
                {
                    // Get schema dependencies for client interface (not repo dependencies)
                    var dependantSchemaArtifacts = solution.Directives.GetArtifactsByType<DotNetSchemaProject>(schemas);
                    await GenerateClientInterfaceProject(solution, directive, openApiDocument, dependantSchemaArtifacts.ToList());
                }

            } 
            catch (Exception ex)
            {
                throw new Exception($"Error Generating {GetType().Name} for {projectName}. {ex.Message}");
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
                foreach(var artifact in schemaKVP.Value.Artifacts.Values)
                {
                    if(remainingEntities.Count == 0) break;
                    if (artifact is DotNetRepoProject)
                    {
                        if(remainingEntities.Count == 0) break;
                        var dotNetRepoProject = artifact as DotNetRepoProject;
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

        private async Task GenerateClientInterfaceProject(SolutionBase solution, Module directive, OpenApiDocument openApiDocument, List<DotNetSchemaProject> dependantSchemaArtifacts)
        {
            await Task.Delay(0);
            var clientProjectName = directive.Key + "ClientInterface";
            var clientOutputFolder = "ClientSDKs";
            var nameSpace = directive.Key; // Use module name as namespace
            
            Info($"Generating client interface project {clientProjectName}");

            // Copy BasicLib template to target directory
            var sourceProjectDir = CombinePath(solution.SolutionRootFolderPath, "ProjectTemplates/BasicLib");
            var targetProjectDir = CombinePath(solution.SolutionRootFolderPath, Path.Combine(clientOutputFolder, clientProjectName));
            var csprojFileName = GetCsprojFile(sourceProjectDir);
            var filesToExclude = new List<string> { csprojFileName, "User.props", "SRCREADME.md" };
            CopyProject(sourceProjectDir, targetProjectDir, filesToExclude);

            // Create project file
            File.Copy(
                Path.Combine(sourceProjectDir, csprojFileName),
                Path.Combine(targetProjectDir, clientProjectName + ".csproj"),
                overwrite: true);

            // Generate project references from schema artifacts (client-side DTOs only)
            var clientProjectReferences = new List<string>();
            var clientPackageReferences = new List<string>();
            var clientGlobalUsings = new List<string>();

            foreach (var dotNetSchemaProject in dependantSchemaArtifacts)
            {
                clientProjectReferences.Add(dotNetSchemaProject.ExportedProjectPath);
                clientPackageReferences.AddRange(dotNetSchemaProject.ExportedPackages);
                clientGlobalUsings.AddRange(dotNetSchemaProject.ExportedGlobalUsings);
            }

            clientProjectReferences = clientProjectReferences.Distinct().ToList();
            clientPackageReferences = clientPackageReferences.Distinct().ToList();
            clientGlobalUsings = clientGlobalUsings.Distinct().ToList();
            clientGlobalUsings.Add(nameSpace);

            // Generate Projects.g.props
            GenerateProjectsPropsFile(clientProjectReferences, Path.Combine(targetProjectDir, "Projects.g.props"));

            // Generate GlobalUsing.g.cs
            var globalUsingsContent = @"// This file is copied from the ProjectTemplate
// Do not modify in the target project or your changes
// will be overwritten.
// Add a GlobalUsing.cs file in the target project
// to add additional global usings.

global using LazyMagic.Shared;

";
            GenerateGlobalUsingFile(clientGlobalUsings, globalUsingsContent, Path.Combine(targetProjectDir, "GlobalUsing.g.cs"));

            // Generate the client interface from the same OpenAPI document
            var interfaceCode = await GenerateModuleClientInterface(directive.Key, nameSpace, openApiDocument);
            var interfaceFilePath = Path.Combine(targetProjectDir, $"I{directive.Key}Client.g.cs");
            File.WriteAllText(interfaceFilePath, interfaceCode);

            // Generate FileParameter class (needed for file upload operations)
            var fileParameterCode = GenerateFileParameterClass(nameSpace);
            var fileParameterFilePath = Path.Combine(targetProjectDir, "FileParameter.g.cs");
            File.WriteAllText(fileParameterFilePath, fileParameterCode);

            // Set export
            ExportedClientInterfaceProjectPath = Path.Combine(clientOutputFolder, clientProjectName, clientProjectName + ".csproj");
        }

        private string GenerateFileParameterClass(string namespaceName)
        {
            return $@"//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
// </auto-generated>
//----------------------

namespace {namespaceName}
{{
    /// <summary>
    /// Represents a file parameter for multipart form data uploads.
    /// </summary>
    public class FileParameter
    {{
        public FileParameter(System.IO.Stream data, string fileName = null, string contentType = null)
        {{
            Data = data;
            FileName = fileName;
            ContentType = contentType;
        }}

        public System.IO.Stream Data {{ get; }}
        public string FileName {{ get; }}
        public string ContentType {{ get; }}
    }}
}}
";
        }

        private async Task<string> GenerateModuleClientInterface(string moduleName, string namespaceName, OpenApiDocument openApiDocument)
        {
            await Task.Delay(0);
            
            // Use NSWAG to generate the controller interface in memory first
            var nswagSettings = new CSharpControllerGeneratorSettings
            {
                UseActionResultType = true,
                ClassName = $"{moduleName}Controller",
                ControllerTarget = NSwag.CodeGeneration.CSharp.Models.CSharpControllerTarget.AspNetCore,
                GenerateModelValidationAttributes = false,
                CSharpGeneratorSettings = 
                {
                    Namespace = namespaceName,
                    GenerateDataAnnotations = false,
                    ClassStyle = NJsonSchema.CodeGeneration.CSharp.CSharpClassStyle.Inpc
                }
            };
            
            var nswagGenerator = new CSharpControllerGenerator(openApiDocument, nswagSettings);
            var controllerCode = nswagGenerator.GenerateFile();
            
            // Parse the generated controller interface and transform it to client interface
            var root = CSharpSyntaxTree.ParseText(controllerCode).GetCompilationUnitRoot();

            // Apply x-lz-fromform transformation before extracting interface
            var fromFormOperations = GetFromFormOperations(openApiDocument);
            if (fromFormOperations.Count > 0)
            {
                var modulePath = moduleName.Replace(".", "").Replace("-","");
                TransformFromFormMethodsInInterface(ref root, fromFormOperations, modulePath, openApiDocument);
            }

            var interfaceNode = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().FirstOrDefault();
            
            if (interfaceNode == null)
            {
                return GenerateEmptyClientInterface(moduleName, namespaceName);
            }
            
            // Transform the interface for client use
            var clientMethods = new List<string>();
            
            foreach (var method in interfaceNode.Members.OfType<MethodDeclarationSyntax>())
            {
                // Transform the method for client interface
                var clientMethod = TransformControllerMethodToClientMethod(method, fromFormOperations, openApiDocument);
                if (!string.IsNullOrEmpty(clientMethod))
                {
                    clientMethods.Add(clientMethod);
                }
            }
            
            var methodsCode = string.Join("\r\n\r\n", clientMethods);

            return $@"//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
// </auto-generated>
//----------------------

namespace {namespaceName}
{{
    public partial interface I{moduleName}Client
    {{
{methodsCode}
    }}
}}
";
        }
        
        private string GenerateEmptyClientInterface(string moduleName, string namespaceName)
        {
            return $@"//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
// </auto-generated>
//----------------------

namespace {namespaceName}
{{
    public partial interface I{moduleName}Client
    {{
        // No operations found
    }}
}}
";
        }
        
        private string TransformControllerMethodToClientMethod(MethodDeclarationSyntax method, Dictionary<string, string> fromFormOperations = null, OpenApiDocument openApiDocument = null)
        {
            if (method == null) return string.Empty;
            
            // Extract method documentation
            var documentationLines = new List<string>();
            var leadingTrivia = method.GetLeadingTrivia();
            
            foreach (var trivia in leadingTrivia)
            {
                if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || 
                    trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                {
                    var docText = trivia.ToString().Trim();
                    if (!string.IsNullOrEmpty(docText))
                    {
                        // Fix malformed XML comments by ensuring /// prefix
                        var lines = docText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            if (trimmedLine.StartsWith("///"))
                            {
                                documentationLines.Add($"        {trimmedLine}");
                            }
                            else if (trimmedLine.StartsWith("//"))
                            {
                                documentationLines.Add($"        ///{trimmedLine.Substring(2)}");
                            }
                            else if (trimmedLine.StartsWith("<") && (trimmedLine.Contains("summary") || trimmedLine.Contains("param") || trimmedLine.Contains("returns")))
                            {
                                documentationLines.Add($"        /// {trimmedLine}");
                            }
                        }
                    }
                }
            }
            
            // Transform return type from Task<ActionResult<T>> to Task<T>
            var returnType = method.ReturnType.ToString();
            var clientReturnType = TransformReturnTypeForClient(returnType);
            
            // Get method name and parameters
            var methodName = method.Identifier.ToString();
            var parameters = method.ParameterList.ToString();
            
            // Keep FileParameter type - it's generated in the client interface project

            // Check if this method needs x-lz-fromform transformation for client interface
            // Client interfaces don't need [FromForm] attribute, just the type parameter
            if (fromFormOperations != null && fromFormOperations.TryGetValue(methodName, out var formTypeName))
            {
                // Get path parameters from OpenAPI document
                var pathParamNames = openApiDocument != null 
                    ? GetPathParametersFromOpenApi(openApiDocument, methodName)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                // Build new parameter list: keep path params, replace form params with single body param
                var pathQueryParams = new List<string>();
                var addedPathParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var param in method.ParameterList.Parameters)
                {
                    var paramName = param.Identifier.ToString();
                    var paramType = param.Type?.ToString() ?? "object";
                    
                    // Keep path parameters (avoid duplicates from form data)
                    if (pathParamNames.Contains(paramName) && !addedPathParams.Contains(paramName))
                    {
                        pathQueryParams.Add($"{paramType} {paramName}");
                        addedPathParams.Add(paramName);
                    }
                }
                
                // Add the form body parameter
                pathQueryParams.Add($"{formTypeName} body");
                parameters = $"({string.Join(", ", pathQueryParams)})";
            }
            
            // Add ApiException documentation if not present
            var hasApiExceptionDoc = documentationLines.Any(line => line.Contains("ApiException"));
            if (!hasApiExceptionDoc)
            {
                documentationLines.Add("        /// <exception cref=\"ApiException\">A server side error occurred.</exception>");
            }
            
            var documentation = string.Join("\r\n", documentationLines);
            if (!string.IsNullOrEmpty(documentation))
            {
                documentation += "\r\n";
            }
            
            return $"{documentation}        {clientReturnType} {methodName}{parameters};";
        }
         
        private string TransformReturnTypeForClient(string controllerReturnType)
        {
            // Transform Task<ActionResult<T>> to Task<T>
            // Transform Task<IActionResult> to Task
            // Handle both short and fully qualified names
            
            // Handle Microsoft.AspNetCore.Mvc.ActionResult<T>
            if (controllerReturnType.Contains("ActionResult<") && controllerReturnType.EndsWith(">>"))
            {
                // Extract T from Task<ActionResult<T>> or Task<Microsoft.AspNetCore.Mvc.ActionResult<T>>
                var actionResultStart = controllerReturnType.IndexOf("ActionResult<") + "ActionResult<".Length;
                var end = controllerReturnType.LastIndexOf(">>");
                var innerType = controllerReturnType.Substring(actionResultStart, end - actionResultStart);
                return $"System.Threading.Tasks.Task<{innerType}>";
            }
            // Handle IActionResult (both short and fully qualified)
            else if (controllerReturnType.Contains("IActionResult>"))
            {
                return "System.Threading.Tasks.Task";
            }
            // Handle ActionResult<T> without Task wrapper (shouldn't happen but just in case)
            else if (controllerReturnType.Contains("ActionResult<") && controllerReturnType.EndsWith(">") && !controllerReturnType.Contains("Task"))
            {
                var actionResultStart = controllerReturnType.IndexOf("ActionResult<") + "ActionResult<".Length;
                var end = controllerReturnType.LastIndexOf(">");
                var innerType = controllerReturnType.Substring(actionResultStart, end - actionResultStart);
                return $"System.Threading.Tasks.Task<{innerType}>";
            }
            // Handle standalone IActionResult
            else if (controllerReturnType.Contains("IActionResult") && !controllerReturnType.Contains("Task"))
            {
                return "System.Threading.Tasks.Task";
            }
            // Handle Task<T> that's already in correct format
            else if (controllerReturnType.StartsWith("System.Threading.Tasks.Task"))
            {
                return controllerReturnType;
            }
            // Handle Task<T> without full qualification
            else if (controllerReturnType.StartsWith("Task<") && controllerReturnType.EndsWith(">"))
            {
                return $"System.Threading.Tasks.{controllerReturnType}";
            }
            
            // Fallback - assume it's already correct or add Task wrapper
            if (controllerReturnType.Contains("Task"))
            {
                return controllerReturnType;
            }
            else
            {
                return $"System.Threading.Tasks.Task<{controllerReturnType}>";
            }
        }


        private string GetClientReturnTypeFromOperation(NSwag.OpenApiOperation operation)
        {
            var successResponse = operation.Responses.FirstOrDefault(r => r.Key.StartsWith("2"));
            if (successResponse.Value?.Schema != null)
            {
                var schema = successResponse.Value.Schema;
                var typeName = GetClientTypeNameFromSchema(schema);
                
                if (schema.Type == NJsonSchema.JsonObjectType.Array && schema.Item != null)
                {
                    var itemType = GetClientTypeNameFromSchema(schema.Item);
                    return $"System.Threading.Tasks.Task<System.Collections.Generic.ICollection<{itemType}>>";
                }
                else if (!string.IsNullOrEmpty(typeName) && typeName != "void")
                {
                    return $"System.Threading.Tasks.Task<{typeName}>";
                }
            }
            
            return "System.Threading.Tasks.Task";
        }

        private string GetClientTypeNameFromSchema(NJsonSchema.JsonSchema schema)
        {
            if (schema == null)
                return "object";
                
            // Handle schema references (this is where types like TenantUser should be resolved)
            if (schema.Reference != null)
            {
                // Try to get the type name from the reference
                var reference = schema.Reference;
                if (!string.IsNullOrEmpty(reference.Id))
                {
                    return reference.Id;
                }
                
                // If the reference has a type definition, use that
                if (reference.ActualSchema != null)
                {
                    if (!string.IsNullOrEmpty(reference.ActualSchema.Id))
                        return reference.ActualSchema.Id;
                    if (!string.IsNullOrEmpty(reference.ActualSchema.Title))
                        return reference.ActualSchema.Title;
                }
            }
            
            // Check if this is a referenced schema by looking at the actual schema
            if (schema.ActualSchema != null && schema.ActualSchema != schema)
            {
                if (!string.IsNullOrEmpty(schema.ActualSchema.Id))
                    return schema.ActualSchema.Id;
                if (!string.IsNullOrEmpty(schema.ActualSchema.Title))
                    return schema.ActualSchema.Title;
            }
            
            // Check for title or id on the schema itself
            if (!string.IsNullOrEmpty(schema.Id))
            {
                return schema.Id;
            }
            
            if (!string.IsNullOrEmpty(schema.Title))
            {
                return schema.Title;
            }
            
            // Handle primitive types
            switch (schema.Type)
            {
                case NJsonSchema.JsonObjectType.String:
                    return "string";
                case NJsonSchema.JsonObjectType.Integer:
                    return schema.Format == "int64" ? "long" : "int";
                case NJsonSchema.JsonObjectType.Number:
                    return schema.Format == "float" ? "float" : "double";
                case NJsonSchema.JsonObjectType.Boolean:
                    return "bool";
                case NJsonSchema.JsonObjectType.Array:
                    if (schema.Item != null)
                    {
                        var itemType = GetClientTypeNameFromSchema(schema.Item);
                        return $"System.Collections.Generic.ICollection<{itemType}>";
                    }
                    return "System.Collections.Generic.ICollection<object>";
                case NJsonSchema.JsonObjectType.Object:
                    // For object types, this might be a complex type that should have been resolved above
                    // If we get here, it's likely a schema resolution issue
                    return "object";
                default:
                    return "object";
            }
        }

        private List<string> GetClientParametersFromOperation(NSwag.OpenApiOperation operation)
        {
            var parameters = new List<string>();
            var usedParameterNames = new HashSet<string>();
            
            // Add request body parameter if present
            if (operation.RequestBody != null)
            {
                var bodySchema = operation.RequestBody.Content?.FirstOrDefault().Value?.Schema;
                if (bodySchema != null)
                {
                    var typeName = GetClientTypeNameFromSchema(bodySchema);
                    parameters.Add($"{typeName} body");
                    usedParameterNames.Add("body");
                }
            }
            
            foreach (var parameter in operation.Parameters.OrderBy(p => p.IsRequired ? 0 : 1))
            {
                var typeName = GetClientTypeNameFromSchema(parameter.Schema);
                var paramName = parameter.Name;
                
                // Skip if we already have a parameter with this name (avoid duplicates)
                if (usedParameterNames.Contains(paramName))
                {
                    continue;
                }
                usedParameterNames.Add(paramName);
                
                if (parameter.Schema.Type == NJsonSchema.JsonObjectType.Array)
                {
                    typeName = $"System.Collections.Generic.IEnumerable<{GetClientTypeNameFromSchema(parameter.Schema.Item)}>";
                }
                
                if (!parameter.IsRequired && parameter.Schema.Type != NJsonSchema.JsonObjectType.Array)
                {
                    parameters.Add($"{typeName} {paramName} = null");
                }
                else
                {
                    parameters.Add($"{typeName} {paramName}");
                }
            }
            
            return parameters;
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
public partial interface I{projectName}Authorization : ILzAuthorization 
{{ 
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
    
}}
";
            File.WriteAllText(filePath, ReplaceLineEndings(code)); // Write the controller class file
        }
        private static void GenerateBaseClass(ref CompilationUnitSyntax root, 
            OpenApiDocument openApiDocument, 
            List<string> interfaces, 
            List<string> dependencies, 
            string projectName, 
            string filePath, 
            string operationType, 
            bool autoGenCall, 
            string flowThroughHelpersTpl)
        {
            InsertPragma(ref root, "1998", "Disable async warning."); // Disable async warning

            RemoveConstructor(ref root); // Remove constructor 

            RemoveMember(ref root, "_implementation"); // Remove _implementation field 

            InsertRepoVars(ref root, interfaces);

            // Add IHttpClientFactory property for flowthrough operations
            if (operationType == "flowthrough")
            {
                InsertMemberIntoClass(ref root, "\r\n\t\tpublic IHttpClientFactory HttpClientFactory { get; set; }");
                // Generate the flowthrough helpers in a separate file to avoid NSwag processing issues
                var flowthroughFilePath = filePath.Replace("ControllerBase.g.cs", "FlowThroughHelpers.g.cs");
                GenerateFlowThroughHelpersFile(projectName, flowthroughFilePath, flowThroughHelpersTpl);
            }

            EnsureMethodsHaveAsyncSuffix(ref root); // Ensure all methods have Async suffix to match interface

            MarkMethodsVirtualAsync(ref root); // Make all methods virtual async 

            UpdateControllerMethodBodies(ref root, openApiDocument, projectName, operationType, autoGenCall); // Use x-lz-gencall attributes to generate method bodies   

            InsertMethodIntoClass(ref root, "\r\n\t\tprotected virtual void Init() { }"); // Add Init method

            var code = root.ToFullString();
            
            FixNswagSyntax(code); // NSwag seems to have a _template bug. Microsoft.AspNetCore.Mvc.HttpGET should be Microsoft.AspNetCore.Mvc.HttpGet

            File.WriteAllText(filePath, ReplaceLineEndings(code)); // Write the controller class file
        }

        /// <summary>
        /// Generates a separate file containing flow-through helper methods as a partial class.
        /// This avoids issues with NSwag processing (async suffix addition, method body replacement).
        /// </summary>
        private static void GenerateFlowThroughHelpersFile(string projectName, string filePath, string flowThroughHelpersTpl)
        {
            // The module path is the project name without "Module" suffix if present
            var modulePath = projectName.EndsWith("Module") 
                ? projectName.Substring(0, projectName.Length - "Module".Length) 
                : projectName;
            modulePath = modulePath.Replace(".", "").Replace("-", "");
            
            var code = flowThroughHelpersTpl
                .Replace("{projectName}", projectName)
                .Replace("{modulePath}", modulePath); 
            File.WriteAllText(filePath, ReplaceLineEndings(code));
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
        private static string GetInterfaceCode(CompilationUnitSyntax root) {

            var namespaceName = root
                ?.DescendantNodes().OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault()
                .Name.ToFullString();

            var interfaceCode = root
                 ?.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                 .FirstOrDefault()
                 .ToFullString();

            var code = $@"
namespace {namespaceName} 
{{
{interfaceCode}
}}
";
            return ReplaceLineEndings(code);
        }

        // Client interface generation methods moved to DotNetHttpApiSDKProject
        private static void RemoveInterface(ref CompilationUnitSyntax root)
        {
            var interfaceNode = root
                ?.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                .FirstOrDefault();
            root = root.RemoveNodes(new[] { interfaceNode }, SyntaxRemoveOptions.KeepNoTrivia);
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
        private static void InsertRepoVars(ref CompilationUnitSyntax root, List<string> interfaces, List<string> dependencies = null)
        {
            var varDeclarations = string.Empty;
            interfaces.ForEach(x => varDeclarations += $"\r\n\t\tpublic {x} {x.Substring(1)} {{ get; set; }}");
            if(dependencies != null)
                dependencies.ForEach(x => varDeclarations += $"\r\n\t\tpublic {x};");
            InsertMemberIntoClass(ref root, varDeclarations);

        }

        private static void EnsureMethodsHaveAsyncSuffix(ref CompilationUnitSyntax root)
        {
            root = root.ReplaceNodes(
                root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .SelectMany(c => c.DescendantNodes().OfType<MethodDeclarationSyntax>()),
                (originalMethod, updatedMethod) =>
                {
                    var originalName = originalMethod.Identifier.Text;
                    if (!originalName.EndsWith("Async"))
                    {
                        var newName = originalName + "Async";
                        return updatedMethod.WithIdentifier(SyntaxFactory.Identifier(newName));
                    }
                    return originalMethod;
                });
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
        private static void InsertConstructor(ref CompilationUnitSyntax root, string constructorName, List<string> interfaces, List<string> dependencies, string operationType = "default")
        {

            // Generate constructor arguments of the form "IClassName className"
            var constructorArguments = new List<string>();
            interfaces.ForEach(x => constructorArguments.Add($"{x} {DownCaseFirstChar(x.Substring(1))}")); // Iterfaces have the form "IClassName"
            dependencies.ForEach(x => constructorArguments.Add(x)); // Dependencies have the form "IClassName className"

            // Add IHttpClientFactory for flowthrough operations
            if (operationType == "flowthrough")
            {
                constructorArguments.Add("IHttpClientFactory httpClientFactory");
            }

            var constructorAssignments = new List<string>();
            interfaces.ForEach(x => constructorAssignments.Add($"{x.Substring(1)} = {DownCaseFirstChar(x.Substring(1))};"));
            dependencies.ForEach(x =>
            {
                var parts = x.Split(' ');
                if (parts.Length != 2)
                    throw new Exception($"Invalid dependency: {x}. Missing variable name. Ex: ICICD cicd");
                var varname = parts[1];
                constructorAssignments.Add($"this.{varname} = {varname};");
            });

            // Add HttpClientFactory assignment for flowthrough operations
            if (operationType == "flowthrough")
            {
                constructorAssignments.Add("HttpClientFactory = httpClientFactory;");
            }

            var code =
$@"
        public {constructorName}(
            {string.Join(",\r\n\t\t\t", constructorArguments.Select(x => x))}
            ) 
        {{
            {string.Join("\r\n\t\t\t", constructorAssignments.Select(x => x))}

            Init();
        }}
";

            InsertMemberIntoClass(ref root, code);

        }
        private static Dictionary<string, Dictionary<string, string>> MethodExtensionsData(OpenApiDocument openApiDocument)
        {
            var map = new Dictionary<string, Dictionary<string, string>>();

            // Use x-* keys for operationId
            foreach (var path in openApiDocument.Paths)
                foreach (var operation in path.Value.Values)
                    if (operation.OperationId != null /* && endpointkeys.Contains(UpCaseFirstChar(operation.OperationId)) */)
                    {
                        var opId = operation.OperationId.Replace(".", "_").Replace("-", "_");
                        if (!map.ContainsKey(opId))
                            map[opId] = new Dictionary<string, string>();
                        var extList = map[opId];
                        if (operation.ExtensionData != null)
                            foreach (var extension in operation.ExtensionData)
                                extList.Add(extension.Key, extension.Value.ToString());
                    }
            return map;
        }

        /// <summary>
        /// Extracts x-lz-fromform extension data from OpenAPI operations.
        /// Returns a dictionary mapping operationId to the form type name (from $ref).
        /// </summary>
        private static Dictionary<string, string> GetFromFormOperations(OpenApiDocument openApiDocument)
        {
            var map = new Dictionary<string, string>();

            foreach (var path in openApiDocument.Paths)
            {
                foreach (var operation in path.Value.Values)
                {
                    if (operation.OperationId == null || operation.ExtensionData == null)
                        continue;

                    if (operation.ExtensionData.TryGetValue("x-lz-fromform", out var fromFormValue))
                    {
                        // The value is expected to be an object with $ref property
                        // e.g., { "$ref": "#/components/schemas/CarouselWidgetForm" }
                        string typeName = null;

                        if (fromFormValue is IDictionary<string, object> fromFormDict)
                        {
                            if (fromFormDict.TryGetValue("$ref", out var refValue))
                            {
                                var refString = refValue?.ToString();
                                if (!string.IsNullOrEmpty(refString))
                                {
                                    // Extract type name from $ref: "#/components/schemas/CarouselWidgetForm" -> "CarouselWidgetForm"
                                    var lastSlash = refString.LastIndexOf('/');
                                    typeName = lastSlash >= 0 ? refString.Substring(lastSlash + 1) : refString;
                                }
                            }
                        }
                        else if (fromFormValue is JObject jObj)
                        {
                            // Handle JObject (common when parsing YAML/JSON)
                            var refToken = jObj["$ref"];
                            if (refToken != null)
                            {
                                var refString = refToken.ToString();
                                if (!string.IsNullOrEmpty(refString))
                                {
                                    var lastSlash = refString.LastIndexOf('/');
                                    typeName = lastSlash >= 0 ? refString.Substring(lastSlash + 1) : refString;
                                }
                            }
                        }
                        else if (fromFormValue is string refString && refString.Contains("/"))
                        {
                            // Handle case where value is directly a $ref string
                            var lastSlash = refString.LastIndexOf('/');
                            typeName = lastSlash >= 0 ? refString.Substring(lastSlash + 1) : refString;
                        }

                        if (!string.IsNullOrEmpty(typeName))
                        {
                            map[operation.OperationId] = typeName;
                        }
                    }
                }
            }

            return map;
        }

        /// <summary>
        /// Extracts path parameter names from the OpenAPI document for a given operation.
        /// </summary>
        private static HashSet<string> GetPathParametersFromOpenApi(OpenApiDocument openApiDocument, string operationId)
        {
            var pathParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var path in openApiDocument.Paths)
            {
                foreach (var operation in path.Value.Values)
                {
                    if (operation.OperationId == operationId)
                    {
                        // Get parameters that are in path
                        foreach (var param in operation.Parameters.Where(p => p.Kind == NSwag.OpenApiParameterKind.Path))
                        {
                            pathParams.Add(param.Name);
                        }
                        return pathParams;
                    }
                }
            }
            
            return pathParams;
        }

        /// <summary>
        /// Transforms methods that have x-lz-fromform to use a single [FromForm] parameter
        /// instead of individual form field parameters.
        /// </summary>
        private static void TransformFromFormMethods(ref CompilationUnitSyntax root, Dictionary<string, string> fromFormOperations, string modulePath, OpenApiDocument openApiDocument = null)
        {
            // Build a set of method names that need transformation (with module prefix and Async suffix)
            var methodsToTransform = new Dictionary<string, string>();
            foreach (var kvp in fromFormOperations)
            {
                // The operationId in fromFormOperations already has the module prefix and Async suffix
                // from the earlier processing in GenerateAsync
                methodsToTransform[kvp.Key] = kvp.Value;
            }

            if (methodsToTransform.Count == 0)
                return;

            var allMethods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            
            // Match methods by name - class methods don't have Async suffix yet, interface methods do
            // So we need to match both with and without Async suffix
            var matchingMethods = allMethods.Where(m => {
                var methodName = m.Identifier.Text;
                // Direct match
                if (methodsToTransform.ContainsKey(methodName))
                    return true;
                // Try adding Async suffix for class methods
                if (methodsToTransform.ContainsKey(methodName + "Async"))
                    return true;
                return false;
            }).ToList();

            // Transform ALL matching methods (both class and interface)
            root = root.ReplaceNodes(
                matchingMethods,
                (originalMethod, updatedMethod) =>
                {
                    var methodName = originalMethod.Identifier.Text;
                    
                    // Get form type name - try direct match first, then with Async suffix
                    string formTypeName;
                    string operationIdForOpenApi;
                    if (methodsToTransform.TryGetValue(methodName, out formTypeName))
                    {
                        operationIdForOpenApi = methodName;
                    }
                    else if (methodsToTransform.TryGetValue(methodName + "Async", out formTypeName))
                    {
                        operationIdForOpenApi = methodName + "Async";
                    }
                    else
                    {
                        return originalMethod;
                    }

                    // Check if this is a class method (has attributes) or interface method (no attributes)
                    var isClassMethod = originalMethod.AttributeLists.Count > 0;

                    // Get path parameters from OpenAPI document
                    var pathParamNames = openApiDocument != null 
                        ? GetPathParametersFromOpenApi(openApiDocument, operationIdForOpenApi)
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Separate parameters into path parameters and form parameters
                    // Track which path params we've already added to avoid duplicates
                    // (form data might have fields with same name as path params)
                    var parameters = originalMethod.ParameterList.Parameters;
                    var pathParams = new List<ParameterSyntax>();
                    var addedPathParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var hasFormParams = false;

                    foreach (var param in parameters)
                    {
                        var paramName = param.Identifier.Text;
                        
                        // Check if this parameter name matches a path parameter AND we haven't added it yet
                        if (pathParamNames.Contains(paramName) && !addedPathParams.Contains(paramName))
                        {
                            pathParams.Add(param);
                            addedPathParams.Add(paramName);
                        }
                        else
                        {
                            // This is a form parameter (or duplicate path param) - we'll replace with single body param
                            hasFormParams = true;
                        }
                    }

                    if (!hasFormParams)
                    {
                        return originalMethod;
                    }

                    // Create the new parameter - with [FromForm] for class methods, without for interface methods
                    ParameterSyntax formParameter;
                    var typeWithSpace = SyntaxFactory.ParseTypeName(formTypeName)
                        .WithTrailingTrivia(SyntaxFactory.Space);
                    
                    if (isClassMethod)
                    {
                        var fromFormAttribute = SyntaxFactory.Attribute(
                            SyntaxFactory.IdentifierName("FromForm"));
                        var attributeList = SyntaxFactory.AttributeList(
                            SyntaxFactory.SingletonSeparatedList(fromFormAttribute));

                        formParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("body"))
                            .WithType(typeWithSpace)
                            .WithAttributeLists(SyntaxFactory.SingletonList(attributeList));
                    }
                    else
                    {
                        // Interface methods don't have attributes
                        formParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("body"))
                            .WithType(typeWithSpace);
                    }

                    // Build the new parameter list: path params first, then body
                    var newParams = new List<ParameterSyntax>(pathParams);
                    newParams.Add(formParameter);

                    var newParameterList = SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList(newParams));

                    return updatedMethod.WithParameterList(newParameterList);
                });
        }

        /// <summary>
        /// Transforms methods in interfaces that have x-lz-fromform to use a single parameter
        /// instead of individual form field parameters. Used for client interface generation.
        /// </summary>
        private static void TransformFromFormMethodsInInterface(ref CompilationUnitSyntax root, Dictionary<string, string> fromFormOperations, string modulePath, OpenApiDocument openApiDocument = null)
        {
            // Build a set of method names that need transformation
            var methodsToTransform = new Dictionary<string, string>();
            foreach (var kvp in fromFormOperations)
            {
                methodsToTransform[kvp.Key] = kvp.Value;
            }

            if (methodsToTransform.Count == 0)
                return;

            root = root.ReplaceNodes(
                root.DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>()
                    .SelectMany(i => i.DescendantNodes().OfType<MethodDeclarationSyntax>())
                    .Where(m => methodsToTransform.ContainsKey(m.Identifier.Text)),
                (originalMethod, updatedMethod) =>
                {
                    var methodName = originalMethod.Identifier.Text;
                    var formTypeName = methodsToTransform[methodName];

                    // Get path parameters from OpenAPI document
                    var pathParamNames = openApiDocument != null 
                        ? GetPathParametersFromOpenApi(openApiDocument, methodName)
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Separate parameters into path parameters and form parameters
                    var parameters = originalMethod.ParameterList.Parameters;
                    var pathQueryParams = new List<ParameterSyntax>();
                    var addedPathParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var hasFormParams = false;

                    foreach (var param in parameters)
                    {
                        var paramName = param.Identifier.Text;
                        
                        // Keep path parameters (avoid duplicates from form data)
                        if (pathParamNames.Contains(paramName) && !addedPathParams.Contains(paramName))
                        {
                            pathQueryParams.Add(param);
                            addedPathParams.Add(paramName);
                        }
                        else
                        {
                            // This is a form parameter - we'll replace all of them
                            hasFormParams = true;
                        }
                    }

                    if (!hasFormParams)
                        return originalMethod;

                    // Create the form body parameter (no [FromForm] attribute for interfaces)
                    var formParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("body"))
                        .WithType(SyntaxFactory.ParseTypeName(formTypeName));

                    // Build the new parameter list: path/query params first, then body
                    var newParams = new List<ParameterSyntax>(pathQueryParams);
                    newParams.Add(formParameter);

                    var newParameterList = SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList(newParams));

                    return updatedMethod.WithParameterList(newParameterList);
                });
        }

        private static void UpdateControllerMethodBodies(ref CompilationUnitSyntax root, OpenApiDocument openApiDocument, string projectName, string operationType, bool autoGenCall)
        {
            var methodExtensions = MethodExtensionsData(openApiDocument); // Dictionary<operationId, Dictionary<extensionKey, extensionValue>>   
            var operationDetails = GetOperationDetails(openApiDocument); // Dictionary<operationId, (httpMethod, path, returnType, hasBody)>

            var code = root.ToFullString();
            var yaml = openApiDocument.ToYaml();
            root = root.ReplaceNodes(
                root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .SelectMany(i => i.DescendantNodes().OfType<MethodDeclarationSyntax>()),
                        (originalMethod, updatedMethod) =>
                        {
                            string body = string.Empty;
                            string indent = "            ";
                            var methodName = originalMethod.Identifier.Text;
                            
                            // Skip the Init method - it has its own implementation
                            if (methodName == "Init")
                            {
                                return originalMethod;
                            }
                            if (methodExtensions.TryGetValue(methodName, out var extensions))
                            {
                                if(autoGenCall || extensions.ContainsKey("x-lz-gencall"))
                                {
                                    var gencallValue = (extensions.ContainsKey("x-lz-gencall")) ? extensions["x-lz-gencall"] : "";

                                    // If the gencall starts with "throw", don't wrap with "return await"
                                    if (gencallValue.TrimStart().StartsWith("throw", StringComparison.OrdinalIgnoreCase))
                                    {
                                        body = $"{indent}throw new NotImplementedException();";
                                    }
                                    else
                                    {
                                        switch (operationType)
                                        {
                                            case "default":
                                                body = $"{indent}var callerInfo = await {projectName}Authorization.GetCallerInfoAsync(this.Request);";
                                                body += $"\r\n{indent}return await {gencallValue};";
                                                break;
                                            case "flowthrough":
                                                var odata = extensions.ContainsKey("x-lz-odatapath") ? extensions["x-lz-odatapath"] : null;
                                                body = $"{indent}var callerInfo = await {projectName}Authorization.GetCallerInfoAsync(this.Request);";
                                                body += "\r\n" + GenerateFlowThroughMethodBody(methodName, originalMethod, operationDetails, projectName, indent, odata);
                                                break;
                                            default:
                                                throw new Exception($"Unknown OperationType: {operationType}");
                                        }
                                    }
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
            code = root.ToFullString();
        }

        /// <summary>
        /// Gets operation details (HTTP method, path, return type, has body) for each operation in the OpenAPI document.
        /// </summary>
        private static Dictionary<string, (string httpMethod, string path, string returnType, bool hasBody, bool isCollection)> GetOperationDetails(OpenApiDocument openApiDocument)
        {
            var details = new Dictionary<string, (string httpMethod, string path, string returnType, bool hasBody, bool isCollection)>();

            foreach (var pathKvp in openApiDocument.Paths)
            {
                var path = pathKvp.Key;
                foreach (var operationKvp in pathKvp.Value)
                {
                    var httpMethod = operationKvp.Key; // "get", "post", etc.
                    var operation = operationKvp.Value;
                    
                    if (string.IsNullOrEmpty(operation.OperationId))
                        continue;

                    // Determine return type from response schema
                    var returnType = "object";
                    var isCollection = false;
                    var successResponse = operation.Responses.FirstOrDefault(r => r.Key.StartsWith("2"));
                    
                    // Try to get schema from response - check ActualResponse first, then Content
                    NJsonSchema.JsonSchema schema = null;
                    if (successResponse.Value != null)
                    {
                        // Try ActualResponse.Schema first (resolved schema)
                        schema = successResponse.Value.ActualResponse?.Schema;
                        
                        // If not found, try Content (OpenAPI 3.0 style)
                        if (schema == null && successResponse.Value.Content != null && successResponse.Value.Content.Count > 0)
                        {
                            // Get schema from first content type (typically application/json)
                            var contentSchema = successResponse.Value.Content.Values.FirstOrDefault()?.Schema;
                            if (contentSchema != null)
                            {
                                schema = contentSchema;
                            }
                        }
                        
                        // Fallback to direct Schema property
                        if (schema == null)
                        {
                            schema = successResponse.Value.Schema;
                        }
                    }
                    
                    if (schema != null)
                    {
                        if (schema.Type == NJsonSchema.JsonObjectType.Array && schema.Item != null)
                        {
                            isCollection = true;
                            returnType = GetTypeNameFromSchema(schema.Item);
                        }
                        else
                        {
                            returnType = GetTypeNameFromSchema(schema);
                        }
                    }
                    else
                    {
                        returnType = "void";
                    }

                    // Check if operation has a request body
                    var hasBody = operation.RequestBody != null;
                    var opId = operation.OperationId.Replace(".", "_").Replace("-", "_");
                    details[opId] = (httpMethod, path, returnType, hasBody, isCollection);
                }
            }

            return details;
        }

        /// <summary>
        /// Gets the C# type name from an OpenAPI schema.
        /// </summary>
        private static string GetTypeNameFromSchema(NJsonSchema.JsonSchema schema)
        {
            if (schema == null)
                return "object";

            // For NJsonSchema, ActualSchema resolves references
            var actualSchema = schema.ActualSchema ?? schema;
            
            // Check DocumentPath - contains full path like "#/components/schemas/AppSettingVm"
            var docPath = actualSchema.DocumentPath;
            if (!string.IsNullOrEmpty(docPath) && docPath.Contains("/schemas/"))
            {
                var lastSlash = docPath.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < docPath.Length - 1)
                {
                    return docPath.Substring(lastSlash + 1);
                }
            }

            // Check if HasReference and get from reference path
            if (schema.HasReference && schema.Reference != null)
            {
                var refPath = schema.Reference.DocumentPath;
                if (!string.IsNullOrEmpty(refPath) && refPath.Contains("/schemas/"))
                {
                    var lastSlash = refPath.LastIndexOf('/');
                    if (lastSlash >= 0 && lastSlash < refPath.Length - 1)
                    {
                        return refPath.Substring(lastSlash + 1);
                    }
                }
            }

            // Check Id property
            if (!string.IsNullOrEmpty(actualSchema.Id))
                return actualSchema.Id;
            
            // Check Title property
            if (!string.IsNullOrEmpty(actualSchema.Title))
                return actualSchema.Title;

            // Handle primitive types
            switch (actualSchema.Type)
            {
                case NJsonSchema.JsonObjectType.String:
                    return "string";
                case NJsonSchema.JsonObjectType.Integer:
                    return actualSchema.Format == "int64" ? "long" : "int";
                case NJsonSchema.JsonObjectType.Number:
                    return actualSchema.Format == "float" ? "float" : "double";
                case NJsonSchema.JsonObjectType.Boolean:
                    return "bool";
                default:
                    return "object";
            }
        }

        /// <summary>
        /// Generates the method body for flow-through operations.
        /// </summary>
        private static string GenerateFlowThroughMethodBody(
            string methodName,
            MethodDeclarationSyntax method,
            Dictionary<string, (string httpMethod, string path, string returnType, bool hasBody, bool isCollection)> operationDetails,
            string projectName,
            string indent,
            string odata)
        {
            if (!operationDetails.TryGetValue(methodName, out var details))
            {
                return $"{indent}throw new NotImplementedException(\"Operation details not found for {methodName}\");";
            }

            var (httpMethod, path, _, hasBody, _) = details;

            // Extract return type directly from method signature - NSwag already has the correct type
            var (returnType, isCollection, hasReturnValue) = ExtractReturnTypeFromMethod(method);

            // Build the path with parameter substitution
            var pathExpression = ConvertPathToInterpolatedString((odata == null) ? path : odata, method);

            // Check if method has a 'body' parameter - if not, we can't pass it
            var hasBodyParameter = method.ParameterList.Parameters.Any(p => p.Identifier.Text == "body");
            
            // If OpenAPI says there's a body but there's no body parameter, don't pass body
            var shouldPassBody = hasBody && hasBodyParameter;

            var body = new System.Text.StringBuilder();

            if (httpMethod.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                if (isCollection)
                {
                    body.AppendLine($"{indent}return await FlowThroughGetCollectionAsync<{returnType}>(callerInfo, {pathExpression});");
                }
                else if (hasReturnValue)
                {
                    body.AppendLine($"{indent}return await FlowThroughGetAsync<{returnType}>(callerInfo, {pathExpression});");
                }
                else
                {
                    // IActionResult return - use non-generic version
                    body.AppendLine($"{indent}return await FlowThroughGetAsync(callerInfo, {pathExpression});");
                }
            }
            else if (httpMethod.Equals("post", StringComparison.OrdinalIgnoreCase))
            {
                if (shouldPassBody)
                {
                    if (hasReturnValue)
                    {
                        body.AppendLine($"{indent}return await FlowThroughPostAsync<{returnType}, object>(callerInfo, {pathExpression}, body);");
                    }
                    else
                    {
                        body.AppendLine($"{indent}return await FlowThroughPostAsync(callerInfo, {pathExpression}, body);");
                    }
                }
                else
                {
                    if (hasReturnValue)
                    {
                        body.AppendLine($"{indent}return await FlowThroughPostAsync<{returnType}, object>(callerInfo, {pathExpression}, new {{}});");
                    }
                    else
                    {
                        body.AppendLine($"{indent}return await FlowThroughPostAsync(callerInfo, {pathExpression}, new {{}});");
                    }
                }
            }
            else if (httpMethod.Equals("put", StringComparison.OrdinalIgnoreCase))
            {
                if (shouldPassBody)
                {
                    if (hasReturnValue)
                    {
                        body.AppendLine($"{indent}return await FlowThroughPutAsync<{returnType}, object>(callerInfo, {pathExpression}, body);");
                    }
                    else
                    {
                        body.AppendLine($"{indent}return await FlowThroughPutAsync(callerInfo, {pathExpression}, body);");
                    }
                }
                else
                {
                    if (hasReturnValue)
                    {
                        body.AppendLine($"{indent}return await FlowThroughPutAsync<{returnType}, object>(callerInfo, {pathExpression}, new {{}});");
                    }
                    else
                    {
                        body.AppendLine($"{indent}return await FlowThroughPutAsync(callerInfo, {pathExpression}, new {{}});");
                    }
                }
            }
            else if (httpMethod.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                if (hasReturnValue)
                {
                    body.AppendLine($"{indent}return await FlowThroughDeleteAsync<{returnType}>(callerInfo, {pathExpression});");
                }
                else
                {
                    body.AppendLine($"{indent}return await FlowThroughDeleteAsync(callerInfo, {pathExpression});");
                }
            }
            else if (httpMethod.Equals("patch", StringComparison.OrdinalIgnoreCase))
            {
                // PATCH uses same pattern as PUT
                if (shouldPassBody)
                {
                    if (hasReturnValue)
                    {
                        body.AppendLine($"{indent}return await FlowThroughPutAsync<{returnType}, object>(callerInfo, {pathExpression}, body);");
                    }
                    else
                    {
                        body.AppendLine($"{indent}return await FlowThroughPutAsync(callerInfo, {pathExpression}, body);");
                    }
                }
                else
                {
                    if (hasReturnValue)
                    {
                        body.AppendLine($"{indent}return await FlowThroughPutAsync<{returnType}, object>(callerInfo, {pathExpression}, new {{}});");
                    }
                    else
                    {
                        body.AppendLine($"{indent}return await FlowThroughPutAsync(callerInfo, {pathExpression}, new {{}});");
                    }
                }
            }
            else
            {
                body.AppendLine($"{indent}throw new NotImplementedException(\"Unsupported HTTP method: {httpMethod}\");");
            }

            return body.ToString().TrimEnd();
        }

        /// <summary>
        /// Extracts the return type from a method's signature.
        /// Handles Task<ActionResult<T>>, Task<ActionResult<ICollection<T>>>, Task<IActionResult>, etc.
        /// </summary>
        private static (string returnType, bool isCollection, bool hasReturnValue) ExtractReturnTypeFromMethod(MethodDeclarationSyntax method)
        {
            var returnTypeStr = method.ReturnType.ToString();
            
            // Check for IActionResult (no return value)
            if (returnTypeStr.Contains("IActionResult") && !returnTypeStr.Contains("ActionResult<"))
            {
                return ("void", false, false);
            }

            // Check for ActionResult<ICollection<T>> or ActionResult<IEnumerable<T>> etc.
            var collectionMatch = Regex.Match(returnTypeStr, @"ActionResult<(?:System\.Collections\.Generic\.)?(?:ICollection|IEnumerable|IList|List)<([^>]+)>>");
            if (collectionMatch.Success)
            {
                var itemType = collectionMatch.Groups[1].Value;
                return (itemType, true, true);
            }

            // Check for ActionResult<T>
            var actionResultMatch = Regex.Match(returnTypeStr, @"ActionResult<([^>]+)>");
            if (actionResultMatch.Success)
            {
                var innerType = actionResultMatch.Groups[1].Value;
                return (innerType, false, true);
            }

            // Fallback
            return ("object", false, true);
        }

        /// <summary>
        /// Converts an OpenAPI path template to a C# interpolated string expression.
        /// E.g., "/api/orders/{id}" becomes $"/api/orders/{id}"
        /// Handles NSwag's parameter renaming (e.g., id -> idPath when there's also idQuery)
        /// </summary>
        private static string ConvertPathToInterpolatedString(string path, MethodDeclarationSyntax method)
        {
            // Get parameter names from method signature for case-sensitive matching
            var paramNames = method.ParameterList.Parameters
                .Select(p => p.Identifier.Text)
                .ToList();

            // Check if path contains any parameters
            if (!path.Contains("{"))
            {
                return $"\"{path}\"";
            }

            // Extract path parameter names from the template (e.g., {id}, {orderId})
            var pathParamMatches = Regex.Matches(path, @"\{([^}]+)\}");
            var result = path;
            
            foreach (Match match in pathParamMatches)
            {
                var pathParamName = match.Groups[1].Value;
                
                // Look for exact match first
                var exactMatch = paramNames.FirstOrDefault(p => 
                    p.Equals(pathParamName, StringComparison.OrdinalIgnoreCase));
                
                if (exactMatch != null)
                {
                    // Found exact match - use it
                    result = Regex.Replace(result, $@"\{{{pathParamName}\}}", $"{{{exactMatch}}}", RegexOptions.IgnoreCase);
                }
                else
                {
                    // Look for renamed parameter (NSwag adds "Path" suffix when there's a conflict)
                    var pathSuffixMatch = paramNames.FirstOrDefault(p => 
                        p.Equals(pathParamName + "Path", StringComparison.OrdinalIgnoreCase));
                    
                    if (pathSuffixMatch != null)
                    {
                        result = Regex.Replace(result, $@"\{{{pathParamName}\}}", $"{{{pathSuffixMatch}}}", RegexOptions.IgnoreCase);
                    }
                }
            }

            return $"$\"{result}\"";
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
        
        /// <summary>
        /// Fixes NSwag serialization bug where parameter references incorrectly get /schema appended.
        /// e.g., '#/components/parameters/top/schema' should be '#/components/parameters/top'
        /// </summary>
        private static string FixParameterReferences(string yaml)
        {
            // Fix parameter references that have /schema appended
            // Pattern: '#/components/parameters/{paramName}/schema' -> '#/components/parameters/{paramName}'
            return System.Text.RegularExpressions.Regex.Replace(
                yaml, 
                @"'#/components/parameters/([^']+)/schema'", 
                "'#/components/parameters/$1'");
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
        public static void InsertMemberIntoClass(ref CompilationUnitSyntax root, string textToInsert)
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
        public static void InsertMemberIntoInterface(ref CompilationUnitSyntax root, string textToInsert)
        {
            // ParseAndAdd the text to insert
            var parsedMembers = SyntaxFactory.ParseCompilationUnit(textToInsert)
                                             .DescendantNodes()
                                             .OfType<MemberDeclarationSyntax>()
                                             .ToList();

            var interfaceDeclaration = root.DescendantNodes()
                                       .OfType<InterfaceDeclarationSyntax>()
                                       .First();

            // Create a new interface with the inserted members
            var updatedInterface = interfaceDeclaration.AddMembers(parsedMembers.ToArray());
            var scratch = updatedInterface.ToFullString();

            // Replace the old class with the updated class in the syntax tree
            root = root.ReplaceNode(interfaceDeclaration, updatedInterface);
        }
        private static void GenerateServiceRegistrationsClass(string projectName, string nameSpace, List<string> interfaces, string filePath, string controllerLifetime, string operationType = "default")
        {
            var registrations = new List<string>();
            interfaces.ForEach(x => registrations.Add($"services.{x}();")); 

            var httpClientRegistration = "";
            var additionalUsings = "";
            
            if (operationType == "flowthrough")
            {
                // NOTE: Flowthrough operations require the Microsoft.Extensions.Http.Polly package.
                // This is added to Packages.g.props automatically.
                // The global usings for Polly are added to GlobalUsing.g.cs automatically.
                additionalUsings = "";
                httpClientRegistration = $@"
            // Register HttpClient for flow-through operations with Polly retry and circuit breaker policies
            services.AddHttpClient(""{projectName}FlowThrough"", client =>
            {{
                var baseUrl = Environment.GetEnvironmentVariable(""LZ_FLOWTHROUGH_{projectName.ToUpper()}_URL"") 
                    ?? ""http://localhost:8080/"";
                client.BaseAddress = new Uri(baseUrl);
                
                var timeout = Environment.GetEnvironmentVariable(""LZ_FLOWTHROUGH_{projectName.ToUpper()}_TIMEOUT"");
                if (int.TryParse(timeout, out var timeoutSeconds))
                {{
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                }}
                else
                {{
                    client.Timeout = TimeSpan.FromSeconds(30);
                }}
            }})
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());
";
            }

            var pollyMethods = "";
            if (operationType == "flowthrough")
            {
                pollyMethods = $@"

        /// <summary>
        /// Creates a retry policy for transient HTTP errors.
        /// Retries 3 times with exponential backoff (2, 4, 8 seconds).
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {{
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }}

        /// <summary>
        /// Creates a circuit breaker policy.
        /// Opens circuit after 5 consecutive failures, stays open for 30 seconds.
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {{
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
        }}";
            }

            var classbody = $@"
//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
//     Implement another class for registrations not directly generated by LazyMagic.
// </auto-generated>
//----------------------
{additionalUsings}namespace {nameSpace}
{{
    public static partial class {projectName}Registrations 
    {{
        public static IServiceCollection Add{projectName}(this IServiceCollection services) 
        {{
            services.TryAddSingleton<I{projectName}Authorization, {projectName}Authorization>();
            services.TryAdd{controllerLifetime}<I{projectName}Controller, {projectName}Controller>();
            {string.Join("\r\n\t\t\t", registrations.Select(x => x))}{httpClientRegistration}
            CustomConfigurations(services);
            return services;            
        }}
        static partial void CustomConfigurations(IServiceCollection sdervices);{pollyMethods}
    }}
}}
";
            File.WriteAllText(filePath, classbody);
        }

        /* Currently unused Methods */
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
        private static void GenerateControllerClassFile(string projectName, List<string> interfaces, List<string> dependencies, string filePath)
        {
            // Generate constructor arguments of the form "IClassName className"
            var constructorArguments = new List<string>();
            interfaces.ForEach(x => constructorArguments.Add($"{x} {DownCaseFirstChar(x.Substring(1))}")); // Iterfaces have the form "IClassName"
            dependencies.ForEach(x => constructorArguments.Add(x)); // Dependencies have the form "IClassName className"

            var constructorAssignments = new List<string>();
            interfaces.ForEach(x => constructorAssignments.Add($"{x.Substring(1)} = {DownCaseFirstChar(x.Substring(1))};"));
            dependencies.ForEach(x =>
            {
                var parts = x.Split(' ');
                if (parts.Length != 2)
                    throw new Exception($"Invalid dependency: {x}. Missing variable name. Ex: ICICD cicd");
                var varname = parts[1];
                constructorAssignments.Add($"this.{varname} = {varname};");
            });

            string varDeclarations = "";
            interfaces.ForEach(x => varDeclarations += $"\r\n\t\tpublic {x} {x.Substring(1)} {{ get; set; }}");
            if (dependencies != null)
                dependencies.ForEach(x => varDeclarations += $"\r\n\t\tpublic {x};");

            var classbody = $@"
//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
//     Create your own cs file for this partial class and implement any endpoint methods 
//     not having an x-lz-gencall attribute.
// </auto-generated>
//----------------------
namespace {projectName}
{{
    public partial class {projectName}Controller : Controller, I{projectName}Controller
    {{
        public {projectName}Controller(
            {string.Join(",\r\n\t\t\t", constructorArguments.Select(x => x))}
            ) 
        {{
            {string.Join("\r\n\t\t\t", constructorAssignments.Select(x => x))}

            Init();
        }}

{varDeclarations}

        partial void Init();
    }}
}}
";


            File.WriteAllText(filePath, classbody);
        }
        private static string UpCaseFirstChar(string token)
        {
            if (string.IsNullOrEmpty(token))
                return token;
            return token.Substring(0, 1).ToUpper() + token.Substring(1);
        }
        private static void MarkInterfaceMethodsAsync(ref CompilationUnitSyntax root)
        {
            root = root.ReplaceNodes(
                root.DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>()
                    .SelectMany(c => c.DescendantNodes().OfType<MethodDeclarationSyntax>()),
                (originalMethod, updatedMethod) =>
                {
                    // Check if the method already has 'virtual', 'override', 'sealed', 'abstract', or 'static' modifiers.
                    if (originalMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
                    {
                        return originalMethod;
                    }

                    // Add the 'virtual' modifier.
                    return updatedMethod
                        .AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword).WithTrailingTrivia(SyntaxFactory.Whitespace(" ")));

                });
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

    }
}
