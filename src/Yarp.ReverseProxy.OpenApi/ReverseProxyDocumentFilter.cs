using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.OpenApi.Extensions;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.OpenApi;

public sealed class ReverseProxyDocumentFilter : IOpenApiDocumentTransformer
{
    private readonly List<ITransformFactory> _factories;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IReadOnlyDictionary<string, HttpMethod> _operationTypeMapping;

    private ReverseProxyDocumentFilterConfig config;

    public ReverseProxyDocumentFilter(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ReverseProxyDocumentFilterConfig> configOptions,
        IEnumerable<ITransformFactory> factories)
    {
        _factories = factories?.ToList();
        config = configOptions.CurrentValue;
        _httpClientFactory = httpClientFactory;

        configOptions.OnChange(x => { config = x; });

        _operationTypeMapping = new Dictionary<string, HttpMethod>
        {
            { "GET", HttpMethod.Get },
            { "PUT", HttpMethod.Put },
            { "HEAD", HttpMethod.Head },
            { "POST", HttpMethod.Post },
            { "PATCH", HttpMethod.Patch },
            { "TRACE", HttpMethod.Trace },
            { "DELETE", HttpMethod.Delete },
            { "OPTIONS", HttpMethod.Options }
        };
    }

    public async Task TransformAsync(OpenApiDocument doc, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (config.IsEmpty)
            return;

        IReadOnlyDictionary<string, ReverseProxyDocumentFilterConfig.Cluster> clusters;

        if (config.OpenApiConfig.IsCommonDocument)
        {
            clusters = config.Clusters;
        }
        else
        {
            clusters = config.Clusters
                .Where(x => x.Key == context.DocumentName)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        Apply(doc, clusters);
    }

    private void Apply(
        OpenApiDocument openApiDoc,
        IReadOnlyDictionary<string, ReverseProxyDocumentFilterConfig.Cluster> clusters
    )
    {
        if (true != clusters?.Any())
            return;

        var info = openApiDoc.Info;
        var paths = new OpenApiPaths();
        var components = new OpenApiComponents();
        var securityRequirements = new List<OpenApiSecurityRequirement>();
        var tags = new HashSet<OpenApiTag>();

        foreach (var clusterKeyValuePair in clusters)
        {
            var clusterKey = clusterKeyValuePair.Key;
            var cluster = clusterKeyValuePair.Value;

            if (true != cluster.Destinations?.Any())
                continue;

            foreach (var destination in cluster.Destinations)
            {
                if (true != destination.Value.OpenApiDocs?.Any())
                    continue;

                var httpClient = _httpClientFactory.CreateClient($"{clusterKey}_{destination.Key}");

                foreach (var openApi in destination.Value.OpenApiDocs)
                {
                    if (openApi.Paths?.Any() != true)
                        continue;

                    Regex filterRegex = null;
                    IReadOnlyDictionary<string, IEnumerable<string>> publishedRoutes = null;

                    if (openApi.AddOnlyPublishedPaths)
                        publishedRoutes = GetPublishedPaths(config);

                    if (!string.IsNullOrWhiteSpace(openApi.PathFilterRegexPattern))
                        filterRegex = new Regex(openApi.PathFilterRegexPattern);

                    foreach (var openApiPath in openApi.Paths)
                    {
                        if (!Uri.TryCreate(new Uri(destination.Value.Address), openApiPath, out Uri openApiUrl))
                            throw new ArgumentException("Unable to combine specified url values");

                        var stream = httpClient.GetStreamAsync(openApiUrl).Result;

                        MemoryStream memoryStream;
                        if (stream is MemoryStream ms)
                        {
                            memoryStream = ms;
                        }
                        else
                        {
                            memoryStream = new MemoryStream();
                            stream.CopyTo(memoryStream);
                            memoryStream.Position = 0;
                        }

                        var doc = OpenApiDocument.Load(memoryStream);

                        if (openApi.MetadataPath == openApiPath)
                            info = doc.Document.Info;

                        foreach (var path in doc.Document.Paths)
                        {
                            var key = path.Key;
                            var value = path.Value;

                            if (filterRegex != null && !filterRegex.IsMatch(key))
                                continue;

                            var operationKeys = path.Value.Operations.Keys.ToList();

                            if (publishedRoutes != null)
                            {
                                var pathKey = $"{openApi.PrefixPath}{path.Key}";

                                if (!publishedRoutes.ContainsKey(pathKey))
                                    continue;

                                var methods = publishedRoutes[pathKey];

                                var operations = _operationTypeMapping
                                    .Where(q => methods.Contains(q.Key))
                                    .Select(q => q.Value)
                                    .ToList();

                                foreach (var operationKey in operationKeys)
                                {
                                    if (!operations.Contains(operationKey))
                                        path.Value.Operations.Remove(operationKey);
                                }
                            }

                            ApplyOpenApiTransformation(operationKeys, path, clusterKey);

                            paths.TryAdd($"{openApi.PrefixPath}{key}", value);
                        }

                        components.Add(doc.Document.Components, config.OpenApiConfig.RenameDuplicateSchemas);

                        if (doc.Document.Security != null && doc.Document.Security.Any())
                            securityRequirements.AddRange(doc.Document.Security);

                        foreach (var tag in doc.Document.Tags)
                            tags.Add(tag);
                    }
                }
            }
        }

        openApiDoc.Info = info;
        openApiDoc.Tags = tags;
        openApiDoc.Paths = paths;
        openApiDoc.Components = components;
        openApiDoc.Security = securityRequirements;
    }

    private static IReadOnlyDictionary<string, IEnumerable<string>> GetPublishedPaths(ReverseProxyDocumentFilterConfig config)
    {
        var validRoutes = new Dictionary<string, IEnumerable<string>>();

        foreach (var route in config.Routes)
        {
            if (route.Value?.Match.Path == null)
                continue;

            if (!validRoutes.ContainsKey(route.Value.Match.Path))
                validRoutes.TryAdd(route.Value.Match.Path, route.Value.Match.Methods);
            else
                if (route.Value.Match.Methods != null)
                    validRoutes[route.Value.Match.Path] = validRoutes[route.Value.Match.Path].Concat(route.Value.Match.Methods);
        }

        return validRoutes;
    }

    private void ApplyOpenApiTransformation(List<HttpMethod> operationKeys,
        KeyValuePair<string, IOpenApiPathItem> path, string clusterKey)
    {
        var factories = _factories?.Where(x => x is IOpenApiTransformFactory).ToList();

        if (factories == null)
            return;

        foreach (var operationKey in operationKeys)
        {
            path.Value.Operations.TryGetValue(operationKey, out var operation);

            var transforms = config.Routes
                .Where(x => x.Value.ClusterId == clusterKey)
                .Where(x => x.Value.Transforms != null)
                .SelectMany(x => x.Value.Transforms)
                .ToList();

            foreach (var openApiFactory in factories.Select(factory => factory as IOpenApiTransformFactory))
            {
                foreach (var transform in transforms)
                {
                    openApiFactory?.Build(operation, transform);
                }
            }
        }
    }
}
