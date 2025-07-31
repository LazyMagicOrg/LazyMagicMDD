using NSwag.CodeGeneration.CSharp;
using System;   
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.OpenApiUtils;
using static LazyMagic.ArtifactUtils;
using NSwag;
using System.ComponentModel;
using NSwag.Collections;

namespace LazyMagic
{
    public class DotNetHttpApiSDKProject : DotNetProjectBase
    {
        #region Properties
        public override string Template { get; set; } = "ProjectTemplates/ClientSDK";
        public override string OutputFolder { get; set; } = "ClientSDKs";

        public override string ProjectFilePath
        {
            get => ExportedProjectPath;
            set => ExportedProjectPath = value;
        }
        #endregion

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            try
            {
                await Task.Delay(0);
                Api directive = (Api)directiveArg;
                var apiPath = directive.Key;   
                // Set the project name and namespace
                var projectName = directive.Key;
                projectName += NameSuffix ?? "";    
                var nameSpace = projectName;
                Info($"Generating {directive.Key} {projectName}");

                // The SDK project dependences are interesting:
                // The Api directive contains a list of containers. Each container contains a list of modules.
                // Each module has a list of ExportedOpenApiSpecs.
                // Each Module contains a list of schemas. Each schema has a list of ExportedOpenApiSpecs.
                // The OpenApiSpecs for this project are the union of the OpenApiSpecs for the containers and schemas.
                // This is also true for the other types of dependences.
                // Note that there is no direct reference to the Containers in the Api directive. This is because the 
                // Containers are purely Host side and are only used in this project to determine the dependences.

                // Get Aggregate Modules and Schemas for the Api
                var controllers = GetModulesForApi(directive, solution.Directives);
                var schemas = GetSchemasForApi(directive, solution.Directives);

                // Get Artificat Depenencies
                var controllerArtifacts = solution.Directives.GetArtifactsByType<DotNetControllerProject>(controllers).ToList<ArtifactBase>();
                var schemaArtifacts = solution.Directives.GetArtifactsByType<DotNetSchemaProject>(schemas).ToList<ArtifactBase>();

                // Get Dependencies - only schema projects, not controller modules (since client interfaces are now generated locally)
                ProjectReferences.AddRange(GetExportedProjectReferences(schemaArtifacts));
                // ProjectReferences.AddRange(GetExportedProjectReferences(controllerArtifacts)); // Removed - client interfaces now generated in SDK

                PackageReferences.AddRange(GetExportedPackageReferences(controllerArtifacts));
                PackageReferences = PackageReferences.Distinct().ToList();

                GlobalUsings.AddRange(GetExportedGlobalUsings(schemaArtifacts));
                // GlobalUsings.AddRange(GetExportedGlobalUsings(controllerArtifacts)); // Removed - client SDKs don't need module/repo usings
                GlobalUsings = GlobalUsings.Distinct().ToList();

                ServiceRegistrations.AddRange(GetExportedServiceRegistrations(schemaArtifacts));
                //ServiceRegistrations.AddRange(GetExportedServiceRegistrations(controllerArtifacts));
                ServiceRegistrations = ServiceRegistrations.Distinct().ToList();

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

                var lambdaProjects = GetLambdaProjects(directive, solution.Directives); 
                var openApiSpecs = new List<string>();
                var moduleNames = new HashSet<string>(); // Collect unique module names
                
                // Get all containers referenced by this API to discover modules
                foreach(var containerName in directive.Containers)
                {
                    var container = solution.Directives[containerName] as Container;
                    if (container?.Modules != null)
                    {
                        moduleNames.UnionWith(container.Modules);
                    }
                }
                
                foreach(var lambdaProject in lambdaProjects)
                    if(!string.IsNullOrEmpty(lambdaProject.ExportedOpenApiSpec))
                        openApiSpecs.Add(lambdaProject.ExportedOpenApiSpec);
                var openApiSpec = await MergeApiFilesAsync(solution.SolutionRootFolderPath, openApiSpecs);
                File.WriteAllText(Path.Combine(targetProjectDir, "openapi.g.yaml"), openApiSpec);

                OpenApiDocument openApiDocument = await ParseOpenApiYamlContent(openApiSpec);
                // Add the apiPath to each path 
                var paths = openApiDocument.Paths;
                // ToList() is necessary here because we are modifying Paths
                foreach (var path in openApiDocument.Paths.Keys.ToList())
                {
                    var value = openApiDocument.Paths[path];
                    paths.Remove(path);
                    paths.Add($"/{apiPath}{path}", value);
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
                    OperationNameGenerator = new LzOperationNameGenerator() // todo: This may be problematic. It really needs to be done at the module level. Remove?
                };
                var nswagGenerator = new CSharpClientGenerator(openApiDocument, nswagSettings);
                var code = nswagGenerator.GenerateFile();

                GenerateClientSDKClass(code, projectName, Path.Combine(solution.SolutionRootFolderPath, OutputFolder, projectName, projectName + ".g.cs"), moduleNames.ToList());

                // Exports
                ExportedProjectPath = Path.Combine(OutputFolder, projectName, projectName + ".csproj");
            } catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} {ex.Message}");    
            }
        }
        private void GenerateClientSDKClass(string code, string projectName, string filePath, List<string> moduleNames)
        {
            // Generate the client SDK
            var root = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();
            root = RemoveGeneratedSchemaClasses(root, new List<string> { "ApiException", projectName }); // preserve ApiException and client class
            
            // Store the original interface to extract methods for module interfaces
            var originalInterface = root
                ?.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                .FirstOrDefault();
            
            // Remove the NSWAG-generated interface (we'll create our own)
            RemoveInterface(ref root);
            
            // Write the client class file (without the interface)
            File.WriteAllText(filePath, root.ToFullString());
            
            var directory = Path.GetDirectoryName(filePath);
            
            // Generate individual module client interfaces
            if (originalInterface != null && moduleNames != null && moduleNames.Any())
            {
                GenerateModuleClientInterfaces(originalInterface, moduleNames, directory, projectName);
            }
            
            // Generate and write our custom interface that inherits from module interfaces
            var interfaceCode = GenerateAggregateInterface(projectName, moduleNames);
            var interfaceFilePath = Path.Combine(directory, $"I{projectName}.g.cs");
            File.WriteAllText(interfaceFilePath, interfaceCode);
        }

        private string GenerateAggregateInterface(string projectName, List<string> moduleNames)
        {
            if (moduleNames == null || !moduleNames.Any())
            {
                // If no modules, return empty interface
                return $@"//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
// </auto-generated>
//----------------------

namespace {projectName}
{{
    public partial interface I{projectName}
    {{
        // No modules to inherit from
    }}
}}";
            }
            
            var interfaces = moduleNames.Select(m => $"I{m}Client").ToList();
            var inheritanceList = string.Join(", ", interfaces);
            
            return $@"//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
// </auto-generated>
//----------------------

namespace {projectName}
{{
    public partial interface I{projectName} : {inheritanceList}
    {{
        // All methods inherited from module client interfaces
    }}
}}";
        }

        private void GenerateModuleClientInterfaces(InterfaceDeclarationSyntax originalInterface, List<string> moduleNames, string directory, string projectName)
        {
            // Get the namespace from the original interface
            var namespaceName = originalInterface.Ancestors()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault()?.Name.ToString() ?? projectName;

            // Extract all methods from the original interface
            var allMethods = originalInterface.Members
                .OfType<MethodDeclarationSyntax>()
                .ToList();

            // Generate a client interface for each module
            foreach (var moduleName in moduleNames)
            {
                // Filter methods that belong to this module (prefix match)
                var moduleMethods = allMethods
                    .Where(m => m.Identifier.ToString().StartsWith($"{moduleName}"))
                    .ToList();

                if (moduleMethods.Any())
                {
                    var clientInterfaceCode = GenerateModuleClientInterface(moduleName, moduleMethods, namespaceName);
                    var moduleInterfaceFilePath = Path.Combine(directory, $"I{moduleName}Client.g.cs");
                    File.WriteAllText(moduleInterfaceFilePath, clientInterfaceCode);
                }
            }
        }

        private string GenerateModuleClientInterface(string moduleName, List<MethodDeclarationSyntax> methods, string namespaceName)
        {
            var methodDeclarations = new List<string>();
            
            foreach (var method in methods)
            {
                var returnType = method.ReturnType.ToString();
                var transformedReturnType = TransformReturnTypeForClient(returnType);
                var methodName = method.Identifier.ToString();
                
                // Get parameters
                var parameters = method.ParameterList.Parameters
                    .Select(p => $"{p.Type} {p.Identifier}")
                    .ToList();
                var parameterList = string.Join(", ", parameters);
                
                // Get XML documentation if present
                var xmlDoc = method.GetLeadingTrivia()
                    .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || 
                               t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                    .Select(t => t.ToString())
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(xmlDoc))
                {
                    // Clean up and properly format the XML documentation
                    var docLines = xmlDoc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in docLines)
                    {
                        var trimmedLine = line.Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                        {
                            // Ensure proper indentation and comment prefix
                            if (trimmedLine.StartsWith("///"))
                            {
                                methodDeclarations.Add($"        {trimmedLine}");
                            }
                            else if (trimmedLine.StartsWith("//"))
                            {
                                methodDeclarations.Add($"        {trimmedLine}");
                            }
                            else
                            {
                                // Add the /// prefix if missing
                                methodDeclarations.Add($"        /// {trimmedLine}");
                            }
                        }
                    }
                }
                
                methodDeclarations.Add($"        {transformedReturnType} {methodName}({parameterList});");
                methodDeclarations.Add(""); // Empty line between methods
            }

            // Remove the last empty line
            if (methodDeclarations.Any() && string.IsNullOrEmpty(methodDeclarations.Last()))
            {
                methodDeclarations.RemoveAt(methodDeclarations.Count - 1);
            }

            var methodsCode = string.Join("\r\n", methodDeclarations);

            return $@"//----------------------
