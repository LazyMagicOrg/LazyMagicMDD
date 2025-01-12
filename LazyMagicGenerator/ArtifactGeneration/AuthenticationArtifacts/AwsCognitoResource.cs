using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;
using Microsoft.AspNetCore.Routing.Template;
using YamlDotNet.Serialization;
using LazyMagicGenerator;

namespace LazyMagic
{
    /// <summary>
    /// Generate a AWS::Cognito::UserPool resource suitable for 
    /// inclusion in an AWS SAM template. The resource is in 
    /// yaml format and available in the ExportedAwsResourceDefinition string property.
    /// </summary>
    public class AwsCognitoResource : ArtifactBase
    {
        public override string Template { get; set; } = "AWSTemplates/Templates/sam.cognito.tenant.yaml";
        public int SecurityLevel { get; set; } = 1; // defaults to JWT


        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            Authentication directive = (Authentication)directiveArg;    

            // set the stack name 
            var resourceName = directive.Key + NameSuffix ?? "";
            await InfoAsync($"Generating {directive.Key} {resourceName}");
            var deploymentConfigYamlFile = Path.Combine(solution.SolutionRootFolderPath, "Generated", "deploymentconfig.yaml");
            var deploymentConfig = new DeploymentConfig();  
            if (File.Exists(deploymentConfigYamlFile))
            {

                var deploymentConfigYaml = File.ReadAllText(deploymentConfigYamlFile);
                using (var reader = new StringReader(deploymentConfigYaml))
                {
                    var deserializer = new Deserializer();
                    deploymentConfig = deserializer.Deserialize<DeploymentConfig>(reader);
                }
            }
            // Add this authenticator to the deployment config
            var Authenticator = new Authenticator()
            {
                Name = directiveArg.Key
            };
            deploymentConfig.Authenticators.Add(Authenticator);
            var serializer = new Serializer();
            var yaml = serializer.Serialize(deploymentConfig);
            File.WriteAllText(deploymentConfigYamlFile, yaml);
        }
    }
}
