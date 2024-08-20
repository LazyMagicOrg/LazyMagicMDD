using System;
using System.Collections.Generic;
using System.Text;

namespace LazyMagic
{
    public interface IAwsApiResource
    {
        string ExportedResourceName { get; set; }
        string ExportedResource { get; set; }
        string ExportedPath { get; set; }
        string ExportedPrefix { get; set; }
    }
}
