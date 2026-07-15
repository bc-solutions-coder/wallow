using Wallow.Shared.Kernel.CustomFields;

namespace Wallow.Shared.Kernel.Tests.CustomFields;

public class FieldValidationRulesTests
{
    [Fact]
    public void Constructor_Default_AllPropertiesAreNull()
    {
        FieldValidationRules rules = new();

        rules.MinLength.Should().BeNull();
        rules.MaxLength.Should().BeNull();
        rules.Min.Should().BeNull();
        rules.Max.Should().BeNull();
        rules.Pattern.Should().BeNull();
        rules.PatternMessage.Should().BeNull();
        rules.MinDate.Should().BeNull();
        rules.MaxDate.Should().BeNull();
    }

    [Fact]
    public void Init_TextValidation_SetsMinMaxLengthAndPattern()
    {
        FieldValidationRules rules = new()
        {
            MinLength = 5,
            MaxLength = 100,
            Pattern = @"^[a-zA-Z]+$",
            PatternMessage = "Only letters allowed"
        };

        rules.MinLength.Should().Be(5);
        rules.MaxLength.Should().Be(100);
        rules.Pattern.Should().Be(@"^[a-zA-Z]+$");
        rules.PatternMessage.Should().Be("Only letters allowed");
    }

    [Fact]
    public void Init_NumericValidation_SetsMinMax()
    {
        FieldValidationRules rules = new()
        {
            Min = 0.01m,
            Max = 999999.99m
        };

        rules.Min.Should().Be(0.01m);
        rules.Max.Should().Be(999999.99m);
    }

