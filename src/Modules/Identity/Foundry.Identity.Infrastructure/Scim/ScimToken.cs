namespace Foundry.Identity.Infrastructure.Scim;

public enum TokenType
{
    Attr,
    Op,
    Logic,
    Lparen,
    Rparen,
    String,
    Bool
}

public sealed record ScimToken(TokenType Type, string Value, int Position);
