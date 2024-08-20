using LazyMagic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using NSwag;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace LazyMagic
{
    public class SolutionBase
    {
        public string LazyMagicDirectivesVersion { get; set; }
        public Directives Directives { get; set; }
        public string SolutionRootFolderPath { get; set; }
        public OpenApiDocument AggregateSchemas { get; set; }

    }
}
