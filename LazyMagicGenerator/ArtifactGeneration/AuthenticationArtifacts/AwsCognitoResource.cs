using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;
using Microsoft.AspNetCore.Routing.Template;
using YamlDotNet.Serialization;
using System.Text;

namespace LazyMagic
{
    /// <summary>
    /// Generate a AWS::Cognito::UserPool resource suitable for 
    /// inclusion in an AWS SAM template. The resource is in 
    /// yaml format and available in the ExportedAwsResourceDefinition string property.
    /// </summary>
    public class AwsCognitoResource : AwsResourceArtifact
    {
        public override string Template { get; set; } = "";
        public string CallbackURL { get; set; } = "https://www.example.com";
        public string LogoutURL { get; set; } = "https://www.example.com";
        public int DeleteAfterDays { get; set; } = 60;
        public int StartWindowMinutes { get; set; } = 60;
        public string ScheduleExpression { get; set; } = "cron(0 5 ? * * *)";
        public int SecurityLevel { get; set; } = 1; // defaults to JWT

        public AwsAuthenticationConfig ExportedConfig { get; set; } = null;   

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            Authentication directive = (Authentication)directiveArg;    

            // set the stack name 
            var resourceName = directive.Key + NameSuffix ?? "";
            await InfoAsync($"Generating {directive.Key} {resourceName}");
            var deploymentConfigYamlFile = Path.Combine(solution.SolutionRootFolderPath, "AwsTemplates", "Generated", "deploymentconfig.g.yaml");
            var deploymentConfig = new AwsDeploymentConfig();  
            if (File.Exists(deploymentConfigYamlFile))
            {

                var deploymentConfigYaml = File.ReadAllText(deploymentConfigYamlFile);
                using (var reader = new StringReader(deploymentConfigYaml))
                {
                    var deserializer = new Deserializer();
                    deploymentConfig = deserializer.Deserialize<AwsDeploymentConfig>(reader);
                }
            }
            // Add this authenticator to the deployment config
            ExportedConfig = new AwsAuthenticationConfig()
            {
                Name = directiveArg.Key,
                Template = Template,
                CallbackURL = CallbackURL,
                LogoutURL = LogoutURL,
                DeleteAfterDays = DeleteAfterDays,
                StartWindowMinutes = StartWindowMinutes,
                ScheduleExpression = ScheduleExpression,
                SecurityLevel = SecurityLevel
            };

            ExportedName = directive.Key;

            // StackParameters - these are parameters that will need to be passed to the service stack.
            // These parameters are gathered from the deployed authentication resources by Deploy-ServiceAws 
            // The Service Stack Parameters are outputs from the Auth Stack.
            StackParameters = new List<string>()
            {
                $@"
  {ExportedName}UserPoolIdParameter:
    Type: String",
                $@"
  {ExportedName}UserPoolClientIdParameter:
    Type: String",
                $@"
  {ExportedName}IdentityPoolIdParameter:
    Type: String",
                $@"
  {ExportedName}SecurityLevelParameter:
    Type: String",
                $@"
  {ExportedName}UserPoolArnParameter:
    Type: String"
            };
        }
    }
}
