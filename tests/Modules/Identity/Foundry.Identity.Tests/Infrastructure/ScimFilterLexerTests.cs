using Foundry.Identity.Application.Exceptions;
using Foundry.Identity.Infrastructure.Scim;

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimFilterLexerTests
{
    private readonly ScimFilterLexer _lexer = new();

    [Fact]
    public void Tokenize_WithEmptyString_ReturnsEmptyList()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("");

        // Assert
        tokens.Should().BeEmpty();
    }

    [Fact]
    public void Tokenize_WithWhitespaceOnly_ReturnsEmptyList()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("   \t\n  ");

        // Assert
        tokens.Should().BeEmpty();
    }

    [Fact]
    public void Tokenize_SimpleEqualsComparison_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("userName eq \"john\"");

        // Assert
        tokens.Should().HaveCount(3);
        tokens[0].Should().Be(new ScimToken(TokenType.Attr, "userName", 0));
        tokens[1].Should().Be(new ScimToken(TokenType.Op, "eq", 9));
        tokens[2].Should().Be(new ScimToken(TokenType.String, "john", 12));
    }

    [Fact]
    public void Tokenize_AttributePath_WithDotNotation_ReturnsCorrectToken()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("emails.value eq \"test@example.com\"");

        // Assert
        tokens.Should().HaveCount(3);
        tokens[0].Type.Should().Be(TokenType.Attr);
        tokens[0].Value.Should().Be("emails.value");
    }

    [Fact]
    public void Tokenize_AttributePath_WithMultipleDots_ReturnsCorrectToken()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("name.givenName.value eq \"John\"");

        // Assert
        tokens.Should().HaveCount(3);
        tokens[0].Type.Should().Be(TokenType.Attr);
        tokens[0].Value.Should().Be("name.givenName.value");
    }

    [Theory]
    [InlineData("eq", "equals")]
    [InlineData("ne", "not equals")]
    [InlineData("co", "contains")]
    [InlineData("sw", "starts with")]
    [InlineData("ew", "ends with")]
    [InlineData("gt", "greater than")]
    [InlineData("ge", "greater or equal")]
    [InlineData("lt", "less than")]
    [InlineData("le", "less or equal")]
    [InlineData("pr", "present")]
    public void Tokenize_AllOperators_ReturnsCorrectTokenType(string op, string description)
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize($"attr {op} \"value\"");

        // Assert
        tokens.Should().Contain(t => t.Type == TokenType.Op && t.Value == op, description);
    }

    [Fact]
    public void Tokenize_PresentOperator_WithoutValue_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("emails pr");

        // Assert
        tokens.Should().HaveCount(2);
        tokens[0].Should().Be(new ScimToken(TokenType.Attr, "emails", 0));
        tokens[1].Should().Be(new ScimToken(TokenType.Op, "pr", 7));
    }

    [Theory]
    [InlineData("and")]
    [InlineData("or")]
    [InlineData("not")]
    public void Tokenize_LogicalOperators_ReturnsCorrectTokenType(string logicOp)
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize($"a eq \"1\" {logicOp} b eq \"2\"");

        // Assert
        tokens.Should().Contain(t => t.Type == TokenType.Logic && t.Value == logicOp);
    }

    [Fact]
    public void Tokenize_ComplexExpression_WithAndOperator_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("userName eq \"john\" and active eq true");

        // Assert
        tokens.Should().HaveCount(7);
        tokens[0].Should().Be(new ScimToken(TokenType.Attr, "userName", 0));
        tokens[1].Should().Be(new ScimToken(TokenType.Op, "eq", 9));
        tokens[2].Should().Be(new ScimToken(TokenType.String, "john", 12));
        tokens[3].Should().Be(new ScimToken(TokenType.Logic, "and", 19));
        tokens[4].Should().Be(new ScimToken(TokenType.Attr, "active", 23));
        tokens[5].Should().Be(new ScimToken(TokenType.Op, "eq", 30));
        tokens[6].Should().Be(new ScimToken(TokenType.Bool, "true", 33));
    }

    [Fact]
    public void Tokenize_NotOperator_AsPrefix_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("not active eq false");

        // Assert
        tokens.Should().HaveCount(4);
        tokens[0].Should().Be(new ScimToken(TokenType.Logic, "not", 0));
        tokens[1].Should().Be(new ScimToken(TokenType.Attr, "active", 4));
        tokens[2].Should().Be(new ScimToken(TokenType.Op, "eq", 11));
        tokens[3].Should().Be(new ScimToken(TokenType.Bool, "false", 14));
    }

    [Fact]
    public void Tokenize_Parentheses_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("(userName eq \"john\")");

        // Assert
        tokens.Should().HaveCount(5);
        tokens[0].Should().Be(new ScimToken(TokenType.Lparen, "(", 0));
        tokens[1].Should().Be(new ScimToken(TokenType.Attr, "userName", 1));
        tokens[2].Should().Be(new ScimToken(TokenType.Op, "eq", 10));
        tokens[3].Should().Be(new ScimToken(TokenType.String, "john", 13));
        tokens[4].Should().Be(new ScimToken(TokenType.Rparen, ")", 19));
    }

    [Fact]
    public void Tokenize_NestedParentheses_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("((a eq \"1\"))");

        // Assert
        tokens.Should().HaveCount(7);
        tokens[0].Should().Be(new ScimToken(TokenType.Lparen, "(", 0));
        tokens[1].Should().Be(new ScimToken(TokenType.Lparen, "(", 1));
        tokens[2].Should().Be(new ScimToken(TokenType.Attr, "a", 2));
        tokens[3].Should().Be(new ScimToken(TokenType.Op, "eq", 4));
        tokens[4].Should().Be(new ScimToken(TokenType.String, "1", 7));
        tokens[5].Should().Be(new ScimToken(TokenType.Rparen, ")", 10));
        tokens[6].Should().Be(new ScimToken(TokenType.Rparen, ")", 11));
    }

    [Fact]
    public void Tokenize_ComplexExpression_WithParenthesesAndLogic_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("(userName eq \"john\" or userName eq \"jane\") and active eq true");

        // Assert
        tokens.Should().HaveCount(13);
        tokens[0].Type.Should().Be(TokenType.Lparen);
        tokens[12].Type.Should().Be(TokenType.Bool);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void Tokenize_BooleanValues_ReturnsCorrectTokenType(string boolValue)
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize($"active eq {boolValue}");

        // Assert
        tokens.Should().Contain(t => t.Type == TokenType.Bool && t.Value == boolValue);
    }

    [Fact]
    public void Tokenize_BooleanValue_CaseInsensitive_ReturnsCorrectToken()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("active eq TRUE");

        // Assert
        tokens[2].Should().Be(new ScimToken(TokenType.Bool, "true", 10));
    }

    [Fact]
    public void Tokenize_Operator_CaseInsensitive_ReturnsCorrectToken()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("userName EQ \"john\"");

        // Assert
        tokens[1].Should().Be(new ScimToken(TokenType.Op, "eq", 9));
    }

    [Fact]
    public void Tokenize_LogicOperator_CaseInsensitive_ReturnsCorrectToken()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("a eq \"1\" AND b eq \"2\"");

        // Assert
        tokens[3].Should().Be(new ScimToken(TokenType.Logic, "and", 9));
    }

    [Fact]
    public void Tokenize_EscapedQuote_InString_ReturnsCorrectValue()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("userName eq \"john\\\"doe\"");

        // Assert
        tokens.Should().HaveCount(3);
        tokens[2].Type.Should().Be(TokenType.String);
        tokens[2].Value.Should().Be("john\"doe");
    }

    [Fact]
    public void Tokenize_EscapedBackslash_InString_ReturnsCorrectValue()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("path eq \"C:\\\\Users\\\\john\"");

        // Assert
        tokens[2].Type.Should().Be(TokenType.String);
        tokens[2].Value.Should().Be("C:\\Users\\john");
    }

    [Fact]
    public void Tokenize_EscapedNewline_InString_ReturnsCorrectValue()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("text eq \"line1\\nline2\"");

        // Assert
        tokens[2].Type.Should().Be(TokenType.String);
        tokens[2].Value.Should().Be("line1\nline2");
    }

    [Fact]
    public void Tokenize_EscapedTab_InString_ReturnsCorrectValue()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("text eq \"col1\\tcol2\"");

        // Assert
        tokens[2].Type.Should().Be(TokenType.String);
        tokens[2].Value.Should().Be("col1\tcol2");
    }

    [Fact]
    public void Tokenize_EscapedCarriageReturn_InString_ReturnsCorrectValue()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("text eq \"line1\\rline2\"");

        // Assert
        tokens[2].Type.Should().Be(TokenType.String);
        tokens[2].Value.Should().Be("line1\rline2");
    }

    [Fact]
    public void Tokenize_UnclosedQuote_ThrowsException()
    {
        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _lexer.Tokenize("userName eq \"john"));

        exception.Message.Should().Contain("Unterminated string literal");
        exception.Position.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Tokenize_InvalidCharacter_ThrowsException()
    {
        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _lexer.Tokenize("userName eq @"));

        exception.Message.Should().Contain("Unexpected character '@'");
        exception.Position.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Tokenize_InvalidCharacter_SpecialSymbol_ThrowsException()
    {
        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _lexer.Tokenize("userName $ \"john\""));

        exception.Message.Should().Contain("Unexpected character '$'");
    }

    [Fact]
    public void Tokenize_ExtraWhitespace_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("  userName   eq    \"john\"  ");

        // Assert
        tokens.Should().HaveCount(3);
        tokens[0].Value.Should().Be("userName");
        tokens[1].Value.Should().Be("eq");
        tokens[2].Value.Should().Be("john");
    }

    [Fact]
    public void Tokenize_NoWhitespace_BetweenTokens_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("userName eq\"john\"");

        // Assert
        tokens.Should().HaveCount(3);
        tokens[0].Value.Should().Be("userName");
        tokens[1].Value.Should().Be("eq");
        tokens[2].Value.Should().Be("john");
    }

    [Fact]
    public void Tokenize_AttributeWithNumbers_ReturnsCorrectToken()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("attr123 eq \"value\"");

        // Assert
        tokens[0].Type.Should().Be(TokenType.Attr);
        tokens[0].Value.Should().Be("attr123");
    }

    [Fact]
    public void Tokenize_AttributeWithUnderscore_ReturnsCorrectToken()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("user_name eq \"john\"");

        // Assert
        tokens[0].Type.Should().Be(TokenType.Attr);
        tokens[0].Value.Should().Be("user_name");
    }

    [Fact]
    public void Tokenize_EmptyString_Value_ReturnsCorrectToken()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("userName eq \"\"");

        // Assert
        tokens.Should().HaveCount(3);
        tokens[2].Type.Should().Be(TokenType.String);
        tokens[2].Value.Should().Be("");
    }

    [Fact]
    public void Tokenize_StringWithSpaces_ReturnsCorrectValue()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("displayName eq \"John Doe\"");

        // Assert
        tokens[2].Type.Should().Be(TokenType.String);
        tokens[2].Value.Should().Be("John Doe");
    }

    [Fact]
    public void Tokenize_ComplexRealWorldFilter_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("emails.value co \"@example.com\" and (active eq true or userName sw \"admin\")");

        // Assert
        tokens.Should().HaveCount(13);
        tokens[0].Value.Should().Be("emails.value");
        tokens[1].Value.Should().Be("co");
        tokens[2].Value.Should().Be("@example.com");
        tokens[3].Value.Should().Be("and");
        tokens[4].Value.Should().Be("(");
        tokens[5].Value.Should().Be("active");
        tokens[6].Value.Should().Be("eq");
        tokens[7].Value.Should().Be("true");
        tokens[8].Value.Should().Be("or");
        tokens[9].Value.Should().Be("userName");
        tokens[10].Value.Should().Be("sw");
        tokens[11].Value.Should().Be("admin");
        tokens[12].Value.Should().Be(")");
    }

    [Fact]
    public void Tokenize_AllTokenPositions_AreCorrect()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("a eq \"b\"");

        // Assert
        tokens[0].Position.Should().Be(0); // 'a' at position 0
        tokens[1].Position.Should().Be(2); // 'eq' at position 2
        tokens[2].Position.Should().Be(5); // '"b"' at position 5
    }

    [Fact]
    public void Tokenize_MultipleStrings_ReturnsCorrectTokens()
    {
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("a eq \"x\" and b eq \"y\"");

        // Assert
        tokens.Should().HaveCount(7);
        tokens[2].Type.Should().Be(TokenType.String);
        tokens[2].Value.Should().Be("x");
        tokens[6].Type.Should().Be(TokenType.String);
        tokens[6].Value.Should().Be("y");
    }

    [Fact]
    public void Tokenize_AttributeStartingWithOperatorName_ReturnsAttributeToken()
    {
        // Attribute name starts with 'eq' but is not the operator
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("equipment eq \"laptop\"");

        // Assert
        tokens[0].Type.Should().Be(TokenType.Attr);
        tokens[0].Value.Should().Be("equipment");
        tokens[1].Type.Should().Be(TokenType.Op);
        tokens[1].Value.Should().Be("eq");
    }

    [Fact]
    public void Tokenize_AttributeStartingWithLogicOperatorName_ReturnsAttributeToken()
    {
        // Attribute name starts with 'and' but is not the operator
        // Act
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize("android eq \"phone\"");

        // Assert
        tokens[0].Type.Should().Be(TokenType.Attr);
        tokens[0].Value.Should().Be("android");
    }
}
