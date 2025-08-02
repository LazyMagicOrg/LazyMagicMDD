using System;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using CommandLine;
using LazyMagic;

//using Microsoft.Build.Locator;

namespace LazyMagicApp
{
    public class Logger : ILogger
    {
        public void Info(string message)
        { 
            Console.WriteLine(message);
        }

        public async Task InfoAsync(string message)
        {
            await Task.Delay(0);
            Console.WriteLine(message);
        }

        public void Error(Exception ex, string message)
        {
            Console.WriteLine(message);
            Console.WriteLine(ex.Message);
        }

        public async Task ErrorAsync(Exception ex, string message)
        {
            await Task.Delay(0);
            Console.WriteLine(message);
            Console.WriteLine(ex.Message);
        }
    }

    class Program
    {
        // https://github.com/commandlineparser/commandline
        [Verb("projects", isDefault: true, HelpText = "Generate/Update project files")]
        public class ProjectsOptions
        {

            [Value(0, Required = false, HelpText = "Specify solution file")]
            public string SolutionFilePath { get; set; }
        }

        static async Task<int> Main(string[] args)
        {
           
            return await CommandLine.Parser.Default.ParseArguments<ProjectsOptions>(args)
                .MapResult(
                    async (ProjectsOptions opts) => await RunProjects(opts),
                    errs => Task.FromResult(1)
                 );
        }
         
        /// <summary>
        /// Generate/Update projects
        /// </summary>
        /// <param name="projectsOptions"></param>
        /// <returns></returns>
        public static async Task<int> RunProjects(ProjectsOptions projectsOptions)
        {
            var logger = new Logger();
            try
            {
                projectsOptions.SolutionFilePath ??= Directory.GetCurrentDirectory();
                
                var lzSolution = new LzSolution(logger, projectsOptions.SolutionFilePath);

                await lzSolution.ProcessAsync();
                await AddProjectsToSolutionAsync(projectsOptions.SolutionFilePath);  

                var solutionProjectAdder = new LazyMagic.SolutionProjectAdder(projectsOptions.SolutionFilePath);
                solutionProjectAdder.AddMissingProjects();

                //// Get current projects using "dotnet sln <slnFilePath> list
                //var startInfo = new ProcessStartInfo();
                //startInfo.FileName = "dotnet";
                //startInfo.UseShellExecute = false;
                //startInfo.RedirectStandardOutput = true;
                //startInfo.CreateNoWindow = true;
                //startInfo.Arguments = $" sln {solutionModel.SolutionFilePath} list";
                //using var process = Process.Start(startInfo);
                //var existingProjects = process.StandardOutput.ReadToEnd();
                //Console.WriteLine($"Existing Projects\n{existingProjects}");

                //var projectsToAdd = string.Empty;
                //foreach (KeyValuePair<string, ProjectFileInfo> proj in solutionModel.Projects)
                //    if (!existingProjects.Contains($"{proj.Value.RelativePath}"))
                //    {
                //        logger.Info($"Adding Project {proj.Value.RelativePath} to solution");
                //        projectsToAdd += $" {proj.Value.RelativePath}";
                //    }
                //if (!string.IsNullOrEmpty(projectsToAdd))
                //{
                //    startInfo.Arguments = $"sln {solutionModel.SolutionFilePath} add {projectsToAdd}";
                //    using var addProcess = Process.Start(startInfo);
                //    var addProjectOutput = addProcess.StandardOutput.ReadToEnd();
                //    Console.WriteLine($"{addProjectOutput}");
                //}

                    // Note!
                    // Unlike the VisualStudio processing, we do not have the ability to 
                    // create a Solution Items folder and add items to it when using 
                    // the dotnet CLI. If someone is working with the CLI, it is not clear
                    // if a Solution Items folder would add any value. OTOH, if a solution
                    // generated with Visual Studio and which is later processed using 
                    // the dotnet CLI, nothing bad happens. So, no harm no foul.
            }
            catch (Exception e)
            {
                logger.Error(e, e.Message);
                return -1;
            }
            Console.WriteLine("Projects Generation Complete");
            return 0;
        }
        private static async Task AddProjectsToSolutionAsync(string solutionFilePath)
        {
            await Task.Delay(0);    
        }
    }
}
