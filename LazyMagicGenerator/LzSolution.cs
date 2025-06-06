﻿using System;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;
using NSwag;
using System.Linq;
using Microsoft.CodeAnalysis;
using static LazyMagic.OpenApiUtils;
using System.Collections.Generic;

namespace LazyMagic
{
    /// <summary>
    /// This class holds the processing state for the generation process
    /// Typical usage:
    /// var lzSolution = new LzSolution(logger, solutionRootFolderPath);
    /// await this.GenerateAsync();
    /// Note: We are passing logger in as a parameter instead of using 
    /// a static class or DI because of limitations in the Visual Studio IDE
    /// extension pattern this class is used in.
    /// </summary>
    public class LzSolution : SolutionBase
    {
        private string DirectiveFilePath { get; }

        public LzSolution(ILogger logger, string solutionRootFolderPath)
        {
            LzLogger.SetLogger(logger);
            SolutionRootFolderPath = solutionRootFolderPath;
            DirectiveFilePath = Path.Combine(solutionRootFolderPath, "LazyMagic.yaml");
        }

        #region Public Methods

        public async Task ProcessAsync()
        {
            var generatedDirectory = Path.Combine(SolutionRootFolderPath, "AWSTemplates", "Generated");
            Directory.CreateDirectory(generatedDirectory);
            Directory.GetFiles(generatedDirectory).ToList().ForEach(File.Delete);

            await LoadDirectivesFileAsync(); // Reads the Directives from the LazyMagic.yaml file
            Directives.Validate(); // Appies defaults and validates the resulting Directives
            await LoadAggregateSchemas(); // Loads the OpenApi directive files 
            await Directives.ProcessAsync(this); // Processes the Directives
            await LzLogger.InfoAsync("done");
        }

        public async Task TestDirectiveValidation(string directiveFilePath)
        {
            await LoadDirectivesFileAsync(directiveFilePath); // Reads the Directives from the LazyMagic.yaml file
            Directives.Validate(); // Appies defaults and validates the resulting Directives
        }
        #endregion

        private async Task LoadDirectivesFileAsync(string directiveFilePath = null)
        {
            directiveFilePath = directiveFilePath ?? DirectiveFilePath; //set default
            
            try
            {
                await LzLogger.InfoAsync("Parsing Directives file");
                var yaml = File.ReadAllText(directiveFilePath);
                using (var reader = new StringReader(yaml))
                {
                    string yamlContent = reader.ReadToEnd();
                    var deserializer = new DeserializerBuilder() 
                           .WithTypeConverter(new DirectivesPropertyConverter())
                           .WithTypeConverter(new DirectivePropertyConverter())
                           .WithTypeConverter(new ArtifactsPropertyConverter())
                           .WithTypeConverter(new ArtifactPropertyConverter())
                           .WithNodeDeserializer(inner => new DetailedErrorNodeDeserializer(inner), s => s.InsteadOf<ObjectNodeDeserializer>())
                           .Build();

                    var result = deserializer.Deserialize<SolutionBase>(yamlContent);
                    Directives = result.Directives;
                    LazyMagicDirectivesVersion = result.LazyMagicDirectivesVersion;
                    await LzLogger.InfoAsync("Version: " + result.LazyMagicDirectivesVersion);
                }

                await LzLogger.InfoAsync("Directives parsed.");

            }
            catch (Exception ex)
            {
                var msg = $"Directives parse failed. {ex.Message}";
                await LzLogger.InfoAsync(msg);
                throw new Exception(msg);
            }


            #region Local Functions
            #endregion

        }
        private async Task LoadAggregateSchemas()
        {
            AggregateSchemas = await LoadOpenApiFilesAsync(
                SolutionRootFolderPath,
                Directives.Values
                    .OfType<Schema>()
                    .SelectMany(s => s.OpenApiSpecs)
                    .Distinct()
                    .ToList()
            );
            AggregateSchemas.Paths.Clear(); // We only want the schemas
        }

    }
}
