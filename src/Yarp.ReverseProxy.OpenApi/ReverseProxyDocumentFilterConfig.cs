using System.Collections.Generic;
using System.Linq;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.OpenApi;

public sealed class ReverseProxyDocumentFilterConfig
{
    public OpenApiConfiguration OpenApiConfig { get; set; } = new();
    public IReadOnlyDictionary<string, RouteConfig> Routes { get; set; }
    public IReadOnlyDictionary<string, Cluster> Clusters { get; set; }

    public sealed class Cluster
    {
        public IReadOnlyDictionary<string, Destination> Destinations { get; set; }

        public sealed class Destination
        {
            public string AccessTokenClientName { get; set; }
            public string Address { get; set; }
            public IReadOnlyList<OpenApiDoc> OpenApiDocs { get; set; }

            public sealed class OpenApiDoc
            {
                public string PrefixPath { get; set; }
                public string PathFilterRegexPattern { get; set; }
                public IReadOnlyList<string> Paths { get; set; }
                public bool AddOnlyPublishedPaths { get; set; } = false;
                public string MetadataPath { get; set; }
            }
        }
    }

    public sealed class OpenApiConfiguration
    {
        public bool IsCommonDocument { get; set; } = false;
        public string CommonDocumentName { get; set; } = "YARP";
        public bool RenameDuplicateSchemas { get; set; } = false;
    }

    public bool IsEmpty => Clusters?.Any() != true;
}