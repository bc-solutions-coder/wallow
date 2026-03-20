using Wallow.Shared.Kernel.CustomFields;

namespace Wallow.Shared.Kernel.Tests.CustomFields;

public class CustomFieldValidationResultTests
{
    [Fact]
    public void Success_ReturnsIsValidTrue_WithNoErrors()
    {
        CustomFieldValidationResult result = CustomFieldValidationResult.Success();

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ReturnsIsValidFalse_WithErrors()
    {
        CustomFieldValidationError[] errors =
        [
            new("field_one", "Field one is required"),
            new("field_two", "Field two must be numeric")
        ];

        CustomFieldValidationResult result = CustomFieldValidationResult.Failure(errors);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors[0].FieldKey.Should().Be("field_one");
        result.Errors[0].Message.Should().Be("Field one is required");
        result.Errors[1].FieldKey.Should().Be("field_two");
        result.Errors[1].Message.Should().Be("Field two must be numeric");
    }

    [Fact]
    public void Failure_WithEmptyErrors_ReturnsIsValidTrue()
    {
        CustomFieldValidationResult result = CustomFieldValidationResult.Failure([]);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}

public class CustomFieldValidationErrorTests
{
    [Fact]
    public void Constructor_SetsFieldKeyAndMessage()
    {
        CustomFieldValidationError error = new("email", "Invalid email format");

        error.FieldKey.Should().Be("email");
        error.Message.Should().Be("Invalid email format");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        CustomFieldValidationError error1 = new("field", "message");
        CustomFieldValidationError error2 = new("field", "message");

        error1.Should().Be(error2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        CustomFieldValidationError error1 = new("field_a", "message a");
        CustomFieldValidationError error2 = new("field_b", "message b");

        error1.Should().NotBe(error2);
    }
}
