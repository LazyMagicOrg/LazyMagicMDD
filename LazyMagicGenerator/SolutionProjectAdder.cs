using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace LazyMagic
{
    public class SolutionProjectAdder
    {
        private string _solutionPath;
        private List<string> _existingProjects;

        public SolutionProjectAdder(string solutionPath)
        {
            if (string.IsNullOrWhiteSpace(solutionPath))
                throw new ArgumentNullException(nameof(solutionPath), "Solution path cannot be null or empty.");

            if (!File.Exists(solutionPath))
                throw new FileNotFoundException("Specified solution file does not exist.", solutionPath);

            _solutionPath = solutionPath;
            _existingProjects = new List<string>();
        }

        public void AddMissingProjects()
        {
            try
            {
                GetExistingProjects();
                var missingProjects = FindMissingProjects();
                AddProjectsToSolution(missingProjects);
            }
            catch (Exception ex)
            {
                throw new SolutionModificationException("An error occurred while adding missing projects.", ex);
            }
        }

        public void AddSolutionItems()
        {
            try
            {
                var solutionDir = Path.GetDirectoryName(_solutionPath);
                var (solutionItems, openApiFiles) = GetCategorizedFiles(solutionDir);

                if (solutionItems.Count > 0)
                {
                    AddFilesToSolutionFolder("Solution Items", solutionItems);
                    Console.WriteLine($"Added {solutionItems.Count} files to Solution Items folder.");
                }

                if (openApiFiles.Count > 0)
                {
                    AddFilesToSolutionFolder("OpenAPI", openApiFiles);
                    Console.WriteLine($"Added {openApiFiles.Count} files to OpenAPI folder.");
                }

                if (solutionItems.Count == 0 && openApiFiles.Count == 0)
                {
                    Console.WriteLine("No solution item files to add.");
                }
            }
            catch (Exception ex)
            {
                throw new SolutionModificationException("An error occurred while adding solution items.", ex);
            }
        }

        /// <summary>
        /// Gets all LazyMagic projects in the solution directory that are not currently in the solution.
        /// This method can be used by the Visual Studio extension for folder-based discovery.
        /// </summary>
        /// <returns>List of project file paths that need to be added to the solution</returns>
        public List<string> GetMissingProjects()
        {
            try
            {
                GetExistingProjects();
                return FindMissingProjects();
            }
            catch (Exception ex)
            {
                throw new SolutionModificationException("An error occurred while finding missing projects.", ex);
            }
        }

        private void GetExistingProjects()
        {
            try
            {
                var output = ExecuteDotnetCommand("sln", $"\"{_solutionPath}\" list");
                _existingProjects = output
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .Skip(2) // Skip the first two lines (header)
                    .Select(p => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_solutionPath), p.Trim())))
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new SolutionParsingException("Error getting existing projects from the solution.", ex);
            }
        }

        private List<string> FindMissingProjects()
        {
            try
            {
                var solutionDir = Path.GetDirectoryName(_solutionPath);
                var allProjects = GetProjectsInLazyMagicFolders(solutionDir);

                return allProjects.Except(_existingProjects).ToList();
            }
            catch (IOException ex)
            {
                throw new ProjectScanException("Error scanning for project files.", ex);
            }
            catch (Exception ex)
            {
                throw new ProjectScanException("Unexpected error while scanning for projects.", ex);
            }
        }

        private List<string> GetProjectsInLazyMagicFolders(string solutionDir)
        {
            // Define LazyMagic output folders to scan
            var lazyMagicFolders = new[] 
            { 
                "Schemas", 
                "Modules", 
                "ClientSDKs", 
                "Containers", 
                "APIs",
                "Services"
            };

            var allProjects = new List<string>();

            foreach (var folder in lazyMagicFolders)
            {
                var folderPath = Path.Combine(solutionDir, folder);
                if (Directory.Exists(folderPath))
                {
                    var projectsInFolder = Directory.GetFiles(folderPath, "*.csproj", SearchOption.AllDirectories)
                                                   .Select(p => Path.GetFullPath(p))
                                                   .ToList();
                    allProjects.AddRange(projectsInFolder);
                }
            }

            return allProjects;
        }

        private (List<string> solutionItems, List<string> openApiFiles) GetCategorizedFiles(string solutionDir)
        {
            var solutionItems = new List<string>();
            var openApiFiles = new List<string>();

            // Get all files in the solution directory (excluding subdirectories)
            var allFiles = Directory.GetFiles(solutionDir, "*", SearchOption.TopDirectoryOnly);

            foreach (var file in allFiles)
            {
                var fileName = Path.GetFileName(file);
                var extension = Path.GetExtension(file).ToLowerInvariant();

                // Skip the solution file itself
                if (extension == ".sln")
                    continue;

                // Check if it's an OpenAPI file
                if (fileName.StartsWith("openapi.", StringComparison.OrdinalIgnoreCase) &&
                    (extension == ".yaml" || extension == ".yml" || extension == ".json"))
                {
                    openApiFiles.Add(file);
                    continue;
                }

                // Include common configuration and documentation files
                if (extension == ".yaml" || extension == ".yml" ||
                    extension == ".json" || extension == ".ps1" ||
                    extension == ".md" || extension == ".txt" ||
                    extension == ".targets" || extension == ".props" ||
                    fileName.Equals("README", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals(".gitignore", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase))
                {
                    solutionItems.Add(file);
                }
            }

            return (solutionItems, openApiFiles);
        }

        private void AddFilesToSolutionFolder(string folderName, List<string> filesToAdd)
        {
            try
            {
                // Read the solution file
                var solutionContent = File.ReadAllText(_solutionPath);
                var solutionDir = Path.GetDirectoryName(_solutionPath);

                // Generate a GUID for the folder
                var folderTypeGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}"; // Solution Folder type GUID

                // Check if the folder already exists
                var folderPattern = $"\") = \"{folderName}\", \"{folderName}\", \"";
                var folderExists = solutionContent.Contains(folderPattern);

                if (!folderExists)
                {
                    // Folder doesn't exist - create it with all files
                    var folderGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
                    var projectsSection = "Global\r\n\tGlobalSection(SolutionConfigurationPlatforms)";

                    // Add all files to the ProjectSection
                    var projectSectionContent = "\tProjectSection(SolutionItems) = preProject\r\n";
                    foreach (var file in filesToAdd)
                    {
                        var relativePath = GetRelativePath(solutionDir, file);
                        projectSectionContent += $"\t\t{relativePath} = {relativePath}\r\n";
                    }
                    projectSectionContent += "\tEndProjectSection\r\n";

                    var solutionFolder = $"Project(\"{folderTypeGuid}\") = \"{folderName}\", \"{folderName}\", \"{folderGuid}\"\r\n{projectSectionContent}EndProject\r\n";

                    solutionContent = solutionContent.Replace(projectsSection, solutionFolder + projectsSection);

                    // Write back to the solution file
                    File.WriteAllText(_solutionPath, solutionContent);
                }
                else
                {
                    // Folder exists - add only new files to existing folder
                    // Find the folder's ProjectSection
                    var folderStartPattern = $"Project(\"{folderTypeGuid}\") = \"{folderName}\", \"{folderName}\",";
                    var folderStartIndex = solutionContent.IndexOf(folderStartPattern);

                    if (folderStartIndex == -1)
                        throw new Exception($"Could not find folder '{folderName}' in solution file.");

                    // Find the ProjectSection for this folder
                    var projectSectionStart = solutionContent.IndexOf("ProjectSection(SolutionItems) = preProject", folderStartIndex);
                    var projectSectionEnd = solutionContent.IndexOf("EndProjectSection", projectSectionStart);

                    if (projectSectionStart == -1 || projectSectionEnd == -1)
                        throw new Exception($"Could not find ProjectSection for folder '{folderName}'.");

                    // Get existing files in the section
                    var existingSection = solutionContent.Substring(projectSectionStart, projectSectionEnd - projectSectionStart);

                    // Build list of new files to add (only those not already in the section)
                    var newFilesContent = "";
                    foreach (var file in filesToAdd)
                    {
                        var relativePath = GetRelativePath(solutionDir, file);
                        if (!existingSection.Contains(relativePath))
                        {
                            newFilesContent += $"\t\t{relativePath} = {relativePath}\r\n";
                        }
                    }

                    // If there are new files, add them before EndProjectSection
                    if (!string.IsNullOrEmpty(newFilesContent))
                    {
                        solutionContent = solutionContent.Insert(projectSectionEnd, newFilesContent);
                        File.WriteAllText(_solutionPath, solutionContent);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new SolutionModificationException($"Error adding files to {folderName} folder in the solution file.", ex);
            }
        }

        private void AddProjectsToSolution(List<string> projectsToAdd)
        {
            if (projectsToAdd == null || projectsToAdd.Count == 0)
            {
                Console.WriteLine("No new projects to add.");
                return;
            }

            try
            {
                foreach (var project in projectsToAdd)
                {
                    var relativePath = GetRelativePath(Path.GetDirectoryName(_solutionPath), project);
                    ExecuteDotnetCommand("sln", $"\"{_solutionPath}\" add \"{relativePath}\"");
                }

                Console.WriteLine($"Added {projectsToAdd.Count} projects to the solution.");
            }
            catch (Exception ex)
            {
                throw new SolutionModificationException("Error adding projects to the solution.", ex);
            }
        }

        /// <summary>
        /// Helper method to get relative path (replacement for Path.GetRelativePath which is not available in .NET Standard 2.0)
        /// </summary>
        private string GetRelativePath(string basePath, string fullPath)
        {
            var baseUri = new Uri(basePath.EndsWith("\\") ? basePath : basePath + "\\");
            var fullUri = new Uri(fullPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', '\\'));
        }

        private string ExecuteDotnetCommand(string command, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"{command} {arguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"dotnet command failed: {error}");
                    }

                    return output;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to execute dotnet command: {ex.Message}", ex);
            }
        }
    }

    public class SolutionModificationException : Exception
    {
        public SolutionModificationException(string message) : base(message) { }
        public SolutionModificationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class SolutionParsingException : Exception
    {
        public SolutionParsingException(string message) : base(message) { }
        public SolutionParsingException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ProjectScanException : Exception
    {
        public ProjectScanException(string message) : base(message) { }
        public ProjectScanException(string message, Exception innerException) : base(message, innerException) { }
    }
}