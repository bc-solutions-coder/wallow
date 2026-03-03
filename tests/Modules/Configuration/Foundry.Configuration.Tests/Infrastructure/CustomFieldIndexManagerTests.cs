using Foundry.Configuration.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Foundry.Configuration.Tests.Infrastructure;

public class CustomFieldIndexManagerValidationTests
{
    private readonly CustomFieldIndexManager _manager;

    public CustomFieldIndexManagerValidationTests()
    {
        ILogger<CustomFieldIndexManager> logger = Substitute.For<ILogger<CustomFieldIndexManager>>();
        _manager = new CustomFieldIndexManager(logger);
    }

    [Theory]
    [InlineData("valid_table")]
    [InlineData("_starts_underscore")]
    [InlineData("Table123")]
    [InlineData("A")]
    public async Task CreateExpressionIndexAsync_WithValidIdentifiers_DoesNotThrowArgumentException(string identifier)
    {
        // We can only test the validation path since we don't have a real DbContext.
        // The valid identifiers should pass validation but fail on the DB call.
        // Testing with all three parameters as valid, but the DB call will throw
        // since we don't have a real DbContext. We verify no ArgumentException.
        Func<Task> act = async () => await _manager.CreateExpressionIndexAsync(
            null!, identifier, "schema", "field_key");

        // Should NOT throw ArgumentException (will throw NullReferenceException from null DbContext)
        ArgumentException? argEx = null;
        try { await act(); } catch (ArgumentException ex) { argEx = ex; } catch (Exception) { /* Expected: null DbContext throws non-ArgumentException */ }
        argEx.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("1starts_with_digit")]
    [InlineData("has-hyphen")]
    [InlineData("has space")]
    [InlineData("has.dot")]
    [InlineData("has;semicolon")]
    [InlineData("Robert'); DROP TABLE students;--")]
    public async Task CreateExpressionIndexAsync_WithInvalidTableName_ThrowsArgumentException(string tableName)
    {
        Func<Task> act = async () => await _manager.CreateExpressionIndexAsync(
            null!, tableName, "valid_schema", "valid_field");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid SQL identifier*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1starts_with_digit")]
    [InlineData("has;semicolon")]
    public async Task CreateExpressionIndexAsync_WithInvalidSchemaName_ThrowsArgumentException(string schemaName)
    {
        Func<Task> act = async () => await _manager.CreateExpressionIndexAsync(
            null!, "valid_table", schemaName, "valid_field");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid SQL identifier*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1bad")]
    [InlineData("has-hyphen")]
    public async Task CreateExpressionIndexAsync_WithInvalidFieldKey_ThrowsArgumentException(string fieldKey)
    {
        Func<Task> act = async () => await _manager.CreateExpressionIndexAsync(
            null!, "valid_table", "valid_schema", fieldKey);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid SQL identifier*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1starts_with_digit")]
    [InlineData("Robert'); DROP TABLE students;--")]
    public async Task DropExpressionIndexAsync_WithInvalidTableName_ThrowsArgumentException(string tableName)
    {
        Func<Task> act = async () => await _manager.DropExpressionIndexAsync(
            null!, tableName, "valid_schema", "valid_field");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid SQL identifier*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("has;semicolon")]
    public async Task DropExpressionIndexAsync_WithInvalidSchemaName_ThrowsArgumentException(string schemaName)
    {
        Func<Task> act = async () => await _manager.DropExpressionIndexAsync(
            null!, "valid_table", schemaName, "valid_field");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid SQL identifier*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1bad")]
    public async Task DropExpressionIndexAsync_WithInvalidFieldKey_ThrowsArgumentException(string fieldKey)
    {
        Func<Task> act = async () => await _manager.DropExpressionIndexAsync(
            null!, "valid_table", "valid_schema", fieldKey);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid SQL identifier*");
    }

    [Fact]
    public async Task CreateExpressionIndexAsync_WithIdentifierTooLong_ThrowsArgumentException()
    {
        string longIdentifier = new string('a', 64); // max is 63

        Func<Task> act = async () => await _manager.CreateExpressionIndexAsync(
            null!, longIdentifier, "valid_schema", "valid_field");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid SQL identifier*");
    }

    [Fact]
    public async Task CreateExpressionIndexAsync_WithMaxLengthIdentifier_DoesNotThrowArgumentException()
    {
        string maxIdentifier = new string('a', 63); // exactly 63 is OK

        // Should pass validation (will fail on null DbContext, but not ArgumentException)
        ArgumentException? argEx = null;
        try
        {
            await _manager.CreateExpressionIndexAsync(null!, maxIdentifier, "schema", "field");
        }
        catch (ArgumentException ex) { argEx = ex; }
        catch (Exception) { /* Expected: null DbContext throws non-ArgumentException */ }

        argEx.Should().BeNull();
    }
}
