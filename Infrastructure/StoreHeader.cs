using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ChocolateStores.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
public class UseStoreHeader : Attribute { }

public class StoreHeader : IOperationFilter
{
    public static readonly string HeaderName = "store-code";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        if (context.MethodInfo.GetCustomAttribute(typeof(UseStoreHeader)) is UseStoreHeader _)
        {
            operation.Parameters.Add(
                new OpenApiParameter()
                {
                    Name = HeaderName,
                    In = ParameterLocation.Header,
                    Required = true
                }
            );
        }
    }
}