// <auto-generated>
//     Generated by LazyMagic, do not edit directly. Changes will be overwritten.
//     This is the client interface version with client-appropriate return types.
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

        private static string TransformReturnTypeForClient(string originalReturnType)
        {
            // Task<ActionResult<T>> -> Task<T>
            if (originalReturnType.StartsWith("Task<ActionResult<") && originalReturnType.EndsWith(">>"))
            {
                var innerType = originalReturnType.Substring("Task<ActionResult<".Length);
                innerType = innerType.Substring(0, innerType.Length - 2); // Remove >>
                return $"Task<{innerType}>";
            }
            
            // Task<IActionResult> -> Task
            if (originalReturnType == "Task<IActionResult>")
            {
                return "Task";
            }
            
            // Keep other types unchanged
            return originalReturnType;
        }
        
        private static void RemoveInterface(ref CompilationUnitSyntax root)
        {
            var interfaceNode = root
                ?.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                .FirstOrDefault();
            if (interfaceNode != null)
            {
                root = root.RemoveNodes(new[] { interfaceNode }, SyntaxRemoveOptions.KeepNoTrivia);
            }
        }
        
        private List<DotNetApiLambdaProject> GetLambdaProjects(Api directive, Directives directives)
        {
            var projects = new List<DotNetApiLambdaProject>(); 
            foreach(var containerName in directive.Containers)
            {
                var container = (Container)directives[containerName];
                foreach(var artifact in container.Artifacts.Values.Where(x => x is DotNetApiLambdaProject))
                {
                    projects.Add((DotNetApiLambdaProject)artifact);
                }
            }
            return projects;    

        }

    }
}
