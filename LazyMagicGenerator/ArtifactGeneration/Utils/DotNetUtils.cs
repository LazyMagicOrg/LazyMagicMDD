using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using DotNet.Globbing;

namespace LazyMagic
{
    public class ProjectCopyException : Exception
    {
        public ProjectCopyException(string message) : base(message) { }
        public ProjectCopyException(string message, Exception innerException) : base(message, innerException) { }
    }

    public static class DotNetUtils
    {
        public static void CopyProject(string sourcePath, string destinationPath, List<string> filesToExclude)
        {
            List<Glob> globs = filesToExclude.Select(x => Glob.Parse(x)).ToList();
            try
            {
                if (string.IsNullOrEmpty(sourcePath))
                    throw new ArgumentNullException(nameof(sourcePath));

                if (string.IsNullOrEmpty(destinationPath))
                    throw new ArgumentNullException(nameof(destinationPath));

                if (!Directory.Exists(sourcePath))
                    throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");

                Directory.CreateDirectory(destinationPath);
                DeleteGeneratedContent(destinationPath); // delete *.g.* files
                CopyProjectFolder(sourcePath, destinationPath);

            }
            catch (Exception ex)
            {
                throw new ProjectCopyException("An error occurred while copying the project.", ex);
            }

            void DeleteGeneratedContent(string path)
            {
                var currentFilePath = "";
                try
                {
                    foreach (string directory in Directory.GetDirectories(path))
                    {
                        string dirName = Path.GetFullPath(directory);
                        DeleteGeneratedContent(dirName);
                    }

                    foreach (string filePath in Directory.GetFiles(path))
                    {
                        currentFilePath = filePath;
                        if (filePath.Contains(".g."))
                            File.Delete(filePath);
                    }
                }
                catch (Exception ex)
                {
                    throw new ProjectCopyException($"Failed to delete generated contents: {currentFilePath}", ex);
                }
            }

            void CopyProjectFolder(string source, string destination)
            {

                try
                {
                    Directory.CreateDirectory(destination);
                    foreach (string filePath in Directory.GetFiles(source))
                    {
                        if (globs.Any(glob => glob.IsMatch(Path.GetFileName(filePath))))
                            continue;

                        string fileName = Path.GetFileName(filePath);
                        string destFile = Path.Combine(destination, fileName);
                        File.Copy(filePath, destFile, overwrite: true);
                    }

                    foreach (string dirPath in Directory.GetDirectories(source))
                    {
                        string dirName = Path.GetFileName(dirPath);

                        if (dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("obj", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string destDir = Path.Combine(destination, dirName);
                        CopyProjectFolder(dirPath, destDir);
                    }
                }
                catch (Exception ex)
                {
                    throw new ProjectCopyException($"Failed to copy folder: {source} to {destination}", ex);
                }
            }
        }
        public static string CombinePath(string basePath, string subPath)
        {
            if (string.IsNullOrEmpty(basePath))
                throw new ArgumentNullException(nameof(basePath));
            if (string.IsNullOrEmpty(subPath))
                throw new ArgumentNullException(nameof(subPath));

            // If subPath is rooted (absolute), return it as is
            if (Path.IsPathRooted(subPath))
                return subPath;

            // Combine the paths
            string combinedPath = Path.Combine(basePath, subPath);

            // Normalize the path separators for the current OS
            combinedPath = Path.GetFullPath(combinedPath);

            return combinedPath;
        }
        public static void CheckForMethod(string line, ref string curMethod)
        {
            // ex:    addPet(body: Pet | undefined) {
            // ex:    addPet(body: Pet | undefined , cancelToken?: CancelToken | undefined): Promise<Pet> {
            // return addPet
            var result = Regex.Match(line, @"^    (\w+)\([^\)]+\)[^{]+{");
            if (result.Success)
                curMethod = MakeMethodMapName(result.Groups[1].Value);
        }
        public static string MakeMethodMapName(string method)
        {
            return method.Substring(0, 1).ToUpper() + method.Substring(1) + "Async";
        }
        public static CompilationUnitSyntax RemoveClass(CompilationUnitSyntax root, string className)
        {
            var classDecls = root
                .DescendantNodes().OfType<NamespaceDeclarationSyntax>()
                .First()
                    ?.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Where(x => x.Identifier.ValueText.Equals(className))
                    .ToList(); // ex: OrderController

            root = root.ReplaceNode(root,
                root.RemoveNodes(classDecls, SyntaxRemoveOptions.KeepNoTrivia));

            return root;
        }
        public static CompilationUnitSyntax RemoveLambdaEndpointsMethods(List<string> endpoints, CompilationUnitSyntax root)
        {
            foreach (var endpoint in endpoints)
            {
                var methodName = endpoint + "Async";
                var methodsToRemove = root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(method => method.Identifier.ValueText.Equals(methodName))
                    .ToList();

                if (methodsToRemove != null)
                    root = root.RemoveNodes(methodsToRemove, SyntaxRemoveOptions.KeepNoTrivia);
            }
            return root;
        }
        public static ClassDeclarationSyntax AddInterfaceToClass(ClassDeclarationSyntax classDeclaration, string interfaceName)
        {
            // Create a new SimpleBaseTypeSyntax for the interface
            var interfaceType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(interfaceName));

            // Get the existing base type list or create a new one if it doesn't exist
            var baseList = classDeclaration.BaseList ?? SyntaxFactory.BaseList();

            // Add the new interface to the base type list
            BaseListSyntax newBaseList;
            if (baseList.Types.Count == 0)
            {
                // If there are no existing base types, just add the new interface
                newBaseList = baseList.AddTypes(interfaceType);
            }
            else
            {
                // If there are existing base types, add a comma and space before the new interface
                var lastType = baseList.Types.Last();
                var separatedTypes = baseList.Types.Replace(lastType,
                    lastType.WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(", ")));
                newBaseList = baseList.WithTypes(separatedTypes.Add(interfaceType));
            }

            // Create a new ClassDeclarationSyntax with the updated base list
            return classDeclaration.WithBaseList(newBaseList);
        }
        public static CompilationUnitSyntax RemoveGeneratedSchemaClasses(CompilationUnitSyntax root, List<string> namedClasses = null)
        {
            var classDecls = root
                .DescendantNodes().OfType<NamespaceDeclarationSyntax>()
                .First()
                    ?.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    //.Where(x => x.AttributeLists.First().ToString().StartsWith(@"[System.CodeDom.Compiler.GeneratedCode(""NJsonSchema"))
                    .Where(x => x.AttributeLists.Any(y => y.ToString().StartsWith(@"[System.CodeDom.Compiler.GeneratedCode(""NJsonSchema")))
                    .ToList(); // ex: OrderController

            root = root.ReplaceNode(root,
                root.RemoveNodes(classDecls, SyntaxRemoveOptions.KeepNoTrivia));

            var enumDecls = root
                .DescendantNodes().OfType<NamespaceDeclarationSyntax>()
                .First()
                    ?.DescendantNodes().OfType<EnumDeclarationSyntax>()
                    .ToList(); // ex: OrderController

            root = root.ReplaceNode(root,
                root.RemoveNodes(enumDecls, SyntaxRemoveOptions.KeepNoTrivia));


            // Remove any classes in the namedClasses list
            if (namedClasses != null)
            {
                classDecls = root
                    .DescendantNodes().OfType<NamespaceDeclarationSyntax>()
                    .First()
                        ?.DescendantNodes().OfType<ClassDeclarationSyntax>()
                        .Where(x => namedClasses.Contains(x.Identifier.ValueText.ToString()))
                        .ToList();
                root = root.ReplaceNode(root,
                    root.RemoveNodes(classDecls, SyntaxRemoveOptions.KeepNoTrivia));
            }
            return root;


        }
        public static void GenerateGlobalUsingsFile(List<string> usings, string filePath)
        {
            var usingsCode = "";
            foreach (var usingName in usings)
                usingsCode += $"global using {usingName};\r\n";
            var usingsFileContent = $@"
//----------------------
// <auto-generated>
//     Generated by LazyMagic. Do not modify, your changes will be overwritten.
// </auto-generated>
//----------------------
{usingsCode}
";
            File.WriteAllText(filePath, usingsFileContent);
        }
        public static void GeneratePackagesPropsFile(List<string> packageReferences, string filePath)
        {
            var packagePropsCode = "";
            foreach (var packageRef in packageReferences)
                packagePropsCode += Path.IsPathRooted(packageRef)
                    ? $"<PackageReference Include=\"{packageRef}\" />\r\n"
                    : $"<PackageReference Include=\"$(SolutionDir){packageRef}\" />\r\n";

            var propsfilecontent = $@"
<Project>
   <!-- This file is generated by LazyMagic. Do not modify, your changes will be overwritten. -->
    <ItemGroup>
        {packagePropsCode}
    </ItemGroup>
</Project>";
            File.WriteAllText(filePath, propsfilecontent);
        }
        public static void GenerateProjectsPropsFile(List<string> projectReferences, string filePath)
        {
            var projectPropsCode = "";
            foreach (var projectRef in projectReferences)
                projectPropsCode += Path.IsPathRooted(projectRef)
                    ? $"<ProjectReference Include=\"{projectRef}\" />\r\n"
                    : $"<ProjectReference Include=\"$(SolutionDir){projectRef}\" />\r\n";

            var propsfilecontent = $@"
<Project>
   <!-- This file is generated by LazyMagic. Do not modify, your changes will be overwritten. -->
    <ItemGroup>
        {projectPropsCode}
    </ItemGroup>
</Project>";
            File.WriteAllText(filePath, propsfilecontent);
        }
        public static void GenerateGlobalUsingFile(List<string> usings, string content, string filePath)
        {
            var usingsCode = content;
            foreach (var usingName in usings)
                usingsCode += $"global using {usingName};\r\n";
            File.AppendAllText(filePath, usingsCode);
        }   
        public static void GenerateLicenseFile(string licenseText, string filePath)
        {
            File.WriteAllText(filePath, licenseText);
        }   
        public static void GenerateUserPropsFile(string userPropsText, string filePath)
        {
            if(string.IsNullOrEmpty(userPropsText))
                userPropsText = "<Project></Project>";  
            File.WriteAllText(filePath, userPropsText);
        }   
        public static List<string> GetExportedPackageReferences(List<ArtifactBase> artifacts)
        {
            var packages = new List<string>();
            foreach (DotNetProjectBase artifact in artifacts.Where(x => x is DotNetProjectBase))
                if (!string.IsNullOrEmpty(artifact.ExportedPackage))
                    packages.Add(artifact.ExportedPackage);
            return packages.Distinct().ToList();
        }
        public static List<string> GetExportedProjectReferences(List<ArtifactBase> artifacts)
        {
            var references = new List<string>();
            foreach (DotNetProjectBase artifact in artifacts.Where(x => x is DotNetProjectBase))
                if (!string.IsNullOrEmpty(artifact.ExportedProjectPath))
                    references.Add(artifact.ExportedProjectPath);
            return references.Distinct().ToList();
        }
        public static List<string> GetExportedGlobalUsings(List<ArtifactBase> artifacts)
        {
            var usings = new List<string>();
            foreach (DotNetProjectBase artifact in artifacts.Where(x => x is DotNetProjectBase))
                if (artifact.ExportedGlobalUsings != null)
                    usings.AddRange(artifact.ExportedGlobalUsings);
            return usings.Distinct().ToList();  
        }
        public static List<string> GetExportedServiceRegistrations(List<ArtifactBase> artifacts)
        {
            var serviceRegistrations = new List<string>();
            foreach (DotNetProjectBase artifact in artifacts.Where(x => x is DotNetProjectBase))
                if (artifact.ExportedServiceRegistrations != null)
                    serviceRegistrations.AddRange(artifact.ExportedServiceRegistrations);
            return serviceRegistrations.Distinct().ToList();
        }
        public static List<string> GetExportedInterfaces(List<ArtifactBase> artifacts)
        {
            var interfaces = new List<string>();
            foreach (DotNetProjectBase artifact in artifacts.Where(x => x is DotNetProjectBase))
                if (artifact.ExportedInterfaces != null)
                    interfaces.AddRange(artifact.ExportedInterfaces);
            return interfaces.Distinct().ToList();
        }
        public static List<string> GetExportedOpenApiSpecs(List<ArtifactBase> artifacts)
        {
            var specs = new List<string>();
            foreach (DotNetProjectBase artifact in artifacts.Where(x => x is DotNetProjectBase))
                if (artifact.ExportedOpenApiSpecs != null)
                    specs.AddRange(artifact.ExportedOpenApiSpecs);
            return specs.Distinct().ToList();   
        }
        public static string ReplaceLineEndings(string str)
        {
            // Using stringbuilder
            var sb = new StringBuilder(str.Length + 1000);
            using (StreamReader sr = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(str))))
            {
                while (!sr.EndOfStream)
                    sb.AppendLine(sr.ReadLine());
            }
            return sb.ToString();
        }
    }
}