using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static LazyMagic.LzLogger;
using System.Text;
using CommandLine;
using NJsonSchema.Infrastructure;
using System.Security.Cryptography;

namespace LazyMagic
{
    public class AwsTenancyStackTemplate : ArtifactBase
    {
        public string ExportedTemplatePath { get; set; } = null;

        public override async Task GenerateAsync(SolutionBase solution, DirectiveBase directiveArg)
        {
            Tenancy directive = (Tenancy)directiveArg;

            // set the stack name 
            var templateName = $"sam.{directive.Key}.g.yaml";
            await InfoAsync($"Generating {directive.Key} {templateName}");
            if (string.IsNullOrEmpty(Template)) Template = null;

            var templateBuilder = new StringBuilder();
            // Get the template and replace __tokens__
            templateBuilder.Append( File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, Template ?? "AWSTemplates/Snippets/sam.tenant.yaml")));
            var teantCloudFrontSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.tenant.cloudfront.yaml"));
            var tenantCloudFrontWebAppSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.tenant.cloudfront.webapp.yaml"));
            var tenantCloudFrontApiSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.tenant.cloudfront.api.yaml"));
            var tenantCloudFrontApiOriginSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.tenant.cloudfront.apiorigin.yaml"));
            var tenantCloudFrontWsSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.tenant.cloudfront.ws.yaml"));
            var tenantCloudFrontWsOriginSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.tenant.cloudfront.wsorigin.yaml"));
            var tenantCloudFrontWebAppOriginSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.tenant.cloudfront.webapporigin.yaml"));  
            var tenantCloudFrontConfigFunctionSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.tenant.cloudfront.configfunction.yaml"));
            var tenantCloudFrontLandingPageFunctionSnippet = File.ReadAllText(Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Snippets", "sam.tenant.cloudfront.landingpage.yaml"));

            /* INSERT PARAMETERS */

            var webappsParametersBuilder = new StringBuilder();
            var webAppNames = GetWebAppNames(solution, directive);
            foreach (var webappName in webAppNames)
                webappsParametersBuilder.Append($@"  
  {webappName}BucketNameParameter:
    Type: String");

            templateBuilder.Replace("#LzWebAppParameters#", webappsParametersBuilder.ToString());

            var serviceParametersBuilder = new StringBuilder();
            var apiNames = GetWebAppApiNames(solution, directive);
            foreach(var apiName in apiNames)
                serviceParametersBuilder.Append($@"
  {apiName}IdParameter:
    Type: String");

            var cognitoNames = GetWebAppCognitoNames(solution, directive);
            foreach (var cognitoName in cognitoNames)
            {
                if(string.IsNullOrEmpty(cognitoName)) continue; 
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


            /* BUILD CLOUDFRONT TEMPLATE*/
            var cloudFrontBuilder = new StringBuilder(teantCloudFrontSnippet);

            /* INSERT CACHE BEHAVIORS  
             * It is necessary to build a dictionary of chache behaviors for each webapp and api
             * and then sort that list by the path pattern descending to ensure no path is shadowed by another.
            */
            var cacheBehaviors = new Dictionary<string, string>();

            foreach(var webAppDirective in GetWebApps(solution, directive))
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
            foreach(var apiDirective in apiDirectives)
            {
                var apiArtifact = apiDirective.Artifacts.Values.OfType<IAwsApiResource>().FirstOrDefault();
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

            /* INSERT WEBAPP ORIGINS */
            var webAppOriginsBuilder = new StringBuilder();
            foreach(var webAppDirective in GetWebApps(solution, directive))
            {
                var webAppOrigin = tenantCloudFrontWebAppOriginSnippet.Replace("__OriginId__", $"{webAppDirective.Key}-Origin");
                webAppOrigin = webAppOrigin.Replace("__WebAppBucket__", $"{webAppDirective.Key}BucketNameParameter");
                webAppOriginsBuilder.Append(webAppOrigin);
                //webAppOriginsBuilder.Replace("__WebAppBucket__", webAppDirective.DomainName);
            }
            cloudFrontBuilder.Replace("#LzWebAppOrigins#", webAppOriginsBuilder.ToString());

            /* INSERT API ORIGINS */
            var apiOriginsBuilder = new StringBuilder();
            foreach(var apiDirective in apiDirectives)
            {
                var apiOrigin = tenantCloudFrontApiOriginSnippet.Replace("__OriginId__", $"{apiDirective.Key}-Origin");
                apiOrigin = apiOrigin.Replace("__ApiId__", $"{apiDirective.Key}IdParameter");
                apiOriginsBuilder.Append(apiOrigin);
            }
            var originsText = apiOriginsBuilder.ToString();
            cloudFrontBuilder.Replace("#LzApiOrigins#", originsText); 

            /* INSERT WEBSOCKET ORIGIN */
            //cloudFrontBuilder.Replace("#LzWsOrigins#", "");

            /* INSERT CONFIG FUNCTION */
            var configJson = new StringBuilder();
            foreach(var apiDirective in apiDirectives)
            {
                if(string.IsNullOrEmpty(apiDirective.Authentication)) continue; // No authentication 
                var apiArtifact = apiDirective.Artifacts.Values.FirstOrDefault(x => x is AwsHttpApiResource) as AwsHttpApiResource;
                var jsonText = $@"
                                {apiDirective.Authentication}: {{
                                    awsRegion: '${{AWS::Region}}',
                                    userPoolName: '${{{apiDirective.Authentication}UserPoolNameParameter}}',
                                    userPoolId: '${{{apiDirective.Authentication}UserPoolIdParameter}}',
                                    userPoolClientId: '${{{apiDirective.Authentication}UserPoolClientIdParameter}}',
                                    userPoolSecurityLevel: '${{{apiDirective.Authentication}SecurityLevelParameter}}',
                                    identityPoolId: '${{{apiDirective.Authentication}IdentityPoolIdParameter}}'
                                }},
";
                configJson.Append(jsonText);
            }
            var config = new StringBuilder(tenantCloudFrontConfigFunctionSnippet);
            config = config.Replace("__JsonText__", configJson.ToString());
            var configText = config.ToString();
            cloudFrontBuilder.Replace("#LzConfigFunction#", configText);

            /* LANDING PAGE FUNCTION */
            var landingPageFunction = tenantCloudFrontLandingPageFunctionSnippet;
            var webAppSelections = new StringBuilder();
            foreach(var webAppDirective in GetWebApps(solution, directive))
            {
                webAppSelections.Append($@"
                        <a href='/{webAppDirective.Path}'>{webAppDirective.Key} App</a>
");
            }
            landingPageFunction.Replace("__WebApps__",webAppSelections.ToString());
            landingPageFunction = landingPageFunction.Replace("__WebApps__", webAppSelections.ToString());
            cloudFrontBuilder.Replace("#LzLandingPageFunction#", landingPageFunction);


            /* INSERT CLOUDFRONT SECTION INTO STACK TEMPLATE */
            templateBuilder.Replace("#LzCloudFront#", cloudFrontBuilder.ToString());

            /* SAVE COMPLETED TEMPLATE */
            var templatePath = Path.Combine(solution.SolutionRootFolderPath, "AWSTemplates", "Generated", templateName);
            File.WriteAllText(templatePath, templateBuilder.ToString());

            // Exports
            ExportedTemplatePath = templatePath;
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