    [Fact]
    public void Init_DateValidation_SetsMinMaxDate()
    {
        DateTime minDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime maxDate = new(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        FieldValidationRules rules = new() { MinDate = minDate, MaxDate = maxDate };

        rules.MinDate.Should().Be(minDate);
        rules.MaxDate.Should().Be(maxDate);
    }

    [Fact]
    public void Init_AllProperties_SetsCorrectly()
    {
        DateTime minDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime maxDate = new(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        FieldValidationRules rules = new()
        {
            MinLength = 1,
            MaxLength = 500,
            Min = -100m,
            Max = 100m,
            Pattern = @"\d+",
            PatternMessage = "Digits only",
            MinDate = minDate,
            MaxDate = maxDate
        };

        rules.MinLength.Should().Be(1);
        rules.MaxLength.Should().Be(500);
        rules.Min.Should().Be(-100m);
        rules.Max.Should().Be(100m);
        rules.Pattern.Should().Be(@"\d+");
        rules.PatternMessage.Should().Be("Digits only");
        rules.MinDate.Should().Be(minDate);
        rules.MaxDate.Should().Be(maxDate);
    }

    [Fact]
    public void Init_MinLengthZero_SetsToZero()
    {
        FieldValidationRules rules = new() { MinLength = 0 };

        rules.MinLength.Should().Be(0);
    }

    [Fact]
    public void Init_NegativeMin_SetsNegativeValue()
    {
        FieldValidationRules rules = new() { Min = -999.99m };

        rules.Min.Should().Be(-999.99m);
    }

    [Fact]
    public void Init_EmptyPattern_SetsEmptyString()
    {
        FieldValidationRules rules = new() { Pattern = "" };

        rules.Pattern.Should().BeEmpty();
    }

    [Fact]
    public void Init_PatternMessageWithoutPattern_AllowsOrphanedMessage()
    {
        FieldValidationRules rules = new() { PatternMessage = "Some message" };

        rules.Pattern.Should().BeNull();
        rules.PatternMessage.Should().Be("Some message");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        FieldValidationRules rules1 = new() { MinLength = 1, MaxLength = 50, Pattern = @"\d+" };
        FieldValidationRules rules2 = new() { MinLength = 1, MaxLength = 50, Pattern = @"\d+" };

        rules1.Should().Be(rules2);
    }

    [Fact]
    public void Equality_BothDefault_AreEqual()
    {
        FieldValidationRules rules1 = new();
        FieldValidationRules rules2 = new();

        rules1.Should().Be(rules2);
    }

    [Fact]
    public void Equality_AllPropertiesMatch_AreEqual()
    {
        DateTime minDate = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime maxDate = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        FieldValidationRules rules1 = new()
        {
            MinLength = 3,
            MaxLength = 200,
            Min = 0m,
            Max = 1000m,
            Pattern = "^test$",
            PatternMessage = "Must be test",
            MinDate = minDate,
            MaxDate = maxDate
        };
        FieldValidationRules rules2 = new()
        {
            MinLength = 3,
            MaxLength = 200,
            Min = 0m,
            Max = 1000m,
            Pattern = "^test$",
            PatternMessage = "Must be test",
            MinDate = minDate,
            MaxDate = maxDate
        };

        rules1.Should().Be(rules2);
        rules1.GetHashCode().Should().Be(rules2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentMinLength_AreNotEqual()
    {
        FieldValidationRules rules1 = new() { MinLength = 1 };
        FieldValidationRules rules2 = new() { MinLength = 5 };

        rules1.Should().NotBe(rules2);
    }

    [Fact]
    public void Equality_DifferentMaxLength_AreNotEqual()
    {
        FieldValidationRules rules1 = new() { MaxLength = 50 };
        FieldValidationRules rules2 = new() { MaxLength = 100 };

        rules1.Should().NotBe(rules2);
    }

    [Fact]
    public void Equality_DifferentMin_AreNotEqual()
    {
        FieldValidationRules rules1 = new() { Min = 0m };
        FieldValidationRules rules2 = new() { Min = 1m };

        rules1.Should().NotBe(rules2);
    }

    [Fact]
    public void Equality_DifferentMax_AreNotEqual()
    {
        FieldValidationRules rules1 = new() { Max = 100m };
        FieldValidationRules rules2 = new() { Max = 200m };

        rules1.Should().NotBe(rules2);
    }

    [Fact]
    public void Equality_DifferentPattern_AreNotEqual()
    {
        FieldValidationRules rules1 = new() { Pattern = @"\d+" };
        FieldValidationRules rules2 = new() { Pattern = @"[a-z]+" };

        rules1.Should().NotBe(rules2);
    }

    [Fact]
    public void Equality_DifferentPatternMessage_AreNotEqual()
    {
        FieldValidationRules rules1 = new() { PatternMessage = "Digits" };
        FieldValidationRules rules2 = new() { PatternMessage = "Letters" };

        rules1.Should().NotBe(rules2);
    }

    [Fact]
    public void Equality_DifferentMinDate_AreNotEqual()
    {
        FieldValidationRules rules1 = new() { MinDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        FieldValidationRules rules2 = new() { MinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

        rules1.Should().NotBe(rules2);
    }

    [Fact]
    public void Equality_DifferentMaxDate_AreNotEqual()
    {
        FieldValidationRules rules1 = new() { MaxDate = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc) };
        FieldValidationRules rules2 = new() { MaxDate = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc) };

        rules1.Should().NotBe(rules2);
    }

    [Fact]
    public void Equality_NullVsSet_AreNotEqual()
    {
        FieldValidationRules rules1 = new();
        FieldValidationRules rules2 = new() { MinLength = 1 };

        rules1.Should().NotBe(rules2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        FieldValidationRules original = new() { MinLength = 5, MaxLength = 100 };

        FieldValidationRules modified = original with { MaxLength = 200 };

        original.MaxLength.Should().Be(100);
        modified.MaxLength.Should().Be(200);
        modified.MinLength.Should().Be(5);
    }

    [Fact]
    public void With_ResetPropertyToNull_SetsNull()
    {
        FieldValidationRules original = new() { MinLength = 5, Pattern = @"\d+" };

        FieldValidationRules modified = original with { Pattern = null };

        modified.MinLength.Should().Be(5);
        modified.Pattern.Should().BeNull();
    }

    [Fact]
    public void ToString_ReturnsRecordRepresentation()
    {
        FieldValidationRules rules = new() { MinLength = 1, MaxLength = 50 };

        string result = rules.ToString();

        result.Should().Contain("MinLength = 1");
        result.Should().Contain("MaxLength = 50");
    }
}
