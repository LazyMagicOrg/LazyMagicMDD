using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

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

        /// <summary>
        /// Fixes projects that exist in the solution but are in the wrong solution folder.
        /// </summary>
        public void FixMisplacedProjects()
        {
            try
            {
                var misplacedProjects = FindMisplacedProjects();
                if (misplacedProjects.Count == 0)
                {
                    Console.WriteLine("No misplaced projects found.");
                    return;
                }

                MoveProjectsToCorrectFolders(misplacedProjects);
                Console.WriteLine($"Fixed {misplacedProjects.Count} misplaced projects.");
            }
            catch (Exception ex)
            {
                throw new SolutionModificationException("An error occurred while fixing misplaced projects.", ex);
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
        /// <returns>Dictionary mapping project file paths to their solution folder names</returns>
        public Dictionary<string, string> GetMissingProjects()
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
                var output = ExecuteDotnetCommand("sln", $"{_solutionPath} list");
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

        private Dictionary<string, string> FindMissingProjects()
        {
            try
            {
                var solutionDir = Path.GetDirectoryName(_solutionPath);
                var allProjects = GetProjectsInLazyMagicFolders(solutionDir);

                // Filter out projects that already exist in the solution
                var missingProjects = allProjects
                    .Where(kvp => !_existingProjects.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                return missingProjects;
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

        private Dictionary<string, string> GetProjectsInLazyMagicFolders(string solutionDir)
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

            // Dictionary maps project path to solution folder name
            var allProjects = new Dictionary<string, string>();

            foreach (var folder in lazyMagicFolders)
            {
                var folderPath = Path.Combine(solutionDir, folder);
                if (Directory.Exists(folderPath))
                {
                    var projectsInFolder = Directory.GetFiles(folderPath, "*.csproj", SearchOption.AllDirectories)
                                                   .Select(p => Path.GetFullPath(p))
                                                   .ToList();
                    foreach (var project in projectsInFolder)
                    {
                        allProjects[project] = folder;
                    }
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

        private void AddProjectsToSolution(Dictionary<string, string> projectsToAdd)
        {
            if (projectsToAdd == null || projectsToAdd.Count == 0)
            {
                Console.WriteLine("No new projects to add.");
                return;
            }

            try
            {
                foreach (var kvp in projectsToAdd)
                {
                    var projectPath = kvp.Key;
                    var solutionFolder = kvp.Value;
                    var relativePath = GetRelativePath(Path.GetDirectoryName(_solutionPath), projectPath);
                    ExecuteDotnetCommand("sln", $"{_solutionPath} add {relativePath} --solution-folder {solutionFolder}");
                }

                Console.WriteLine($"Added {projectsToAdd.Count} projects to the solution.");
            }
            catch (Exception ex)
            {
                throw new SolutionModificationException("Error adding projects to the solution.", ex);
            }
        }

        /// <summary>
        /// Finds projects that are in the solution but in the wrong solution folder.
        /// </summary>
        /// <returns>Dictionary mapping project paths to their expected solution folder names</returns>
        private Dictionary<string, string> FindMisplacedProjects()
        {
            var solutionDir = Path.GetDirectoryName(_solutionPath);
            var expectedFolders = GetProjectsInLazyMagicFolders(solutionDir);
            var currentFolders = ParseSolutionProjectFolders();
            var misplacedProjects = new Dictionary<string, string>();

            foreach (var kvp in expectedFolders)
            {
                var projectPath = kvp.Key;
                var expectedFolder = kvp.Value;

                if (currentFolders.TryGetValue(projectPath, out var currentFolder))
                {
                    // Project exists in solution - check if it's in the correct folder
                    if (!string.Equals(currentFolder, expectedFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        misplacedProjects[projectPath] = expectedFolder;
                    }
                }
            }

            return misplacedProjects;
        }

        /// <summary>
        /// Parses the solution file to extract project-to-folder mappings.
        /// </summary>
        /// <returns>Dictionary mapping project paths to their current solution folder names (empty string if at root)</returns>
        private Dictionary<string, string> ParseSolutionProjectFolders()
        {
            var solutionContent = File.ReadAllText(_solutionPath);
            var solutionDir = Path.GetDirectoryName(_solutionPath);
            var projectToFolder = new Dictionary<string, string>();

            // Parse all projects and folders with their GUIDs
            // Project("{GUID}") = "Name", "Path", "{ProjectGUID}"
            var projectPattern = new Regex(
                @"Project\(""\{([^}]+)\}""\)\s*=\s*""([^""]+)"",\s*""([^""]+)"",\s*""\{([^}]+)\}""",
                RegexOptions.Multiline);

            var folderTypeGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8"; // Solution Folder type GUID
            var projectGuids = new Dictionary<string, string>(); // Maps project GUID to project path
            var folderGuids = new Dictionary<string, string>(); // Maps folder GUID to folder name

            foreach (Match match in projectPattern.Matches(solutionContent))
            {
                var typeGuid = match.Groups[1].Value.ToUpperInvariant();
                var name = match.Groups[2].Value;
                var path = match.Groups[3].Value;
                var projectGuid = match.Groups[4].Value.ToUpperInvariant();

                if (typeGuid == folderTypeGuid)
                {
                    // This is a solution folder
                    folderGuids[projectGuid] = name;
                }
                else if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    // This is a project - normalize path separators for cross-platform compatibility
                    var normalizedPath = path.Replace('\\', Path.DirectorySeparatorChar);
                    var fullPath = Path.GetFullPath(Path.Combine(solutionDir, normalizedPath));
                    projectGuids[projectGuid] = fullPath;
                    projectToFolder[fullPath] = ""; // Default to root (no folder)
                }
            }

            // Parse NestedProjects section to find folder assignments
            // {ProjectGUID} = {FolderGUID}
            var nestedPattern = new Regex(
                @"\{([^}]+)\}\s*=\s*\{([^}]+)\}",
                RegexOptions.Multiline);

            var nestedSectionMatch = Regex.Match(solutionContent, 
                @"GlobalSection\(NestedProjects\)\s*=\s*preSolution(.*?)EndGlobalSection",
                RegexOptions.Singleline);

            if (nestedSectionMatch.Success)
            {
                var nestedSection = nestedSectionMatch.Groups[1].Value;
                foreach (Match match in nestedPattern.Matches(nestedSection))
                {
                    var childGuid = match.Groups[1].Value.ToUpperInvariant();
                    var parentGuid = match.Groups[2].Value.ToUpperInvariant();

                    // If the child is a project and parent is a folder, record the mapping
                    if (projectGuids.TryGetValue(childGuid, out var projectPath) &&
                        folderGuids.TryGetValue(parentGuid, out var folderName))
                    {
                        projectToFolder[projectPath] = folderName;
                    }
                }
            }

            return projectToFolder;
        }

        /// <summary>
        /// Moves projects to their correct solution folders by removing and re-adding them.
        /// </summary>
        private void MoveProjectsToCorrectFolders(Dictionary<string, string> projectsToMove)
        {
            var solutionDir = Path.GetDirectoryName(_solutionPath);

            foreach (var kvp in projectsToMove)
            {
                var projectPath = kvp.Key;
                var targetFolder = kvp.Value;
                var relativePath = GetRelativePath(solutionDir, projectPath);

                // Remove the project from the solution
                ExecuteDotnetCommand("sln", $"{_solutionPath} remove {relativePath}");

                // Re-add it to the correct folder
                ExecuteDotnetCommand("sln", $"{_solutionPath} add {relativePath} --solution-folder {targetFolder}");

                Console.WriteLine($"Moved {Path.GetFileName(projectPath)} to {targetFolder} folder.");
            }
        }

        /// <summary>
        /// Helper method to get relative path (replacement for Path.GetRelativePath which is not available in .NET Standard 2.0)
        /// </summary>
        private string GetRelativePath(string basePath, string fullPath)
        {
            var separator = Path.DirectorySeparatorChar;
            var baseUri = new Uri(basePath.EndsWith(separator.ToString()) ? basePath : basePath + separator);
            var fullUri = new Uri(fullPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', separator));
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
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_solutionPath)
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