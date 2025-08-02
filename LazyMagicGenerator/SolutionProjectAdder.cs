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