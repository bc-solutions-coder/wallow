using FluentAssertions;

namespace Wallow.E2E.Tests.Infrastructure;

public class TotpHelperTests
{
    // Valid base32 secret (RFC 4648) - "JBSWY3DPEHPK3PXP" is a well-known test vector
    private const string TestSecret = "JBSWY3DPEHPK3PXP";

    [Fact]
    public void GenerateCode_WithValidSecret_ReturnsExactlySixDigits()
    {
        string code = TotpHelper.GenerateCode(TestSecret);

        code.Should().NotBeNull();
        code.Should().HaveLength(6);
        code.Should().MatchRegex(@"^\d{6}$");
    }

    [Fact]
    public void GenerateCode_CalledTwiceWithinSameWindow_ReturnsSameCode()
    {
        string code1 = TotpHelper.GenerateCode(TestSecret);
        string code2 = TotpHelper.GenerateCode(TestSecret);

        code1.Should().Be(code2);
    }

    [Fact]
    public void GenerateCode_WithEmptySecret_Throws()
    {
        Action act = () => TotpHelper.GenerateCode(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateCode_WithInvalidBase32Characters_Throws()
    {
        Action act = () => TotpHelper.GenerateCode("!!!invalid!!!");

        act.Should().Throw<ArgumentException>();
    }
}
