using Microsoft.OpenApi;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.OpenApi;

public interface IOpenApiTransformFactory
{
    bool Build(OpenApiOperation operation, IReadOnlyDictionary<string, string> transformValues);
}
