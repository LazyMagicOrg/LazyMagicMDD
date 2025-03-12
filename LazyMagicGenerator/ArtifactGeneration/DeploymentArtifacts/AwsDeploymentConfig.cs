using System;
using System.Collections.Generic;
using System.Text;

namespace LazyMagic
{

    public class AwsDeploymentConfigContent
    {
        public List<AwsAuthenticationConfig> Authentications { get; set; }  = new List<AwsAuthenticationConfig>();
        public List<AwsServiceConfig> Services { get; set; } = new List<AwsServiceConfig>();
    }
}
