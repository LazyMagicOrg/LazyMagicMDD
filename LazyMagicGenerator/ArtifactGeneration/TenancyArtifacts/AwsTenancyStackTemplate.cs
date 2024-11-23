using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;
using System.Text;
using CommandLine;
using NJsonSchema.CodeGeneration;

namespace LazyMagic
{

    /// <summary>
    /// Generate a AWS SAM template for a tenant and write it to the 
    /// Generated folder. 
    /// Generate the Deploy-Tenant-Stack.ps1 script and write it to the 
    /// Generated folder.
    /// </summary>
    public class AwsTenancyStackTemplate : ArtifactBase
    {
        public string ExportedTemplatePath { get; set; } = null;
        public override string Template { get; set; } = "AWSTemplates/Snippets/sam.tenant.yaml";
        public string CloudFrontSnippet { get; set; } = "AWSTemplates/Snippets/sam.tenant.cloudfront.yaml";
        public string CloudFrontWebAppSnippet { get; set; } = "AWSTemplates/Snippets/sam.tenant.cloudfront.webapp.yaml";    
        public string CloudFrontApiSnippet { get; set; } = "AWSTemplates/Snippets/sam.tenant.cloudfront.api.yaml";  
        public string CloudFrontApiOriginSnippet { get; set; } = "AWSTemplates/Snippets/sam.tenant.cloudfront.apiorigin.yaml";
        public string CloudFrontWsSnippet { get; set; } = "AWSTemplates/Snippets/sam.tenant.cloudfront.ws.yaml";
        public string CloudFrontWsOriginSnippet { get; set; } = "AWSTemplates/Snippets/sam.tenant.cloudfront.wsorigin.yaml";
        public string CloudFrontWebAppOriginSnippet { get; set; } = "AWSTemplates/Snippets/sam.tenant.cloudfront.webapporigin.yaml";
        public string CloudFrontLandingPageFunctionSnippet { get; set; } = "AWSTemplates/Snippets/sam.tenant.cloudfront.landingpage.yaml";
        public string DeployScriptSnippet { get; set; } = "AWSTemplates/Snippets/Deploy-Tenant-Stack.ps1";

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            var templateName = $"sam.{directiveArg.Key}.g.yaml";

            try
            {
                Tenancy directive = (Tenancy)directiveArg;

                var scriptName = $"Deploy-Tenant-{directive.Key}-Stack.g.ps1";

                await InfoAsync($"Generating {directive.Key} {templateName}");
                if (string.IsNullOrEmpty(Template)) Template = null;

                var templateBuilder = new StringBuilder();
               
                // Get the template and replace __tokens__
                templateBuilder.Append(File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, Template)));

