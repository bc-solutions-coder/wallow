using System.Text.Json.Nodes;
using Wallow.Shared.Infrastructure.Workflows.AsyncApi;

namespace Wallow.Shared.Infrastructure.Tests.AsyncApi;

public class JsonSchemaGeneratorTests
{
    [Theory]
    [InlineData(typeof(string), "string", null)]
    [InlineData(typeof(int), "integer", null)]
    [InlineData(typeof(long), "integer", "int64")]
    [InlineData(typeof(decimal), "number", null)]
    [InlineData(typeof(double), "number", "double")]
    [InlineData(typeof(bool), "boolean", null)]
    [InlineData(typeof(Guid), "string", "uuid")]
    [InlineData(typeof(DateTime), "string", "date-time")]
    [InlineData(typeof(DateTimeOffset), "string", "date-time")]
    [InlineData(typeof(TimeSpan), "string", "duration")]
    public void GetPropertySchema_maps_primitive_types(Type type, string expectedType, string? expectedFormat)
    {
        JsonObject schema = JsonSchemaGenerator.GetPropertySchema(type);

        schema["type"]!.GetValue<string>().Should().Be(expectedType);
        if (expectedFormat is not null)
        {
            schema["format"]!.GetValue<string>().Should().Be(expectedFormat);
        }
        else
        {
            schema.ContainsKey("format").Should().BeFalse();
        }
    }

    [Fact]
    public void GetPropertySchema_nullable_returns_underlying_type()
    {
        JsonObject schema = JsonSchemaGenerator.GetPropertySchema(typeof(Guid?));

        schema["type"]!.GetValue<string>().Should().Be("string");
        schema["format"]!.GetValue<string>().Should().Be("uuid");
    }

    private sealed record TestEvent
    {
        // ReSharper disable once UnusedMember.Local
        public required Guid Id { get; init; }
        // ReSharper disable once UnusedMember.Local
        public required string Name { get; init; }
        // ReSharper disable once UnusedMember.Local
        public required decimal Amount { get; init; }
        // ReSharper disable once UnusedMember.Local
        public string? OptionalField { get; init; }
    }

    [Fact]
    public void GenerateSchema_creates_object_schema_with_properties_and_required()
    {
        JsonObject schema = JsonSchemaGenerator.GenerateSchema(typeof(TestEvent));

        schema["type"]!.GetValue<string>().Should().Be("object");

        JsonObject props = schema["properties"]!.AsObject();
        props.ContainsKey("id").Should().BeTrue();
        props.ContainsKey("name").Should().BeTrue();
        props.ContainsKey("amount").Should().BeTrue();
        props.ContainsKey("optionalField").Should().BeTrue();

        // id is Guid -> string/uuid
        props["id"]!["type"]!.GetValue<string>().Should().Be("string");
        props["id"]!["format"]!.GetValue<string>().Should().Be("uuid");

        // amount is decimal -> number
        props["amount"]!["type"]!.GetValue<string>().Should().Be("number");

        // required should include non-nullable properties only
        List<string> required = schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        required.Should().Contain("id");
        required.Should().Contain("name");
        required.Should().Contain("amount");
        required.Should().NotContain("optionalField");
    }

    private sealed record EventWithCollection
    {
        // ReSharper disable once UnusedMember.Local
        public required IReadOnlyList<LineItem> Items { get; init; }
    }

    private sealed record LineItem
    {
        // ReSharper disable once UnusedMember.Local
        public required Guid ItemId { get; init; }
        // ReSharper disable once UnusedMember.Local
        public required int Quantity { get; init; }
    }

    [Fact]
    public void GenerateSchema_handles_collection_properties_with_nested_objects()
    {
        JsonObject schema = JsonSchemaGenerator.GenerateSchema(typeof(EventWithCollection));

        JsonObject items = schema["properties"]!["items"]!.AsObject();
        items["type"]!.GetValue<string>().Should().Be("array");

        JsonObject itemSchema = items["items"]!.AsObject();
        itemSchema["type"]!.GetValue<string>().Should().Be("object");
        itemSchema["properties"]!.AsObject().ContainsKey("itemId").Should().BeTrue();
        itemSchema["properties"]!.AsObject().ContainsKey("quantity").Should().BeTrue();
    }
}
