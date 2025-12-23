using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Heartbeat.Server.Tests.OpenApi;

/// <summary>
/// Tests for RecordSchemaFilter which marks record positional parameters
/// with [Required] attribute as required in OpenAPI schema.
/// </summary>
[TestFixture]
public class RecordSchemaFilterTests
{
    private RecordSchemaFilter _filter = null!;
    private SchemaGenerator _schemaGenerator = null!;
    private SchemaRepository _schemaRepository = null!;

    [SetUp]
    public void SetUp()
    {
        _filter = new RecordSchemaFilter();
        _schemaRepository = new SchemaRepository();
        
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        _schemaGenerator = new SchemaGenerator(
            new SchemaGeneratorOptions(),
            new JsonSerializerDataContractResolver(serializerOptions));
    }

    #region Test Types

    // Record with single required parameter
    public record SingleRequiredRecord([property: Required] string Name);

    // Record with no required parameters
    public record NoRequiredRecord(string Name);

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
    public record EmptyRecord();

    // Record with nested types
    public record NestedRecord([property: Required] string Id, SingleRequiredRecord? Child);

    #endregion

    #region Required Parameter Detection

    [Test]
    public void Apply_RecordWithRequiredParameter_MarksAsRequired()
    {
        // Arrange
        var schema = CreateSchemaFor<SingleRequiredRecord>();

        // Act
        var context = CreateFilterContext<SingleRequiredRecord>();
        _filter.Apply(schema, context);

        // Assert
        Assert.That(schema.Required, Does.Contain("name"));
    }

    [Test]
    public void Apply_RecordWithMultipleRequiredParameters_MarksAllAsRequired()
    {
        // Arrange
        var schema = CreateSchemaFor<MultipleRequiredRecord>();

        // Act
        var context = CreateFilterContext<MultipleRequiredRecord>();
        _filter.Apply(schema, context);

        // Assert
        Assert.That(schema.Required, Does.Contain("name"));
        Assert.That(schema.Required, Does.Contain("email"));
    }

    [Test]
    public void Apply_RecordWithMixedParameters_OnlyMarksRequiredOnes()
    {
        // Arrange
        var schema = CreateSchemaFor<MixedRecord>();

        // Act
        var context = CreateFilterContext<MixedRecord>();
        _filter.Apply(schema, context);

        // Assert
        Assert.That(schema.Required, Does.Contain("name"));
        Assert.That(schema.Required, Does.Not.Contain("optionalDescription"));
    }

    #endregion

    #region Non-Required Parameters

    [Test]
    public void Apply_RecordWithNoRequiredAttribute_DoesNotMarkAsRequired()
    {
        // Arrange
        var schema = CreateSchemaFor<NoRequiredRecord>();
        var initialRequiredCount = schema.Required?.Count ?? 0;

        // Act
        var context = CreateFilterContext<NoRequiredRecord>();
        _filter.Apply(schema, context);

        // Assert - Required set should not have "name" added
        var finalRequiredCount = schema.Required?.Count ?? 0;
        Assert.That(finalRequiredCount, Is.EqualTo(initialRequiredCount));
    }

    [Test]
    public void Apply_RecordWithAllOptionalParameters_AddsNoRequired()
    {
        // Arrange
        var schema = CreateSchemaFor<AllOptionalRecord>();
        var initialRequiredCount = schema.Required?.Count ?? 0;

        // Act
        var context = CreateFilterContext<AllOptionalRecord>();
        _filter.Apply(schema, context);

        // Assert
        var finalRequiredCount = schema.Required?.Count ?? 0;
        Assert.That(finalRequiredCount, Is.EqualTo(initialRequiredCount));
    }

    #endregion

    #region Non-Record Types

    [Test]
    public void Apply_RegularClass_DoesNotModifySchema()
    {
        // Arrange
        var schema = CreateSchemaFor<RegularClass>();
        var initialRequiredCount = schema.Required?.Count ?? 0;

        // Act
        var context = CreateFilterContext<RegularClass>();
        _filter.Apply(schema, context);

        // Assert
        var finalRequiredCount = schema.Required?.Count ?? 0;
        Assert.That(finalRequiredCount, Is.EqualTo(initialRequiredCount));
    }

    [Test]
    public void Apply_ValueType_ReturnsEarlyWithoutModification()
    {
        // Arrange
        var schema = CreateSchemaFor<TestStruct>();
        var initialRequiredCount = schema.Required?.Count ?? 0;

        // Act
        var context = CreateFilterContext<TestStruct>();
        _filter.Apply(schema, context);

        // Assert - structs are value types, filter should skip them
        var finalRequiredCount = schema.Required?.Count ?? 0;
        Assert.That(finalRequiredCount, Is.EqualTo(initialRequiredCount));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void Apply_EmptyRecord_DoesNotThrow()
    {
        // Arrange
        var schema = CreateSchemaFor<EmptyRecord>();

        // Act & Assert - should not throw
        var context = CreateFilterContext<EmptyRecord>();
        Assert.DoesNotThrow(() => _filter.Apply(schema, context));
    }

    [Test]
    public void Apply_SchemaWithNullRequired_DoesNotThrow()
    {
        // Arrange
        var schema = new OpenApiSchema();
        schema.Required = null!;

        // Act & Assert
        var context = CreateFilterContext<SingleRequiredRecord>();
        Assert.DoesNotThrow(() => _filter.Apply(schema, context));
    }

    #endregion

    #region Property Name Conversion

    [Test]
    public void Apply_PropertyName_ConvertsToCamelCase()
    {
        // Arrange
        var schema = CreateSchemaFor<SingleRequiredRecord>();

        // Act
        var context = CreateFilterContext<SingleRequiredRecord>();
        _filter.Apply(schema, context);

        // Assert - "Name" property should be added as "name" (camelCase)
        Assert.That(schema.Required, Does.Contain("name"));
        Assert.That(schema.Required, Does.Not.Contain("Name"));
    }

    #endregion

    #region Helper Methods

    private OpenApiSchema CreateSchemaFor<T>()
    {
        // Generate the schema - this registers it in the repository
        _schemaGenerator.GenerateSchema(typeof(T), _schemaRepository);
        
        // Get the schema by type name from the repository
        var typeName = typeof(T).Name;
        if (_schemaRepository.Schemas.TryGetValue(typeName, out var schema) && schema is OpenApiSchema concreteSchema)
        {
            return concreteSchema;
        }
        
        // Fallback for inline schemas (primitives, etc.)
        return new OpenApiSchema();
    }

    private SchemaFilterContext CreateFilterContext<T>()
    {
        return new SchemaFilterContext(
            typeof(T),
            _schemaGenerator,
            _schemaRepository);
    }

    #endregion
}

