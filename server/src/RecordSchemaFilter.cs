using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Heartbeat.Server ;

// Schema filter to mark record positional parameters as required
  public class RecordSchemaFilter : ISchemaFilter
  {
    #region ISchemaFilter Members

    /// <summary>
    ///   Applies the schema filter.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <param name="context">The context.</param>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
      if (schema is not OpenApiSchema concreteSchema)
        return;

      Type? type = context.Type;

      if (type.IsValueType || type.BaseType?.Name != "Object") return;
      ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
      ConstructorInfo? primaryConstructor = constructors
        .Where(c => c.GetParameters().Length > 0)
        .OrderByDescending(c => c.GetParameters().Length)
        .FirstOrDefault();

      if (primaryConstructor == null) return;
      // OpenApiSchema.Required is a mutable HashSet in the concrete type
      HashSet<string> requiredProperties = new();
      PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

      foreach (ParameterInfo param in primaryConstructor.GetParameters())
      {
        // Check if parameter has [Required] attribute
        bool hasRequired = param.GetCustomAttribute<RequiredAttribute>() != null;

        // Find the corresponding property (case-insensitive, handling camelCase)
        PropertyInfo? property = properties.FirstOrDefault(p =>
          string.Equals(p.Name, param.Name, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(p.Name, ToPascalCase(param.Name ?? ""), StringComparison.Ordinal));

        if (property == null || !hasRequired) continue;
        // Use camelCase property name for JSON (matching the JsonOptions configuration)
        string propertyName = char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);
        requiredProperties.Add(propertyName);
      }

      if (requiredProperties.Count == 0 || concreteSchema.Required == null) return;
      foreach (string prop in requiredProperties)
        concreteSchema.Required.Add(prop);
    }

    #endregion

    private static string ToPascalCase(string name)
    {
      if (string.IsNullOrEmpty(name)) return name;
      return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
  }