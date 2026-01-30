### From Configuration

Update appsettings.json:

```json lines
{
  "ReverseProxy": {
    "Clusters": {
      "App1Cluster": {
        "Destinations": {
          "Default": {
            "Address": "https://localhost:5101",
            "OpenApiDocs": [ // <-- this block
              {
                "PrefixPath": "/proxy-app1",
                "Paths": [
                  "/openapi/v1.json"
                ]
              }
            ]
          }
        }
      }
    }
  }
}
```

Update Program.cs:

```csharp
var configuration = builder.Configuration.GetSection("ReverseProxy");
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddOpenApi(configuration); // <-- this line
```

### From Code

Update Program.cs:

```csharp
RouteConfig[] GetRoutes()
{
    return new[]
    {
        new RouteConfig
        {
            RouteId = "App1Route",
            ClusterId = "App1Cluster",
            Match = new RouteMatch
            {
                Path = "/proxy-app1/{**catch-all}"
            },
            Transforms = new[]
            {
                new Dictionary<string, string>
                {
                    {"PathPattern", "{**catch-all}"}
                }
            }
        }
    };
}

ClusterConfig[] GetClusters()
{
    return new[]
    {
        new ClusterConfig
        {
            ClusterId = "App1Cluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                {
                    "Default", new DestinationConfig
                    {
                        Address = "https://localhost:5101"
                    }
                }
            }
        }
    };
}

ReverseProxyDocumentFilterConfig GetOpenApiConfig()
{
    return new ReverseProxyDocumentFilterConfig
    {
        Routes = GetRoutes().ToDictionary(_ => _.RouteId, _ => _),
        Clusters = new Dictionary<string, ReverseProxyDocumentFilterConfig.Cluster>
        {
            {
                "App1Cluster", new ReverseProxyDocumentFilterConfig.Cluster
                {
                    Destinations = new Dictionary<string, ReverseProxyDocumentFilterConfig.Cluster.Destination>
                    {
                        {
                            "Default", new ReverseProxyDocumentFilterConfig.Cluster.Destination
                            {
                                Address = "https://localhost:5101",
                                OpenApiDocs = new[]
                                {
                                    new ReverseProxyDocumentFilterConfig.Cluster.Destination.OpenApiDoc
                                    {
                                        PrefixPath = "/proxy-app1",
                                        Paths = new[] {"/openapi/v1.json"}
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    };
}

builder.Services
    .AddReverseProxy()
    .LoadFromMemory(GetRoutes(), GetClusters())
    .AddOpenApi(GetOpenApiConfig()); // <-- this line
```

### Common

Update Program.cs:

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<ReverseProxyDocumentFilter>();
});
```

```csharp
app.MapOpenApi().AllowAnonymous();
app.UseSwaggerUI(options =>
{
    var config = app.Services.GetRequiredService<IOptionsMonitor<ReverseProxyDocumentFilterConfig>>().CurrentValue;
    foreach (var cluster in config.Clusters)
    {
        options.SwaggerEndpoint($"/openapi/{cluster.Key}.json", cluster.Key);
    }
});
```

# Authentication and Authorization

Update appsettings.json:

```json lines
{
  "ReverseProxy": {
    "Clusters": {
      "App1Cluster": {
        "Destinations": {
          "Default": {
            "Address": "https://localhost:5101",
            "AccessTokenClientName": "Identity", // <-- this line
            "OpenApiDocs": [
              {
                "PrefixPath": "/proxy-app1",
                "Paths": [
                  "/openapi/v1.json"
                ]
              }
            ]
          }
        }
      }
    }
  }
}
```

Update Program.cs:

```csharp
builder.Services.AddAccessTokenManagement(options =>
{
    var identityConfig = builder.Configuration.GetSection("Identity").Get<IdentityConfig>()!;
    
    options.Client.Clients.Add("Identity", new ClientCredentialsTokenRequest
    {
        Address = $"{identityConfig.Url}/connect/token",
        ClientId = identityConfig.ClientId,
        ClientSecret = identityConfig.ClientSecret
    });
});
```

# Common Swagger Document

If you want to combine multiple OpenApi documents into one.

Update appsettings.json:

```json lines
{
  "ReverseProxy": {
    "OpenApiConfig": { // <-- this block
      "IsCommonDocument": true,
      "CommonDocumentName": "YARP"
    },
  }
}
```
