using Microsoft.OpenApi;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.OpenApi.Extensions;

public static class OpenApiExtensions
{
    internal static void Add(this OpenApiComponents source, OpenApiComponents components, bool renameDuplicateSchemas = false)
    {
        if (components == null)
            return;

        source.Links ??= new Dictionary<string, IOpenApiLink>();
        source.Headers ??= new Dictionary<string, IOpenApiHeader>();
        source.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        source.Examples ??= new Dictionary<string, IOpenApiExample>();
        source.Callbacks ??= new Dictionary<string, IOpenApiCallback>();
        source.Responses ??= new Dictionary<string, IOpenApiResponse>();
        source.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        source.Parameters ??= new Dictionary<string, IOpenApiParameter>();
        source.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>();
        source.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        if (components.Links != null)
            foreach (var link in components.Links)
                source.Links.TryAdd(link.Key, link.Value);

        if (components.Headers != null)
            foreach (var header in components.Headers)
                source.Headers.TryAdd(header.Key, header.Value);

        if (components.Examples != null)
            foreach (var example in components.Examples)
                source.Examples.TryAdd(example.Key, example.Value);

        if (components.Callbacks != null)
            foreach (var callback in components.Callbacks)
                source.Callbacks.TryAdd(callback.Key, callback.Value);

        if (components.Responses != null)
            foreach (var response in components.Responses)
                source.Responses.TryAdd(response.Key, response.Value);

        if (components.Extensions != null)
            foreach (var extension in components.Extensions)
                source.Extensions.TryAdd(extension.Key, extension.Value);

        if (components.Parameters != null)
            foreach (var parameter in components.Parameters)
                source.Parameters.TryAdd(parameter.Key, parameter.Value);

        if (components.RequestBodies != null)
            foreach (var requestBody in components.RequestBodies)
                source.RequestBodies.TryAdd(requestBody.Key, requestBody.Value);

        if (components.SecuritySchemes != null)
            foreach (var securityScheme in components.SecuritySchemes)
                source.SecuritySchemes.TryAdd(securityScheme.Key, securityScheme.Value);

        if (components.Schemas != null)
        {
            foreach (var schema in components.Schemas)
            {
                int i = 1;
                bool added = source.Schemas.TryAdd(schema.Key, schema.Value);
                
                while (!added && renameDuplicateSchemas)
                {
                    i++;
                    var key = $"{schema.Key}{i}";
                    added = source.Schemas.TryAdd(key, schema.Value);
                }
            }
        }
    }
}
