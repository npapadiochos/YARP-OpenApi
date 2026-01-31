using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Yarp.ReverseProxy.OpenApi.Extensions;

public static class ReverseProxyBuilderExtensions
{
    public static IReverseProxyBuilder AddOpenApi(this IReverseProxyBuilder builder, IConfigurationSection configurationSection)
    {
        ArgumentNullException.ThrowIfNull(configurationSection);

        builder.Services.Configure<ReverseProxyDocumentFilterConfig>(configurationSection);

        var config = configurationSection.Get<ReverseProxyDocumentFilterConfig>();

        builder.ConfigureHttpClient(config);

        return builder;
    }

    public static IReverseProxyBuilder AddOpenApi(this IReverseProxyBuilder builder, ReverseProxyDocumentFilterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        builder.Services.Configure((Action<ReverseProxyDocumentFilterConfig>)(overriddenConfig =>
        {
            overriddenConfig.Routes = config.Routes;
            overriddenConfig.Clusters = config.Clusters;
            overriddenConfig.OpenApiConfig = config.OpenApiConfig;
        }));

        builder.ConfigureHttpClient(config);

        return builder;
    }

    private static void ConfigureHttpClient(this IReverseProxyBuilder builder, ReverseProxyDocumentFilterConfig config)
    {
        foreach (var cluster in config.Clusters)
        {
            foreach (var destination in cluster.Value.Destinations)
            {
                var httpClientBuilder = builder.Services.AddHttpClient($"{cluster.Key}_{destination.Key}");

                if (!string.IsNullOrWhiteSpace(destination.Value.AccessTokenClientName))
                    httpClientBuilder.AddClientAccessTokenHandler(destination.Value.AccessTokenClientName);
            }
        }
    }
}
