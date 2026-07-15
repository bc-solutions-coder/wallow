using Serilog.Core;
using Serilog.Events;
using Wallow.Api.Logging;

namespace Wallow.Api.Tests.Logging;

public class PiiDestructuringPolicyTests
{
    private readonly PiiDestructuringPolicy _sut = new();
    private readonly ILogEventPropertyValueFactory _factory = Substitute.For<ILogEventPropertyValueFactory>();

    public PiiDestructuringPolicyTests()
    {
        _factory.CreatePropertyValue(Arg.Any<object?>(), Arg.Any<bool>())
            .Returns(callInfo => new ScalarValue(callInfo.Arg<object?>()));
    }

    [Fact]
    public void TryDestructure_WithSensitiveProperties_RedactsEmail()
    {
        object value = new { Email = "user@test.com", Name = "John" };

        bool handled = _sut.TryDestructure(value, _factory, out LogEventPropertyValue result);

        handled.Should().BeTrue();
        StructureValue structure = result.Should().BeOfType<StructureValue>().Subject;
        LogEventProperty emailProp = structure.Properties.First(p => p.Name == "Email");
        emailProp.Value.ToString().Should().Be("\"[REDACTED]\"");
    }

    [Fact]
    public void TryDestructure_WithSensitiveProperties_RedactsPassword()
    {
        object value = new { Password = "secret123", Username = "admin" };

        bool handled = _sut.TryDestructure(value, _factory, out LogEventPropertyValue result);

        handled.Should().BeTrue();
        StructureValue structure = (StructureValue)result;
        LogEventProperty passwordProp = structure.Properties.First(p => p.Name == "Password");
        passwordProp.Value.ToString().Should().Be("\"[REDACTED]\"");
    }

    [Fact]
    public void TryDestructure_WithTokenProperty_RedactsIt()
    {
        object value = new { Token = "abc123", Name = "ok" };

        bool handled = _sut.TryDestructure(value, _factory, out LogEventPropertyValue result);

        handled.Should().BeTrue();
        StructureValue structure = (StructureValue)result;
        structure.Properties.First(p => p.Name == "Token").Value.ToString().Should().Be("\"[REDACTED]\"");
    }

    [Fact]
    public void TryDestructure_WithAccessAndRefreshTokens_RedactsBoth()
    {
        object value = new { AccessToken = "at", RefreshToken = "rt" };

        bool handled = _sut.TryDestructure(value, _factory, out LogEventPropertyValue result);

        handled.Should().BeTrue();
        StructureValue structure = (StructureValue)result;
        structure.Properties.First(p => p.Name == "AccessToken").Value.ToString().Should().Be("\"[REDACTED]\"");
        structure.Properties.First(p => p.Name == "RefreshToken").Value.ToString().Should().Be("\"[REDACTED]\"");
    }

    [Fact]
    public void TryDestructure_WithCreditCardAndSsn_RedactsBoth()
    {
        object value = new { CreditCard = "4111", Ssn = "123-45-6789" };

        bool handled = _sut.TryDestructure(value, _factory, out LogEventPropertyValue result);

        handled.Should().BeTrue();
        StructureValue structure = (StructureValue)result;
        structure.Properties.First(p => p.Name == "CreditCard").Value.ToString().Should().Be("\"[REDACTED]\"");
        structure.Properties.First(p => p.Name == "Ssn").Value.ToString().Should().Be("\"[REDACTED]\"");
    }

    [Fact]
    public void TryDestructure_WithPhoneNumberAndSecret_RedactsBoth()
    {
        object value = new { PhoneNumber = "555-1234", Secret = "s3cr3t" };

        bool handled = _sut.TryDestructure(value, _factory, out LogEventPropertyValue result);

        handled.Should().BeTrue();
        StructureValue structure = (StructureValue)result;
        structure.Properties.First(p => p.Name == "PhoneNumber").Value.ToString().Should().Be("\"[REDACTED]\"");
        structure.Properties.First(p => p.Name == "Secret").Value.ToString().Should().Be("\"[REDACTED]\"");
    }

    [Fact]
    public void TryDestructure_WithNoSensitiveProperties_ReturnsFalse()
    {
        object value = new { Name = "John", Age = 30 };

        bool handled = _sut.TryDestructure(value, _factory, out LogEventPropertyValue _);

        handled.Should().BeFalse();
    }

    [Theory]
    [InlineData("hello")]
    [InlineData(42)]
    [InlineData(true)]
    [InlineData(3.14)]
    public void TryDestructure_WithPrimitiveOrString_ReturnsFalse(object value)
    {
        bool handled = _sut.TryDestructure(value, _factory, out LogEventPropertyValue _);

        handled.Should().BeFalse();
    }

    [Fact]
    public void TryDestructure_WithDecimal_ReturnsFalse()
    {
        bool handled = _sut.TryDestructure(99.99m, _factory, out LogEventPropertyValue _);

        handled.Should().BeFalse();
    }

    [Fact]
    public void TryDestructure_WithEnum_ReturnsFalse()
    {
        bool handled = _sut.TryDestructure(DayOfWeek.Monday, _factory, out LogEventPropertyValue _);

        handled.Should().BeFalse();
    }

    [Fact]
    public void TryDestructure_PreservesNonSensitiveProperties()
    {
        object value = new { Email = "user@test.com", Name = "John" };

        _sut.TryDestructure(value, _factory, out LogEventPropertyValue result);

        StructureValue structure = (StructureValue)result;
        LogEventProperty nameProp = structure.Properties.First(p => p.Name == "Name");
        nameProp.Value.ToString().Should().Be("\"John\"");
    }

    [Fact]
    public void TryDestructure_IsCaseInsensitive()
    {
        object value = new { email = "user@test.com", Name = "John" };

        bool handled = _sut.TryDestructure(value, _factory, out LogEventPropertyValue result);

        handled.Should().BeTrue();
        StructureValue structure = (StructureValue)result;
        LogEventProperty emailProp = structure.Properties.First(p => p.Name == "email");
        emailProp.Value.ToString().Should().Be("\"[REDACTED]\"");
    }

    [Fact]
    public void TryDestructure_WithPropertyThrowingException_RecordsError()
    {
        ThrowingObject value = new();

        bool handled = _sut.TryDestructure(value, _factory, out LogEventPropertyValue result);

        handled.Should().BeTrue();
        StructureValue structure = (StructureValue)result;
        LogEventProperty nameProp = structure.Properties.First(p => p.Name == "Name");
        nameProp.Value.ToString().Should().Be("\"Error reading property\"");
    }

    [Fact]
    public void TryDestructure_SetsTypeTagOnResult()
    {
        object value = new SensitiveDto { Email = "test@test.com" };

        _sut.TryDestructure(value, _factory, out LogEventPropertyValue result);

        StructureValue structure = (StructureValue)result;
        structure.TypeTag.Should().Be(nameof(SensitiveDto));
    }

    private sealed class SensitiveDto
    {
        public string Email { get; set; } = "";
    }

    private sealed class ThrowingObject
    {
        public string Email { get; set; } = "test@test.com";
        public string Name => throw new InvalidOperationException("Cannot read");
    }

}
