using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

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
            var allProjects = Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories)
                                       .Select(p => Path.GetFullPath(p))
                                       .ToList();

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
                var relativePath = Path.GetRelativePath(Path.GetDirectoryName(_solutionPath), project);
                ExecuteDotnetCommand("sln", $"\"{_solutionPath}\" add \"{relativePath}\"");
            }

            Console.WriteLine($"Added {projectsToAdd.Count} projects to the solution.");
        }
        catch (Exception ex)
        {
            throw new SolutionModificationException("Error adding projects to the solution.", ex);
        }
    }

    private string ExecuteDotnetCommand(string command, string arguments)
    {
        using (var process = new Process())
        {
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = $"{command} {arguments}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"dotnet command failed. Error: {error}");
            }

            return output;
        }
    }
}

// Custom exceptions (same as before)
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