using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using EnvDTE;
using EnvDTE80;
using EnvDTE100;
using LazyMagic;
using VSLangProj;
using System.Windows;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LazyMagicVsExt
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class LazyMagic_Generate_Projects
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("74dda08f-2bee-4ed4-97b0-1405b9fbbb16");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private readonly DTE dte;

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyMagic_Generate_Projects"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private LazyMagic_Generate_Projects(AsyncPackage package, DTE dte, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            this.dte = dte ?? throw new ArgumentNullException(nameof(dte));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static LazyMagic_Generate_Projects Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in LazyMagic___Generate_Projects's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            var dte = await package.GetServiceAsync(typeof(DTE)) as DTE;
            Instance = new LazyMagic_Generate_Projects(package, dte, commandService);

        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Do the work in this handler async
            _ = this.package.JoinableTaskFactory.RunAsync((Func<Task>)async delegate
            {
                var solutionFullName = dte?.Solution?.FullName;
                if (string.IsNullOrEmpty(solutionFullName))
                {
                    MessageBox.Show("Sorry - no solution is open.");
                    return;
                }

                ToolWindowPane window = await this.package.ShowToolWindowAsync(typeof(LazyMagicLogToolWindow), 0, true, this.package.DisposalToken);
                if ((null == window) || (null == window.Frame))
                    throw new NotSupportedException("Cannot create tool window");

                var userControl = window.Content as LazyMagicLogToolWindowControl;
                userControl.LogEntries.Clear();

                // Progress class
                // Any handler provided to the constructor or event handlers registered with the 
                // ProgressChanged event are invoked through a SynchronizationContext instance captured 
                // when the instance is constructed. If there is no current SynchronizationContext 
                // at the time of construction, the callbacks will be invoked on the ThreadPool.
                // Practical Effect; Progress allows us to avoid wiring up events to handle logging entries
                // made by CPU bound tasks executed with await Task.Run(...)
                var progress = new Progress<LogEntry>(l => userControl.LogEntries.Add(l));
                var logger = new Logger(progress); // ie Logger.Info(msg) calls progress.Report(logEntry).

                try
                {
                    var solutionRootFolderPath = Path.GetDirectoryName(solutionFullName);
                    var lzSolution = new LzSolution(logger, solutionRootFolderPath);
                    await lzSolution.ProcessAsync();

                    // Avoid unnecessary warnings on access to dte etc.
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
                    var solution = dte.Solution as Solution4;

                    // Solution Items folder
                    var projectPath = new List<string> { "Solution Items" };
                    Project solutionItemsProject = GetProject(projectPath);
                    if (solutionItemsProject == null)
                        solutionItemsProject = solution.AddSolutionFolder("Solution Items");

                    var solutionFiles = Directory.GetFiles(solutionRootFolderPath);
                    foreach ( var file in solutionFiles)
                        if( file.EndsWith(".yaml") || file.EndsWith(".json") || file.EndsWith(".ps1"))
                            AddFileToProject(solutionItemsProject, Path.Combine(solutionRootFolderPath, file));

                    AddProjects<DotNetProjectBase>(solution, lzSolution.Directives, solutionRootFolderPath);

                    await logger.InfoAsync("LazyMagic processing complete");

                }
                catch (Exception ex)
                {
                    await logger.ErrorAsync(ex, "LazyMagic Encountered an Error");
                }
            });
        }

        private void AddProjects<T>(Solution4 solution, Directives directives, string solutionRootFolderPath)
            where T : DotNetProjectBase
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (var directive in directives.Values.Where(x => !x.IsDefault))
            {
                foreach (var artifact in directive.Artifacts.Values.OfType<T>())
                {
                    try
                    {
                        if (string.IsNullOrEmpty(artifact.OutputFolder))
                        {
                            // Add project to the root of the solution
                            AddProjectToSolutionRoot(solution, solutionRootFolderPath, artifact.ProjectFilePath);
                        }
                        else
                        {
                            // Create Solution folder (OutputFolder) if necessary
                            var solutionFolderName = artifact.OutputFolder;
                            Project solutionFolderProject = GetProject(new List<string> { solutionFolderName });
                            if (solutionFolderProject == null && !string.IsNullOrEmpty(artifact.OutputFolder))
                                solutionFolderProject = solution.AddSolutionFolder(artifact.OutputFolder);

                            // Add the project to the solution folder
                            AddProjectToSolutionFolder(
                                solutionFolderProject,
                                solutionRootFolderPath,
                                Path.Combine(solutionRootFolderPath,
                                artifact.ProjectFilePath)
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
            }
        }

        private void AddProjectToSolutionRoot(Solution4 solution, string solutionRootFolderPath, string projectFilePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var fullProjectPath = Path.Combine(solutionRootFolderPath, projectFilePath);
            if (File.Exists(fullProjectPath))
            {
                var projectName = Path.GetFileNameWithoutExtension(fullProjectPath);
                var existingProject = solution.Projects.Cast<Project>().FirstOrDefault(p => p.Name == projectName);

                if (existingProject == null)
                {
                    solution.AddFromFile(fullProjectPath, false);
                }
            }
            else
            {
                throw new Exception($"Project file not found: {fullProjectPath}");  
            }
        }

        private void AddProjectToSolutionFolder(Project solutionFolderProject, string solutionFolderPath, string folderName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (string.IsNullOrEmpty(folderName)) return;
                var fullFileName = Path.Combine(solutionFolderPath, folderName);
                var projectName = Path.GetFileNameWithoutExtension(fullFileName);
                var found = false;
                foreach (ProjectItem projectItem in solutionFolderProject.ProjectItems)
                    if (projectItem.FileCount > 0)
                        if (found = projectItem.Name.Equals(projectName))
                            break;
                if (!found)
                {
                    // TODO: error? fullFileName is Service2 which is not a project folder?
                    ((SolutionFolder)solutionFolderProject.Object).AddFromFile(fullFileName);
                }
            } catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }


        //Notes on Project, Artifacts, ProjectItems, SolutionFolders etc
        // Solution
        //   Artifacts -- Docs say all solutionFolderProject are shown, however, only first level projects are!
        //     Project
        //       ProjectItems
        //         ProjectItem
        //           if projectItem.SubProject then use SubProject to drill down -- Note SolutionFolders are SubProjects!
        //           SubProject is not null even if there are no items in the SubProject
        // SolutionFolder is a type of Project eg. solutionFolderProject.Kind == ProjectKinds.vsProjectKindSolutionFolder
        // solutionFolder = (SolutionFolder)solutionFolderProject.Object
        // solutionFolder.AddFromFile(..)
        // solutionFolderProject.ProjectItems.AddFromFile(..)
        // projectItem.FileNames[] has an ordinal starting at 1 instead of 0


        // Find a solutionFolderProject based on supplied path
        private Project GetProject(List<string> path, Project subProject = null, int level = 0)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (level == 0)
            {
                var solution = dte?.Solution as Solution4;
                var projects = solution?.Projects;
                // Got through top level - recurse into solutionFolderProject if found
                foreach (Project project in projects)
                    if (project.Name.Equals(path[level]))
                        return (level == path.Count - 1)
                            ? project
                            : GetProject(path, project, ++level);
            }
            else
            {
                foreach (ProjectItem projectItem in subProject.ProjectItems)
                    if (projectItem.Name.Equals(path[level]))
                        if (projectItem.SubProject != null) // also pick's up SolutionFolders
                            return (level == path.Count - 1)
                                ? (Project)projectItem.Object
                                : GetProject(path, (Project)projectItem, ++level);
            }
            return null;
        }

        private void AddFileToProject(Project project, string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (project == null)
                throw new ArgumentNullException("Error: project is null");

            if (filePath == null)
                throw new ArgumentNullException("Error: filePath is null");

            // This is not an error - we only add the file if it exists
            if (!File.Exists(filePath))
                return;

            // just return if the file is already referenced in solutionFolderProject
            foreach (ProjectItem projectItem in project.ProjectItems)
                if (projectItem.FileCount == 1)
                {
                    if (projectItem.FileNames[1] == null) return;
                    if (projectItem.FileNames[1].Equals(filePath)) // note bizarre ordinal 1 !
                        return;
                }

            project.ProjectItems.AddFromFile(filePath);

        }

    }
}

