using Wallow.Identity.Application.Exceptions;

namespace Wallow.Identity.Infrastructure.Scim;

// Visitor pattern base for AST traversal
public interface IScimFilterVisitor<out T>
{
    T Visit(ComparisonNode node);
    T Visit(LogicalNode node);
    T Visit(NotNode node);
    T Visit(PresenceNode node);
}

// AST node base
public abstract record ScimFilterNode
{
    public abstract T Accept<T>(IScimFilterVisitor<T> visitor);
}

// userName eq "john"
public sealed record ComparisonNode(
    string AttributePath,
    string Operator,
    object Value) : ScimFilterNode
{
    public override T Accept<T>(IScimFilterVisitor<T> visitor) => visitor.Visit(this);
}

// a eq "1" and b eq "2"
public sealed record LogicalNode(
    ScimFilterNode Left,
    string Operator,
    ScimFilterNode Right) : ScimFilterNode
{
    public override T Accept<T>(IScimFilterVisitor<T> visitor) => visitor.Visit(this);
}

// not (userName eq "john")
public sealed record NotNode(
    ScimFilterNode InnerExpression) : ScimFilterNode
{
    public override T Accept<T>(IScimFilterVisitor<T> visitor) => visitor.Visit(this);
}

// emails pr (presence check)
public sealed record PresenceNode(
    string AttributePath) : ScimFilterNode
{
    public override T Accept<T>(IScimFilterVisitor<T> visitor) => visitor.Visit(this);
}

public sealed class ScimFilterParser
{
    private IReadOnlyList<ScimToken> _tokens = null!;
    private int _position;

    public ScimFilterNode Parse(IReadOnlyList<ScimToken> tokens)
    {
        if (tokens == null || tokens.Count == 0)
        {
            throw new ScimFilterException("Cannot parse empty token list");
        }

        _tokens = tokens;
        _position = 0;

        ScimFilterNode result = ParseOrExpression();

        if (_position < _tokens.Count)
        {
            throw new ScimFilterException($"Unexpected token '{CurrentToken.Value}'", CurrentToken.Position);
        }

        return result;
    }

    // Precedence: or (lowest) < and < not < comparison (highest)
    // or expression: and_expr ('or' and_expr)*
    private ScimFilterNode ParseOrExpression()
    {
        ScimFilterNode left = ParseAndExpression();

        while (!IsAtEnd && CurrentToken is { Type: TokenType.Logic, Value: "or" })
        {
            Consume(TokenType.Logic, "or");
            ScimFilterNode right = ParseAndExpression();
            left = new LogicalNode(left, "or", right);
        }

        return left;
    }

    // and expression: not_expr ('and' not_expr)*
    private ScimFilterNode ParseAndExpression()
    {
        ScimFilterNode left = ParseNotExpression();

        while (!IsAtEnd && CurrentToken is { Type: TokenType.Logic, Value: "and" })
        {
            Consume(TokenType.Logic, "and");
            ScimFilterNode right = ParseNotExpression();
            left = new LogicalNode(left, "and", right);
        }

        return left;
    }

    // not expression: 'not' primary | primary
    private ScimFilterNode ParseNotExpression()
    {
        if (!IsAtEnd && CurrentToken is { Type: TokenType.Logic, Value: "not" })
        {
            Consume(TokenType.Logic, "not");
            ScimFilterNode inner = ParsePrimary();
            return new NotNode(inner);
        }

        return ParsePrimary();
    }

    // primary: '(' or_expr ')' | comparison
    private ScimFilterNode ParsePrimary()
    {
        if (!IsAtEnd && CurrentToken.Type == TokenType.Lparen)
        {
            Consume(TokenType.Lparen);
            ScimFilterNode expr = ParseOrExpression();
            Consume(TokenType.Rparen);
            return expr;
        }

        return ParseComparison();
    }

    // comparison: attr op value | attr 'pr'
    private ScimFilterNode ParseComparison()
    {
        if (IsAtEnd || CurrentToken.Type != TokenType.Attr)
        {
            throw new ScimFilterException(
                $"Expected attribute path, got '{(IsAtEnd ? "EOF" : CurrentToken.Value)}'",
                IsAtEnd ? -1 : CurrentToken.Position);
        }

        string attrPath = CurrentToken.Value;
        Advance();

        if (IsAtEnd || CurrentToken.Type != TokenType.Op)
        {
            throw new ScimFilterException(
                $"Expected operator after attribute '{attrPath}', got '{(IsAtEnd ? "EOF" : CurrentToken.Value)}'",
                IsAtEnd ? -1 : CurrentToken.Position);
        }

        string op = CurrentToken.Value;
        Advance();

        // Special case: 'pr' (presence) has no value operand
        if (op == "pr")
        {
            return new PresenceNode(attrPath);
        }

        // All other operators require a value
        if (IsAtEnd)
        {
            throw new ScimFilterException($"Expected value after operator '{op}'", -1);
        }

        object value;
        if (CurrentToken.Type == TokenType.String)
        {
            value = CurrentToken.Value;
            Advance();
        }
        else if (CurrentToken.Type == TokenType.Bool)
        {
            value = CurrentToken.Value == "true";
            Advance();
        }
        else
        {
            throw new ScimFilterException(
                $"Expected string or boolean value, got '{CurrentToken.Value}'",
                CurrentToken.Position);
        }

        return new ComparisonNode(attrPath, op, value);
    }

    private ScimToken CurrentToken => _tokens[_position];
    private bool IsAtEnd => _position >= _tokens.Count;

    private void Advance()
    {
        if (!IsAtEnd)
        {
            _position++;
        }
    }

    private void Consume(TokenType expectedType, string? expectedValue = null)
    {
        if (IsAtEnd)
        {
            throw new ScimFilterException($"Expected {expectedType}, got EOF", -1);
        }

        if (CurrentToken.Type != expectedType)
        {
            throw new ScimFilterException(
                $"Expected {expectedType}, got {CurrentToken.Type}",
                CurrentToken.Position);
        }

        if (expectedValue != null && CurrentToken.Value != expectedValue)
        {
            throw new ScimFilterException(
                $"Expected '{expectedValue}', got '{CurrentToken.Value}'",
                CurrentToken.Position);
        }

        Advance();
    }
}
