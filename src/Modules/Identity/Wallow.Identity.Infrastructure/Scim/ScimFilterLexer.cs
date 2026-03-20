using System.Text;
using Wallow.Identity.Application.Exceptions;

namespace Wallow.Identity.Infrastructure.Scim;

public sealed class ScimFilterLexer
{
    private static readonly HashSet<string> _operators =
    [
        "eq", "ne", "co", "sw", "ew", "gt", "ge", "lt", "le", "pr"
    ];

    private static readonly HashSet<string> _logicOperators =
    [
        "and", "or", "not"
    ];

    private static readonly HashSet<string> _boolValues =
    [
        "true", "false"
    ];

    public IReadOnlyList<ScimToken> Tokenize(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return [];
        }

        List<ScimToken> tokens = [];
        int position = 0;

        while (position < filter.Length)
        {
            char ch = filter[position];

            if (char.IsWhiteSpace(ch))
            {
                position++;
                continue;
            }

            if (ch == '(')
            {
                tokens.Add(new ScimToken(TokenType.Lparen, "(", position));
                position++;
                continue;
            }

            if (ch == ')')
            {
                tokens.Add(new ScimToken(TokenType.Rparen, ")", position));
                position++;
                continue;
            }

            if (ch == '"')
            {
                (string stringValue, int newPosition) = ParseString(filter, position);
                tokens.Add(new ScimToken(TokenType.String, stringValue, position));
                position = newPosition;
                continue;
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                (string identifier, int newPosition) = ParseIdentifier(filter, position);
                string lowerIdentifier = identifier.ToLowerInvariant();

                if (_operators.Contains(lowerIdentifier))
                {
                    tokens.Add(new ScimToken(TokenType.Op, lowerIdentifier, position));
                }
                else if (_logicOperators.Contains(lowerIdentifier))
                {
                    tokens.Add(new ScimToken(TokenType.Logic, lowerIdentifier, position));
                }
                else if (_boolValues.Contains(lowerIdentifier))
                {
                    tokens.Add(new ScimToken(TokenType.Bool, lowerIdentifier, position));
                }
                else
                {
                    tokens.Add(new ScimToken(TokenType.Attr, identifier, position));
                }

                position = newPosition;
                continue;
            }

            throw new ScimFilterException($"Unexpected character '{ch}'", position);
        }

        return tokens;
    }

    private static (string Value, int Position) ParseString(string filter, int position)
    {
        if (filter[position] != '"')
        {
            throw new ScimFilterException("Expected string to start with double quote", position);
        }

        StringBuilder sb = new();
        position++; // Skip opening quote

        while (position < filter.Length)
        {
            char ch = filter[position];

            if (ch == '\\' && position + 1 < filter.Length)
            {
                position++;
                char nextChar = filter[position];

                switch (nextChar)
                {
                    case '"':
                        sb.Append('"');
                        break;
                    case '\\':
                        sb.Append('\\');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    default:
                        sb.Append(nextChar);
                        break;
                }
                position++;
            }
            else if (ch == '"')
            {
                position++; // Skip closing quote
                return (sb.ToString(), position);
            }
            else
            {
                sb.Append(ch);
                position++;
            }
        }

        throw new ScimFilterException("Unterminated string literal", position);
    }

    private static (string Value, int Position) ParseIdentifier(string filter, int position)
    {
        StringBuilder sb = new();

        while (position < filter.Length)
        {
            char ch = filter[position];

            // Support attribute paths like emails.value, name.givenName
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '.')
            {
                sb.Append(ch);
                position++;
            }
            else
            {
                break;
            }
        }

        return (sb.ToString(), position);
    }
}
