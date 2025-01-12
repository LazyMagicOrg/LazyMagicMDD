using System;
using System.Collections.Generic;
using System.Text;

namespace LazyMagicGenerator
{
    public class DeploymentConfig
    {
        public List<Authenticator> Authenticators { get; set; }  = new List<Authenticator>();
    }
}
