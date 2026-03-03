using Foundry.Identity.Infrastructure.Scim;

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimFilterParserTests
{
    private readonly ScimFilterLexer _lexer = new();
    private readonly ScimFilterParser _parser = new();

    private ScimFilterNode Parse(string filter)
    {
        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize(filter);
        return _parser.Parse(tokens);
    }

    [Fact]
    public void Parse_EmptyTokenList_ThrowsException()
    {
        // Arrange
        IReadOnlyList<ScimToken> tokens = Array.Empty<ScimToken>();

        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(tokens));
        exception.Message.Should().Contain("Cannot parse empty token list");
    }

    [Fact]
    public void Parse_NullTokenList_ThrowsException()
    {
        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(null!));
        exception.Message.Should().Contain("Cannot parse empty token list");
    }

    [Fact]
    public void Parse_SimpleComparison_Equals_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("userName eq \"john\"");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("userName");
        node.Operator.Should().Be("eq");
        node.Value.Should().Be("john");
    }

    [Fact]
    public void Parse_SimpleComparison_NotEquals_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("status ne \"inactive\"");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("status");
        node.Operator.Should().Be("ne");
        node.Value.Should().Be("inactive");
    }

    [Fact]
    public void Parse_SimpleComparison_Contains_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("email co \"@example.com\"");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("email");
        node.Operator.Should().Be("co");
        node.Value.Should().Be("@example.com");
    }

    [Fact]
    public void Parse_SimpleComparison_StartsWith_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("userName sw \"admin\"");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("userName");
        node.Operator.Should().Be("sw");
        node.Value.Should().Be("admin");
    }

    [Fact]
    public void Parse_SimpleComparison_EndsWith_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("fileName ew \".pdf\"");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("fileName");
        node.Operator.Should().Be("ew");
        node.Value.Should().Be(".pdf");
    }

    [Fact]
    public void Parse_SimpleComparison_GreaterThan_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("age gt \"18\"");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("age");
        node.Operator.Should().Be("gt");
        node.Value.Should().Be("18");
    }

    [Fact]
    public void Parse_SimpleComparison_GreaterOrEqual_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("age ge \"21\"");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("age");
        node.Operator.Should().Be("ge");
        node.Value.Should().Be("21");
    }

    [Fact]
    public void Parse_SimpleComparison_LessThan_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("age lt \"65\"");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("age");
        node.Operator.Should().Be("lt");
        node.Value.Should().Be("65");
    }

    [Fact]
    public void Parse_SimpleComparison_LessOrEqual_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("age le \"64\"");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("age");
        node.Operator.Should().Be("le");
        node.Value.Should().Be("64");
    }

    [Fact]
    public void Parse_Presence_ReturnsPresenceNode()
    {
        // Act
        ScimFilterNode result = Parse("emails pr");

        // Assert
        result.Should().BeOfType<PresenceNode>();
        PresenceNode node = (PresenceNode)result;
        node.AttributePath.Should().Be("emails");
    }

    [Fact]
    public void Parse_Comparison_WithBooleanValue_True_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("active eq true");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("active");
        node.Operator.Should().Be("eq");
        node.Value.Should().Be(true);
    }

    [Fact]
    public void Parse_Comparison_WithBooleanValue_False_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("active eq false");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("active");
        node.Operator.Should().Be("eq");
        node.Value.Should().Be(false);
    }

    [Fact]
    public void Parse_LogicalAnd_ReturnsLogicalNode()
    {
        // Act
        ScimFilterNode result = Parse("a eq \"1\" and b eq \"2\"");

        // Assert
        result.Should().BeOfType<LogicalNode>();
        LogicalNode node = (LogicalNode)result;
        node.Operator.Should().Be("and");

        node.Left.Should().BeOfType<ComparisonNode>();
        ComparisonNode left = (ComparisonNode)node.Left;
        left.AttributePath.Should().Be("a");
        left.Value.Should().Be("1");

        node.Right.Should().BeOfType<ComparisonNode>();
        ComparisonNode right = (ComparisonNode)node.Right;
        right.AttributePath.Should().Be("b");
        right.Value.Should().Be("2");
    }

    [Fact]
    public void Parse_LogicalOr_ReturnsLogicalNode()
    {
        // Act
        ScimFilterNode result = Parse("a eq \"1\" or b eq \"2\"");

        // Assert
        result.Should().BeOfType<LogicalNode>();
        LogicalNode node = (LogicalNode)result;
        node.Operator.Should().Be("or");

        node.Left.Should().BeOfType<ComparisonNode>();
        ComparisonNode left = (ComparisonNode)node.Left;
        left.AttributePath.Should().Be("a");
        left.Value.Should().Be("1");

        node.Right.Should().BeOfType<ComparisonNode>();
        ComparisonNode right = (ComparisonNode)node.Right;
        right.AttributePath.Should().Be("b");
        right.Value.Should().Be("2");
    }

    [Fact]
    public void Parse_Not_ReturnsNotNode()
    {
        // Act
        ScimFilterNode result = Parse("not userName eq \"john\"");

        // Assert
        result.Should().BeOfType<NotNode>();
        NotNode node = (NotNode)result;

        node.InnerExpression.Should().BeOfType<ComparisonNode>();
        ComparisonNode inner = (ComparisonNode)node.InnerExpression;
        inner.AttributePath.Should().Be("userName");
        inner.Value.Should().Be("john");
    }

    [Fact]
    public void Parse_Not_WithParentheses_ReturnsNotNode()
    {
        // Act
        ScimFilterNode result = Parse("not (userName eq \"john\")");

        // Assert
        result.Should().BeOfType<NotNode>();
        NotNode node = (NotNode)result;

        node.InnerExpression.Should().BeOfType<ComparisonNode>();
        ComparisonNode inner = (ComparisonNode)node.InnerExpression;
        inner.AttributePath.Should().Be("userName");
        inner.Value.Should().Be("john");
    }

    [Fact]
    public void Parse_Precedence_AndBeforeOr_ReturnsCorrectStructure()
    {
        // a eq "1" and b eq "2" or c eq "3"
        // Should be: (a and b) or c
        // Act
        ScimFilterNode result = Parse("a eq \"1\" and b eq \"2\" or c eq \"3\"");

        // Assert
        result.Should().BeOfType<LogicalNode>();
        LogicalNode orNode = (LogicalNode)result;
        orNode.Operator.Should().Be("or");

        // Left side should be "a and b"
        orNode.Left.Should().BeOfType<LogicalNode>();
        LogicalNode andNode = (LogicalNode)orNode.Left;
        andNode.Operator.Should().Be("and");

        ComparisonNode a = (ComparisonNode)andNode.Left;
        a.AttributePath.Should().Be("a");
        a.Value.Should().Be("1");

        ComparisonNode b = (ComparisonNode)andNode.Right;
        b.AttributePath.Should().Be("b");
        b.Value.Should().Be("2");

        // Right side should be "c eq 3"
        orNode.Right.Should().BeOfType<ComparisonNode>();
        ComparisonNode c = (ComparisonNode)orNode.Right;
        c.AttributePath.Should().Be("c");
        c.Value.Should().Be("3");
    }

    [Fact]
    public void Parse_Precedence_MultipleAnd_AssociatesLeft()
    {
        // a eq "1" and b eq "2" and c eq "3"
        // Should be: (a and b) and c
        // Act
        ScimFilterNode result = Parse("a eq \"1\" and b eq \"2\" and c eq \"3\"");

        // Assert
        result.Should().BeOfType<LogicalNode>();
        LogicalNode outerAnd = (LogicalNode)result;
        outerAnd.Operator.Should().Be("and");

        // Left side should be "a and b"
        outerAnd.Left.Should().BeOfType<LogicalNode>();
        LogicalNode innerAnd = (LogicalNode)outerAnd.Left;
        innerAnd.Operator.Should().Be("and");

        ComparisonNode a = (ComparisonNode)innerAnd.Left;
        a.AttributePath.Should().Be("a");

        ComparisonNode b = (ComparisonNode)innerAnd.Right;
        b.AttributePath.Should().Be("b");

        // Right side should be "c eq 3"
        ComparisonNode c = (ComparisonNode)outerAnd.Right;
        c.AttributePath.Should().Be("c");
    }

    [Fact]
    public void Parse_Precedence_MultipleOr_AssociatesLeft()
    {
        // a eq "1" or b eq "2" or c eq "3"
        // Should be: (a or b) or c
        // Act
        ScimFilterNode result = Parse("a eq \"1\" or b eq \"2\" or c eq \"3\"");

        // Assert
        result.Should().BeOfType<LogicalNode>();
        LogicalNode outerOr = (LogicalNode)result;
        outerOr.Operator.Should().Be("or");

        // Left side should be "a or b"
        outerOr.Left.Should().BeOfType<LogicalNode>();
        LogicalNode innerOr = (LogicalNode)outerOr.Left;
        innerOr.Operator.Should().Be("or");

        ComparisonNode a = (ComparisonNode)innerOr.Left;
        a.AttributePath.Should().Be("a");

        ComparisonNode b = (ComparisonNode)innerOr.Right;
        b.AttributePath.Should().Be("b");

        // Right side should be "c eq 3"
        ComparisonNode c = (ComparisonNode)outerOr.Right;
        c.AttributePath.Should().Be("c");
    }

    [Fact]
    public void Parse_ParenthesesOverride_OrBeforeAnd_ReturnsCorrectStructure()
    {
        // (a eq "1" or b eq "2") and c eq "3"
        // Parentheses force or to be evaluated first
        // Act
        ScimFilterNode result = Parse("(a eq \"1\" or b eq \"2\") and c eq \"3\"");

        // Assert
        result.Should().BeOfType<LogicalNode>();
        LogicalNode andNode = (LogicalNode)result;
        andNode.Operator.Should().Be("and");

        // Left side should be "a or b"
        andNode.Left.Should().BeOfType<LogicalNode>();
        LogicalNode orNode = (LogicalNode)andNode.Left;
        orNode.Operator.Should().Be("or");

        ComparisonNode a = (ComparisonNode)orNode.Left;
        a.AttributePath.Should().Be("a");
        a.Value.Should().Be("1");

        ComparisonNode b = (ComparisonNode)orNode.Right;
        b.AttributePath.Should().Be("b");
        b.Value.Should().Be("2");

        // Right side should be "c eq 3"
        andNode.Right.Should().BeOfType<ComparisonNode>();
        ComparisonNode c = (ComparisonNode)andNode.Right;
        c.AttributePath.Should().Be("c");
        c.Value.Should().Be("3");
    }

    [Fact]
    public void Parse_NestedParentheses_ReturnsCorrectStructure()
    {
        // ((a eq "1"))
        // Act
        ScimFilterNode result = Parse("((a eq \"1\"))");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("a");
        node.Value.Should().Be("1");
    }

    [Fact]
    public void Parse_ComplexNestedExpression_ReturnsCorrectStructure()
    {
        // (a eq "1" or b eq "2") and (c eq "3" or d eq "4")
        // Act
        ScimFilterNode result = Parse("(a eq \"1\" or b eq \"2\") and (c eq \"3\" or d eq \"4\")");

        // Assert
        result.Should().BeOfType<LogicalNode>();
        LogicalNode andNode = (LogicalNode)result;
        andNode.Operator.Should().Be("and");

        // Left: (a or b)
        andNode.Left.Should().BeOfType<LogicalNode>();
        LogicalNode leftOr = (LogicalNode)andNode.Left;
        leftOr.Operator.Should().Be("or");

        ComparisonNode a = (ComparisonNode)leftOr.Left;
        a.AttributePath.Should().Be("a");

        ComparisonNode b = (ComparisonNode)leftOr.Right;
        b.AttributePath.Should().Be("b");

        // Right: (c or d)
        andNode.Right.Should().BeOfType<LogicalNode>();
        LogicalNode rightOr = (LogicalNode)andNode.Right;
        rightOr.Operator.Should().Be("or");

        ComparisonNode c = (ComparisonNode)rightOr.Left;
        c.AttributePath.Should().Be("c");

        ComparisonNode d = (ComparisonNode)rightOr.Right;
        d.AttributePath.Should().Be("d");
    }

    [Fact]
    public void Parse_NotWithLogicalExpression_ReturnsCorrectStructure()
    {
        // not (a eq "1" and b eq "2")
        // Act
        ScimFilterNode result = Parse("not (a eq \"1\" and b eq \"2\")");

        // Assert
        result.Should().BeOfType<NotNode>();
        NotNode notNode = (NotNode)result;

        notNode.InnerExpression.Should().BeOfType<LogicalNode>();
        LogicalNode andNode = (LogicalNode)notNode.InnerExpression;
        andNode.Operator.Should().Be("and");

        ComparisonNode a = (ComparisonNode)andNode.Left;
        a.AttributePath.Should().Be("a");

        ComparisonNode b = (ComparisonNode)andNode.Right;
        b.AttributePath.Should().Be("b");
    }

    [Fact]
    public void Parse_MultipleNots_WithParentheses_ReturnsCorrectStructure()
    {
        // not (not (a eq "1"))
        // Act
        ScimFilterNode result = Parse("not (not (a eq \"1\"))");

        // Assert
        result.Should().BeOfType<NotNode>();
        NotNode outerNot = (NotNode)result;

        outerNot.InnerExpression.Should().BeOfType<NotNode>();
        NotNode innerNot = (NotNode)outerNot.InnerExpression;

        innerNot.InnerExpression.Should().BeOfType<ComparisonNode>();
        ComparisonNode comparison = (ComparisonNode)innerNot.InnerExpression;
        comparison.AttributePath.Should().Be("a");
    }

    [Fact]
    public void Parse_AttributeWithDotNotation_ReturnsComparisonNode()
    {
        // Act
        ScimFilterNode result = Parse("emails.value eq \"test@example.com\"");

        // Assert
        result.Should().BeOfType<ComparisonNode>();
        ComparisonNode node = (ComparisonNode)result;
        node.AttributePath.Should().Be("emails.value");
        node.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Parse_RealWorldFilter_EmailDomain_ReturnsCorrectStructure()
    {
        // Act
        ScimFilterNode result = Parse("emails.value co \"@example.com\" and active eq true");

        // Assert
        result.Should().BeOfType<LogicalNode>();
        LogicalNode andNode = (LogicalNode)result;
        andNode.Operator.Should().Be("and");

        ComparisonNode email = (ComparisonNode)andNode.Left;
        email.AttributePath.Should().Be("emails.value");
        email.Operator.Should().Be("co");
        email.Value.Should().Be("@example.com");

        ComparisonNode active = (ComparisonNode)andNode.Right;
        active.AttributePath.Should().Be("active");
        active.Value.Should().Be(true);
    }

    [Fact]
    public void Parse_RealWorldFilter_UserSearch_ReturnsCorrectStructure()
    {
        // Act
        ScimFilterNode result = Parse("(userName sw \"admin\" or displayName co \"Admin\") and active eq true");

        // Assert
        result.Should().BeOfType<LogicalNode>();
        LogicalNode andNode = (LogicalNode)result;

        andNode.Left.Should().BeOfType<LogicalNode>();
        LogicalNode orNode = (LogicalNode)andNode.Left;
        orNode.Operator.Should().Be("or");

        ComparisonNode userName = (ComparisonNode)orNode.Left;
        userName.AttributePath.Should().Be("userName");
        userName.Operator.Should().Be("sw");

        ComparisonNode displayName = (ComparisonNode)orNode.Right;
        displayName.AttributePath.Should().Be("displayName");
        displayName.Operator.Should().Be("co");
    }

    [Fact]
    public void Parse_MissingAttributePath_ThrowsException()
    {
        // Arrange
        List<ScimToken> tokens = new List<ScimToken>
        {
            new(TokenType.Op, "eq", 0),
            new(TokenType.String, "value", 3)
        };

        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(tokens));
        exception.Message.Should().Contain("Expected attribute path");
    }

    [Fact]
    public void Parse_MissingOperator_ThrowsException()
    {
        // Arrange
        List<ScimToken> tokens = new List<ScimToken>
        {
            new(TokenType.Attr, "userName", 0)
        };

        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(tokens));
        exception.Message.Should().Contain("Expected operator");
    }

    [Fact]
    public void Parse_MissingValue_ThrowsException()
    {
        // Arrange
        List<ScimToken> tokens = new List<ScimToken>
        {
            new(TokenType.Attr, "userName", 0),
            new(TokenType.Op, "eq", 9)
        };

        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(tokens));
        exception.Message.Should().Contain("Expected value after operator");
    }

    [Fact]
    public void Parse_UnmatchedLeftParenthesis_ThrowsException()
    {
        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => Parse("(userName eq \"john\""));
        exception.Message.Should().Contain("Expected Rparen");
    }

    [Fact]
    public void Parse_UnmatchedRightParenthesis_ThrowsException()
    {
        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => Parse("userName eq \"john\")"));
        exception.Message.Should().Contain("Unexpected token");
    }

    [Fact]
    public void Parse_InvalidValueType_ThrowsException()
    {
        // Arrange - operator followed by operator instead of value
        List<ScimToken> tokens = new List<ScimToken>
        {
            new(TokenType.Attr, "userName", 0),
            new(TokenType.Op, "eq", 9),
            new(TokenType.Op, "ne", 12)
        };

        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(tokens));
        exception.Message.Should().Contain("Expected string or boolean value");
    }

    [Fact]
    public void Parse_MissingRightOperand_ThrowsException()
    {
        // Arrange
        List<ScimToken> tokens = new List<ScimToken>
        {
            new(TokenType.Attr, "a", 0),
            new(TokenType.Op, "eq", 2),
            new(TokenType.String, "1", 5),
            new(TokenType.Logic, "and", 9)
        };

        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(tokens));
        exception.Message.Should().Contain("Expected attribute path");
    }

    [Fact]
    public void Parse_NotWithoutExpression_ThrowsException()
    {
        // Arrange
        List<ScimToken> tokens = new List<ScimToken>
        {
            new(TokenType.Logic, "not", 0)
        };

        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(tokens));
        exception.Message.Should().Contain("Expected attribute path");
    }

    [Fact]
    public void Parse_EmptyParentheses_ThrowsException()
    {
        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => Parse("()"));
        exception.Message.Should().Contain("Expected attribute path");
    }

    [Fact]
    public void Parse_AttributeFollowedByLogic_WithoutComparison_ThrowsException()
    {
        // Arrange
        List<ScimToken> tokens = new List<ScimToken>
        {
            new(TokenType.Attr, "userName", 0),
            new(TokenType.Logic, "and", 9)
        };

        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(tokens));
        exception.Message.Should().Contain("Expected operator");
    }

    [Fact]
    public void Parse_TrailingTokens_ThrowsException()
    {
        // Arrange
        List<ScimToken> tokens = new List<ScimToken>
        {
            new(TokenType.Attr, "a", 0),
            new(TokenType.Op, "eq", 2),
            new(TokenType.String, "1", 5),
            new(TokenType.Attr, "b", 9)
        };

        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(tokens));
        exception.Message.Should().Contain("Unexpected token");
    }

    [Fact]
    public void Parse_PresenceWithValue_ThrowsException()
    {
        // Presence operator should not be followed by a value, but parser will consume it as trailing token
        // Arrange
        List<ScimToken> tokens = new List<ScimToken>
        {
            new(TokenType.Attr, "emails", 0),
            new(TokenType.Op, "pr", 7),
            new(TokenType.String, "invalid", 10)
        };

        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => _parser.Parse(tokens));
        exception.Message.Should().Contain("Unexpected token");
    }

    [Fact]
    public void Parse_ComplexMalformedExpression_ThrowsException()
    {
        // Act & Assert
        ScimFilterException exception = Assert.Throws<ScimFilterException>(() => Parse("a eq and b eq \"2\""));
        exception.Message.Should().Contain("Expected string or boolean value");
    }
}
