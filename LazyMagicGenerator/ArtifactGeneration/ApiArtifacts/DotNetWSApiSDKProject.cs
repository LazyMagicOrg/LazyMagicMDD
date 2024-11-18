using NSwag.CodeGeneration.CSharp;
using System;   
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static LazyMagic.DotNetUtils;
using static LazyMagic.LzLogger;
using static LazyMagic.OpenApiUtils;
using static LazyMagic.ArtifactUtils;
using NSwag;
using System.ComponentModel;
using NSwag.Collections;

namespace LazyMagic
{
    public class DotNetWSApiSDKProject : DotNetProjectBase
    {
        #region Properties
        public override string ProjectFilePath => ExportedProjectPath;
        #endregion

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var projectName = directiveArg.Key;
            try
            {
                await Task.Delay(0);
                Api directive = (Api)directiveArg;
                var apiPrefix = directive.Key;   
                
                projectName += NameSuffix ?? "";    
                var nameSpace = projectName;
                Info($"Generating {directive.Key} {projectName}");

                // TODO: Copy the template project to the target project. Removes *.g.* files.

                // Exports
                ExportedName = projectName;
                ExportedProjectPath = Path.Combine(OutputFolder, projectName, projectName + ".csproj");
            } catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name} for {projectName} {ex.Message}");    
            }
        }

    }
}
