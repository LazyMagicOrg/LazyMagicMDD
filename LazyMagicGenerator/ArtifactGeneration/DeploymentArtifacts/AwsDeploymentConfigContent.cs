using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;
using Microsoft.AspNetCore.Routing.Template;
using System.Text;
using CommandLine;
using YamlDotNet.Serialization;
using YamlDotNet.RepresentationModel;

namespace LazyMagic
{
    public class AwsDeploymentConfig : ArtifactBase
    {
        public override string Template { get; set; } = "AWSTemplates/sam.service.deployment.yaml";
        public string ExportedStackName { get; set; } = null;
        public string ExportedTemplatePath { get; set; } = null;

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            try
            {
                await Task.Delay(0);
                Deployment directive = (Deployment)directiveArg;
                var deploymentConfig = new AwsDeploymentConfigContent();

                // We collect all the cognito resources from the services as we process each service
                var AwsCognitoResources = new List<AwsCognitoResource>();
                var serviceStacks = GetAwsServiceStacks(solution);
                foreach (var service in serviceStacks)
                {
                    deploymentConfig.Services.Add(service.AwsServiceConfig);
                    AwsCognitoResources.AddRange(GetAwsCognitoResources(solution, solution.Directives[service.AwsServiceConfig.Name].Cast<Service>()));
                }

                AwsCognitoResources = AwsCognitoResources.Distinct().ToList();
                foreach (var cognitoAuth in AwsCognitoResources)
                {
                    deploymentConfig.Authentications.Add(cognitoAuth.ExportedConfig);
                }
                var AuthConfigYamlFile = Path.Combine(solution.SolutionRootFolderPath, "AwsTemplates", "Generated", "deploymentconfig.g.yaml");
                var serializer = new SerializerBuilder().Build();
                var yaml = serializer.Serialize(deploymentConfig);
                File.WriteAllText(AuthConfigYamlFile, yaml);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error Generating {GetType().Name}. {ex.Message}");
            }
        }

        private List<AwsCognitoResource> GetAwsCognitoResources(SolutionBase solution, Service directive) =>
            directive.Apis
                .Select(x => solution.Directives[x].Cast<Api>())
                .Where(api => api.Authenticators != null && api.Authenticators.Any())
                .SelectMany(api => api.Authenticators
                    .Select(auth => (AwsCognitoResource)solution.Directives[auth].Artifacts.Values.First()))
                .Distinct()
                .ToList();

        private List<AwsServiceStackTemplate> GetAwsServiceStacks(SolutionBase solution) =>
            solution.Directives.Values
                .SelectMany(x => x.Artifacts.Values)
                .Where(x => x is AwsServiceStackTemplate)
                .Cast<AwsServiceStackTemplate>()
                .ToList();
    }
}