                var teantCloudFrontSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, CloudFrontSnippet));
                teantCloudFrontSnippet.Replace("__TemplateSource__", CloudFrontSnippet);

                var tenantCloudFrontWebAppSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, CloudFrontWebAppSnippet));
                tenantCloudFrontWebAppSnippet.Replace("__TemplateSource__", CloudFrontWebAppSnippet);

                var tenantCloudFrontApiSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, CloudFrontApiSnippet));
                tenantCloudFrontApiSnippet.Replace("__TemplateSource__", CloudFrontApiSnippet);

                var tenantCloudFrontApiOriginSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, CloudFrontApiOriginSnippet));
                tenantCloudFrontApiOriginSnippet.Replace("__TemplateSource__", CloudFrontApiOriginSnippet);

                var tenantCloudFrontWsSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, CloudFrontWsSnippet));
                tenantCloudFrontWsSnippet.Replace("__TemplateSource__", CloudFrontWsSnippet);

                var tenantCloudFrontWsOriginSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, CloudFrontWsOriginSnippet));
                tenantCloudFrontWsOriginSnippet.Replace("__TemplateSource__", CloudFrontWsOriginSnippet);

                var tenantCloudFrontWebAppOriginSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, CloudFrontWebAppOriginSnippet));
                tenantCloudFrontWebAppOriginSnippet.Replace("__TemplateSource__", CloudFrontWebAppOriginSnippet);

                var tenantCloudFrontLandingPageFunctionSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, CloudFrontLandingPageFunctionSnippet));
                tenantCloudFrontLandingPageFunctionSnippet.Replace("__TemplateSource__", CloudFrontLandingPageFunctionSnippet);

                var tenantDeployScriptSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, DeployScriptSnippet));
                tenantDeployScriptSnippet.Replace("__TemplateSource__", DeployScriptSnippet);

                /* #LzServiceParameters# INSERT PARAMETERS */

                var webappsParametersBuilder = new StringBuilder();
                var webAppNames = GetWebAppNames(solution, directive);
                foreach (var webappName in webAppNames)
                    webappsParametersBuilder.Append($@"  
  {webappName}BucketNameParameter:
    Type: String");

                templateBuilder.Replace("#LzWebAppParameters#", webappsParametersBuilder.ToString());

                var serviceParametersBuilder = new StringBuilder();
                var apiNames = GetWebAppApiNames(solution, directive);
                foreach (var apiName in apiNames)
                    serviceParametersBuilder.Append($@"
  {apiName}IdParameter:
    Type: String");

                var cognitoNames = GetWebAppCognitoNames(solution, directive);
                foreach (var cognitoName in cognitoNames)
                {
                    if (string.IsNullOrEmpty(cognitoName)) continue;
                    serviceParametersBuilder.Append($@"
  {cognitoName}UserPoolNameParameter:
    Type: String
  {cognitoName}UserPoolIdParameter:
    Type: String
  {cognitoName}UserPoolClientIdParameter:
    Type: String
  {cognitoName}IdentityPoolIdParameter:
    Type: String
  {cognitoName}SecurityLevelParameter:
    Type: String
");
                }

                templateBuilder.Replace("#LzServiceParameters#", serviceParametersBuilder.ToString());

                /* BUILD CLOUDFRONT TEMPLATE */
                var cloudFrontBuilder = new StringBuilder(teantCloudFrontSnippet);
                cloudFrontBuilder.Replace("__ResourceGenerator__", GetType().Name);

                /* #LzCacheBehaviors# INSERT CACHE BEHAVIORS  
                 * It is necessary to build a dictionary of chache behaviors for each webapp and api
                 * and then sort that list by the path pattern descending to ensure no path is shadowed by another.
                */
                var cacheBehaviors = new Dictionary<string, string>();
                foreach (var webAppDirective in GetWebApps(solution, directive))
                {

                    var path = $"/{webAppDirective.Path}/*";
                    var body = tenantCloudFrontWebAppSnippet
                        .Replace("__PathPattern__", path)
                        .Replace("__TargetOriginId__", $"{webAppDirective.Key}-Origin");
                    cacheBehaviors.Add(path, body);

                    var path2 = $"/{webAppDirective.Path}";
                    var body2 = tenantCloudFrontWebAppSnippet
                        .Replace("__PathPattern__", path2)
                        .Replace("__TargetOriginId__", $"{webAppDirective.Key}-Origin");
                    cacheBehaviors.Add(path2, body2);
                }

                // Get only the apiHttpApiResources referenced by the webapps
                //var apiHttpApiResources = GetAwsHttpApiResources(solution, directive);
                // We need to have one Api Cache Behavior for each Api Gateway
                var apiDirectives = GetApis(solution, directive);
                foreach (var apiDirective in apiDirectives)
                {
                    var apiArtifact = apiDirective.Artifacts.Values
                        .OfType<IAwsApiResource>().FirstOrDefault();
                    var path = $"/{apiArtifact.ExportedPrefix}/*";
                    var body = tenantCloudFrontApiSnippet
                        .Replace("__PathPattern__", path)
                        .Replace("__TargetOriginId__", $"{apiDirective.Key}-Origin");
                    cacheBehaviors.Add(path, body);
                }

                var cacheBehaviorsList = string.Join("",
                    cacheBehaviors
                        .OrderByDescending(x => x.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(x => x.Value)
                        .ToList());
                cloudFrontBuilder.Replace("#LzCacheBehaviors#", cacheBehaviorsList);

                /* #LzWebAppOrigins# INSERT WEBAPP ORIGINS */
                var webAppOriginsBuilder = new StringBuilder();
                foreach (var webAppDirective in GetWebApps(solution, directive))
                {
                    var webAppOrigin = tenantCloudFrontWebAppOriginSnippet.Replace("__OriginId__", $"{webAppDirective.Key}-Origin");
                    webAppOrigin = webAppOrigin.Replace("__WebAppBucket__", $"{webAppDirective.Key}BucketNameParameter");
                    webAppOriginsBuilder.Append(webAppOrigin);
                    //webAppOriginsBuilder.Replace("__WebAppBucket__", webAppDirective.DomainName);
                }
                cloudFrontBuilder.Replace("#LzWebAppOrigins#", webAppOriginsBuilder.ToString());

                /* "#LzApiOrigins#" INSERT API ORIGINS */
                var apiOriginsBuilder = new StringBuilder();
                foreach (var apiDirective in apiDirectives)
                {
                    var apiOrigin = tenantCloudFrontApiOriginSnippet.Replace("__OriginId__", $"{apiDirective.Key}-Origin");
                    apiOrigin = apiOrigin.Replace("__ApiId__", $"{apiDirective.Key}IdParameter");
                    apiOriginsBuilder.Append(apiOrigin);
                }
                var originsText = apiOriginsBuilder.ToString();
                cloudFrontBuilder.Replace("#LzApiOrigins#", originsText);

                /* #LzLandingPageFunction# LANDING PAGE FUNCTION */
                var landingPageFunction = tenantCloudFrontLandingPageFunctionSnippet;
                var webAppSelections = new StringBuilder();
                foreach (var webAppDirective in GetWebApps(solution, directive))
                {
                    webAppSelections.Append($@"
                        <a href='/{webAppDirective.Path}'>{webAppDirective.Key} App</a>
");
                }
                landingPageFunction.Replace("__WebApps__", webAppSelections.ToString());
                landingPageFunction = landingPageFunction.Replace("__WebApps__", webAppSelections.ToString());
                cloudFrontBuilder.Replace("#LzLandingPageFunction#", landingPageFunction);

                templateBuilder
                    .Replace("#LzCloudFront#", cloudFrontBuilder.ToString())
                    .Replace("__ResourceGenerator__", this.GetType().Name)
                    .Replace("__TemplateSource__", Template);

                /* SAVE COMPLETED TEMPLATE */
                var templatePath = Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Generated", templateName);
                File.WriteAllText(templatePath, templateBuilder.ToString());

                /****************   SCRIPT ******************/
                /* INSERT __webappstackoutputs__ INTO SCRIPT */
                var webAppStackReferences = "";
                foreach (var webAppDirective in GetWebApps(solution, directive))
                {
                    var appName = $"{webAppDirective.Key}";
                    webAppStackReferences += $@"
$targetStack = $config.SystemName + ""-webapp-{appName.ToLower()}"" 
${appName}StackOutputDict = Get-StackOutputs $targetStack
Display-OutputDictionary -Dictionary ${appName}StackOutputDict -Title ""storeapp Stack Outputs""
                    ";
                }
                tenantDeployScriptSnippet = tenantDeployScriptSnippet.Replace("__webappstackoutputs__", webAppStackReferences);

                // INSERT __webapps__ INTO SCRIPT 
                var webAppParameters = "";
                foreach (var webAppDirective in GetWebApps(solution, directive))
                {
                    var appName = $"{webAppDirective.Key}";
                    webAppParameters += $@"
    ""{appName}BucketNameParameter"" = ${appName}StackOutputDict[""AppBucket""]
";
                }
                tenantDeployScriptSnippet = tenantDeployScriptSnippet.Replace("__webapps__", webAppParameters);

                // INSERT __apis__ INTO SCRIPT
                var apiParameters = "";
                foreach (var apiDirective in apiDirectives)
                {
                    var apiName = apiDirective.Key;
                    apiParameters += $@"
    ""{apiName}IdParameter"" = $ServiceStackOutputDict[""{apiName}Id""]
";
                }
                tenantDeployScriptSnippet = tenantDeployScriptSnippet.Replace("__apis__", apiParameters);

                // INSERT __auths__ INTO SCRIPT
                var authParameters = "";
                foreach (var apiDirective in apiDirectives)
                {
                    if (string.IsNullOrEmpty(apiDirective.Authentication)) continue; // No authentication 
                    var apiArtifact = apiDirective.Artifacts.Values.FirstOrDefault(x => x is AwsHttpApiResource) as AwsHttpApiResource;
                    var authName = apiDirective.Authentication;
                    authParameters += $@"
    ""{authName}UserPoolNameParameter"" = $ServiceStackOutputDict[""{authName}UserPoolName""]
    ""{authName}UserPoolIdParameter"" = $ServiceStackOutputDict[""{authName}UserPoolId""]
    ""{authName}UserPoolClientIdParameter"" = $ServiceStackOutputDict[""{authName}UserPoolClientId""]
    ""{authName}IdentityPoolIdParameter"" = $ServiceStackOutputDict[""{authName}IdentityPoolId""]
    ""{authName}SecurityLevelParameter"" = $ServiceStackOutputDict[""{authName}SecurityLevel""]
";
                }
                tenantDeployScriptSnippet = tenantDeployScriptSnippet.Replace("__auths__", authParameters);

                // REPLACE TEMPLATE NAME
                tenantDeployScriptSnippet = tenantDeployScriptSnippet.Replace("__templatename__", templateName);

                // WRITE COMPLETED SCRIPT
                var scriptPath = Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates/Generated", scriptName);
                File.WriteAllText(scriptPath, tenantDeployScriptSnippet);


                // Exports
                ExportedTemplatePath = templatePath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating {GetType().Name}: {templateName} : {ex.Message}");    
            }   
        }

        public List<WebApp> GetWebApps(SolutionBase solution, Tenancy directive)
        {
            var webApps = new List<WebApp>();
            foreach(var webAppDirective in directive.WebApps.Select(x => solution.Directives[x].Cast<WebApp>()).ToList())
                webApps.Add(webAppDirective);   
            return webApps.Distinct().ToList(); 
        }

        public List<string> GetWebAppNames(SolutionBase solution, Tenancy directive)
        {
            var webAppNames = new List<string>();
            foreach(var webAppDirective in GetWebApps(solution, directive))
                webAppNames.Add(webAppDirective.Key);
            return webAppNames.Distinct().ToList(); 
        }

        public List<Api> GetApis(SolutionBase solution, Tenancy directive)
        {
            var apis = new List<Api>();
            var webApps = GetWebApps(solution, directive);  
            foreach(var webAppDirective in webApps)
                foreach(var apiDirective in webAppDirective.Apis.Select(x => solution.Directives[x].Cast<Api>()).ToList())
                    apis.Add(apiDirective);
            return apis.Distinct().ToList(); 
        }
        public List<string> GetWebAppApiNames(SolutionBase solution, Tenancy directive)
        {
            var apiNames = new List<string>();
            foreach(var webAppDirective in GetWebApps(solution, directive))
                apiNames.AddRange(webAppDirective.Apis);
            return apiNames.Distinct().ToList(); 
        }
        public List<string> GetWebAppCognitoNames(SolutionBase solution, Tenancy directive)
        {
            var authenticationNames = new List<string>();
            var apiNames = GetWebAppApiNames(solution, directive);
            foreach (var apiDirective in apiNames.Select(x => solution.Directives[x].Cast<Api>()).ToList())
                authenticationNames.Add(apiDirective.Authentication);
            return authenticationNames.Distinct().ToList(); 
        }
    
    }
}
