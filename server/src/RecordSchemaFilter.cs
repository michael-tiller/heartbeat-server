using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Heartbeat.Server;

// Schema filter to mark record positional parameters as required
public class RecordSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concreteSchema)
            return;

        var type = context.Type;
        
        if (!type.IsValueType && type.BaseType?.Name == "Object")
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            var primaryConstructor = constructors
                .Where(c => c.GetParameters().Length > 0)
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();
            
            if (primaryConstructor != null)
            {
                // OpenApiSchema.Required is a mutable HashSet in the concrete type
                HashSet<string> requiredProperties = new();
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                
                foreach (var param in primaryConstructor.GetParameters())
                {
                    // Check if parameter has [Required] attribute
                    var hasRequired = param.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() != null;
                    
                    // Find the corresponding property (case-insensitive, handling camelCase)
                    var property = properties.FirstOrDefault(p => 
                        string.Equals(p.Name, param.Name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.Name, ToPascalCase(param.Name ?? ""), StringComparison.Ordinal));
                    
                    if (property != null && hasRequired)
                    {
                        // Use camelCase property name for JSON (matching the JsonOptions configuration)
                        var propertyName = char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);
                        requiredProperties.Add(propertyName);
                    }
                }
                
                if (requiredProperties.Any() && concreteSchema.Required != null)
                {
                    foreach (var prop in requiredProperties)
                    {
                        concreteSchema.Required.Add(prop);
                    }
                }
            }
        }
    }
    
    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}
