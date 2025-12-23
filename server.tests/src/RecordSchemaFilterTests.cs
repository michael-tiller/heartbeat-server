using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Heartbeat.Server.Tests.OpenApi ;

  /// <summary>
  ///   Tests for RecordSchemaFilter which marks record positional parameters
  ///   with [Required] attribute as required in OpenAPI schema.
  /// </summary>
  [TestFixture]
  public class RecordSchemaFilterTests
  {
    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
      filter = new RecordSchemaFilter();
      schemaRepository = new SchemaRepository();

      JsonSerializerOptions serializerOptions = new()
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
      };

      schemaGenerator = new SchemaGenerator(
        new SchemaGeneratorOptions(),
        new JsonSerializerDataContractResolver(serializerOptions));
    }

    #endregion

    private RecordSchemaFilter filter = null!;
    private SchemaGenerator schemaGenerator = null!;
    private SchemaRepository schemaRepository = null!;

    // Record with single required parameter
    public record SingleRequiredRecord([property: Required] string Name);

    // Record with no required parameters
    private record NoRequiredRecord(string Name);

    // Record with multiple required parameters
    public record MultipleRequiredRecord(
      [property: Required] string Name,
      [property: Required] string Email);

    // Record with mixed parameters (some required, some not)
    public record MixedRecord(
      [property: Required] string Name,
      string OptionalDescription);

    // Record with all optional parameters
    public record AllOptionalRecord(string Name, int? Age, string? Description);

    // Regular class (not a record)
    public class RegularClass
    {
      public string? Name { get; set; }
    }

    // Struct (value type)
    public struct TestStruct
    {
      public string Name { get; set; }
    }

    // Record with parameterless constructor
    public record EmptyRecord;

    // Record with nested types
    public record NestedRecord([property: Required] string Id, SingleRequiredRecord? Child);

    private OpenApiSchema CreateSchemaFor<T>()
    {
      // Generate the schema - this registers it in the repository
      schemaGenerator.GenerateSchema(typeof(T), schemaRepository);

      // Get the schema by type name from the repository
      string typeName = typeof(T).Name;
      if (schemaRepository.Schemas.TryGetValue(typeName, out IOpenApiSchema? schema) && schema is OpenApiSchema concreteSchema)
        return concreteSchema;

      // Fallback for inline schemas (primitives, etc.)
      return new OpenApiSchema();
    }

    private SchemaFilterContext CreateFilterContext<T>()
    {
      return new SchemaFilterContext(
        typeof(T),
        schemaGenerator,
        schemaRepository);
    }

    [Test]
    public void Apply_EmptyRecord_DoesNotThrow()
    {
      // Arrange
      OpenApiSchema schema = CreateSchemaFor<EmptyRecord>();

      // Act & Assert - should not throw
      SchemaFilterContext context = CreateFilterContext<EmptyRecord>();
      Assert.DoesNotThrow(() => filter.Apply(schema, context));
    }

    [Test]
    public void Apply_PropertyName_ConvertsToCamelCase()
    {
      // Arrange
      OpenApiSchema schema = CreateSchemaFor<SingleRequiredRecord>();

      // Act
      SchemaFilterContext context = CreateFilterContext<SingleRequiredRecord>();
      filter.Apply(schema, context);

      // Assert - "Name" property should be added as "name" (camelCase)
      Assert.That(schema.Required, Does.Contain("name"));
      Assert.That(schema.Required, Does.Not.Contain("Name"));
    }

    [Test]
    public void Apply_RecordWithAllOptionalParameters_AddsNoRequired()
    {
      // Arrange
      OpenApiSchema schema = CreateSchemaFor<AllOptionalRecord>();
      int initialRequiredCount = schema.Required?.Count ?? 0;

      // Act
      SchemaFilterContext context = CreateFilterContext<AllOptionalRecord>();
      filter.Apply(schema, context);

      // Assert
      int finalRequiredCount = schema.Required?.Count ?? 0;
      Assert.That(finalRequiredCount, Is.EqualTo(initialRequiredCount));
    }

    [Test]
    public void Apply_RecordWithMixedParameters_OnlyMarksRequiredOnes()
    {
      // Arrange
      OpenApiSchema schema = CreateSchemaFor<MixedRecord>();

      // Act
      SchemaFilterContext context = CreateFilterContext<MixedRecord>();
      filter.Apply(schema, context);

      // Assert
      Assert.That(schema.Required, Does.Contain("name"));
      Assert.That(schema.Required, Does.Not.Contain("optionalDescription"));
    }

    [Test]
    public void Apply_RecordWithMultipleRequiredParameters_MarksAllAsRequired()
    {
      // Arrange
      OpenApiSchema schema = CreateSchemaFor<MultipleRequiredRecord>();

      // Act
      SchemaFilterContext context = CreateFilterContext<MultipleRequiredRecord>();
      filter.Apply(schema, context);

      // Assert
      Assert.That(schema.Required, Does.Contain("name"));
      Assert.That(schema.Required, Does.Contain("email"));
    }

    [Test]
    public void Apply_RecordWithNoRequiredAttribute_DoesNotMarkAsRequired()
    {
      // Arrange
      OpenApiSchema schema = CreateSchemaFor<NoRequiredRecord>();
      int initialRequiredCount = schema.Required?.Count ?? 0;

      // Act
      SchemaFilterContext context = CreateFilterContext<NoRequiredRecord>();
      filter.Apply(schema, context);

      // Assert - Required set should not have "name" added
      int finalRequiredCount = schema.Required?.Count ?? 0;
      Assert.That(finalRequiredCount, Is.EqualTo(initialRequiredCount));
    }

    [Test]
    public void Apply_RecordWithRequiredParameter_MarksAsRequired()
    {
      // Arrange
      OpenApiSchema schema = CreateSchemaFor<SingleRequiredRecord>();

      // Act
      SchemaFilterContext context = CreateFilterContext<SingleRequiredRecord>();
      filter.Apply(schema, context);

      // Assert
      Assert.That(schema.Required, Does.Contain("name"));
    }

    [Test]
    public void Apply_RegularClass_DoesNotModifySchema()
    {
      // Arrange
      OpenApiSchema schema = CreateSchemaFor<RegularClass>();
      int initialRequiredCount = schema.Required?.Count ?? 0;

      // Act
      SchemaFilterContext context = CreateFilterContext<RegularClass>();
      filter.Apply(schema, context);

      // Assert
      int finalRequiredCount = schema.Required?.Count ?? 0;
      Assert.That(finalRequiredCount, Is.EqualTo(initialRequiredCount));
    }

    [Test]
    public void Apply_SchemaWithNullRequired_DoesNotThrow()
    {
      // Arrange
      OpenApiSchema schema = new()
      {
        Required = null!
      };

      // Act & Assert
      SchemaFilterContext context = CreateFilterContext<SingleRequiredRecord>();
      Assert.DoesNotThrow(() => filter.Apply(schema, context));
    }

    [Test]
    public void Apply_ValueType_ReturnsEarlyWithoutModification()
    {
      // Arrange
      OpenApiSchema schema = CreateSchemaFor<TestStruct>();
      int initialRequiredCount = schema.Required?.Count ?? 0;

      // Act
      SchemaFilterContext context = CreateFilterContext<TestStruct>();
      filter.Apply(schema, context);

      // Assert - structs are value types, filter should skip them
      int finalRequiredCount = schema.Required?.Count ?? 0;
      Assert.That(finalRequiredCount, Is.EqualTo(initialRequiredCount));
    }
  }