using System.Globalization;
using Foundry.Identity.Application.DTOs;

namespace Foundry.Identity.Infrastructure.Scim;

public sealed record KeycloakQueryParams(
    string? Username = null,
    string? Email = null,
    string? FirstName = null,
    string? LastName = null,
    string? Search = null,
    Func<ScimUser, bool>? InMemoryFilter = null);

public sealed class ScimToKeycloakTranslator
{
    private readonly ScimFilterLexer _lexer = new();
    private readonly ScimFilterParser _parser = new();

    public KeycloakQueryParams Translate(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return new KeycloakQueryParams();
        }

        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize(filter);
        ScimFilterNode ast = _parser.Parse(tokens);
        ScimToKeycloakVisitor visitor = new();
        return ast.Accept(visitor);
    }

    private sealed class ScimToKeycloakVisitor : IScimFilterVisitor<KeycloakQueryParams>
    {
        public KeycloakQueryParams Visit(ComparisonNode node)
        {
            string normalizedAttr = NormalizeAttributePath(node.AttributePath);
            string op = node.Operator;
            string value = Convert.ToString(node.Value, CultureInfo.InvariantCulture) ?? string.Empty;

            // Keycloak only supports exact match on username, email, firstName, lastName
            // For "eq" on supported attributes, we can use direct query params
            if (op == "eq")
            {
                return normalizedAttr switch
                {
                    "username" => new KeycloakQueryParams(Username: value),
                    "email" => new KeycloakQueryParams(Email: value),
                    "firstname" => new KeycloakQueryParams(FirstName: value),
                    "lastname" => new KeycloakQueryParams(LastName: value),
                    _ => CreateInMemoryFilter(node)
                };
            }

            // For "co" (contains), use Keycloak's search parameter if it's a common field
            if (op == "co")
            {
                if (normalizedAttr is "username" or "email" or "firstname" or "lastname")
                {
                    return new KeycloakQueryParams(Search: value);
                }
            }

            // For all other operators or unsupported attributes, use in-memory filtering
            return CreateInMemoryFilter(node);
        }

        public KeycloakQueryParams Visit(LogicalNode node)
        {
            KeycloakQueryParams left = node.Left.Accept(this);
            KeycloakQueryParams right = node.Right.Accept(this);

            // If either side requires in-memory filtering, combine them
            if (left.InMemoryFilter != null || right.InMemoryFilter != null)
            {
                return CombineWithInMemoryFilter(left, right, node.Operator);
            }

            // If both sides are simple Keycloak params, we can only use one at a time
            // Keycloak doesn't support complex AND/OR queries via query params
            // So we must fall back to in-memory filtering
            if (HasMultipleKeycloakParams(left) || HasMultipleKeycloakParams(right) ||
                (HasAnyKeycloakParam(left) && HasAnyKeycloakParam(right)))
            {
                return CombineWithInMemoryFilter(left, right, node.Operator);
            }

            // Merge simple params (only one side has a param)
            return MergeParams(left, right);
        }

        public KeycloakQueryParams Visit(NotNode node)
        {
            // Keycloak doesn't support negation in query params
            // Must use in-memory filtering
            KeycloakQueryParams inner = node.InnerExpression.Accept(this);
            return new KeycloakQueryParams(
                InMemoryFilter: user => !EvaluateFilter(user, node.InnerExpression));
        }

        public KeycloakQueryParams Visit(PresenceNode node)
        {
            // Keycloak doesn't support presence checks in query params
            // Use in-memory filtering
            string normalizedAttr = NormalizeAttributePath(node.AttributePath);
            return new KeycloakQueryParams(
                InMemoryFilter: user => normalizedAttr switch
                {
                    "username" => !string.IsNullOrWhiteSpace(user.UserName),
                    "email" => user.Emails?.Any() == true,
                    "firstname" => !string.IsNullOrWhiteSpace(user.Name?.GivenName),
                    "lastname" => !string.IsNullOrWhiteSpace(user.Name?.FamilyName),
                    _ => false
                });
        }

        private static string NormalizeAttributePath(string attributePath)
        {
            // Map SCIM attribute paths to lowercase for consistent matching
            return attributePath.ToLowerInvariant() switch
            {
                "username" => "username",
                "emails.value" => "email",
                "emails[type eq \"work\"].value" => "email",
                "emails[primary eq true].value" => "email",
                "name.givenname" => "firstname",
                "name.familyname" => "lastname",
                _ => attributePath.ToLowerInvariant()
            };
        }

        private static KeycloakQueryParams CreateInMemoryFilter(ComparisonNode node)
        {
            return new KeycloakQueryParams(
                InMemoryFilter: user => EvaluateComparison(user, node));
        }

        private static bool EvaluateComparison(ScimUser user, ComparisonNode node)
        {
            string normalizedAttr = NormalizeAttributePath(node.AttributePath);
            string value = Convert.ToString(node.Value, CultureInfo.InvariantCulture) ?? string.Empty;

            string? userValue = normalizedAttr switch
            {
                "username" => user.UserName,
                "email" => user.Emails is { Count: > 0 } emails ? emails[0].Value : null,
                "firstname" => user.Name?.GivenName,
                "lastname" => user.Name?.FamilyName,
                "active" => user.Active.ToString().ToLowerInvariant(),
                _ => null
            };

            if (userValue == null)
            {
                return false;
            }

            return node.Operator switch
            {
                "eq" => userValue.Equals(value, StringComparison.OrdinalIgnoreCase),
                "ne" => !userValue.Equals(value, StringComparison.OrdinalIgnoreCase),
                "co" => userValue.Contains(value, StringComparison.OrdinalIgnoreCase),
                "sw" => userValue.StartsWith(value, StringComparison.OrdinalIgnoreCase),
                "ew" => userValue.EndsWith(value, StringComparison.OrdinalIgnoreCase),
                "gt" => string.Compare(userValue, value, StringComparison.Ordinal) > 0,
                "ge" => string.Compare(userValue, value, StringComparison.Ordinal) >= 0,
                "lt" => string.Compare(userValue, value, StringComparison.Ordinal) < 0,
                "le" => string.Compare(userValue, value, StringComparison.Ordinal) <= 0,
                _ => false
            };
        }

        private static bool EvaluateFilter(ScimUser user, ScimFilterNode node)
        {
            return node switch
            {
                ComparisonNode c => EvaluateComparison(user, c),
                LogicalNode l => l.Operator == "and"
                    ? EvaluateFilter(user, l.Left) && EvaluateFilter(user, l.Right)
                    : EvaluateFilter(user, l.Left) || EvaluateFilter(user, l.Right),
                NotNode n => !EvaluateFilter(user, n.InnerExpression),
                PresenceNode p => NormalizeAttributePath(p.AttributePath) switch
                {
                    "username" => !string.IsNullOrWhiteSpace(user.UserName),
                    "email" => user.Emails?.Any() == true,
                    "firstname" => !string.IsNullOrWhiteSpace(user.Name?.GivenName),
                    "lastname" => !string.IsNullOrWhiteSpace(user.Name?.FamilyName),
                    _ => false
                },
                _ => false
            };
        }

        private static KeycloakQueryParams CombineWithInMemoryFilter(
            KeycloakQueryParams left,
            KeycloakQueryParams right,
            string logicOp)
        {
            // When combining filters with in-memory components, we must fetch all users
            // and filter in-memory
            Func<ScimUser, bool> combinedFilter = logicOp == "and"
                ? user => ApplyFilter(user, left) && ApplyFilter(user, right)
                : user => ApplyFilter(user, left) || ApplyFilter(user, right);

            return new KeycloakQueryParams(InMemoryFilter: combinedFilter);
        }

        private static bool ApplyFilter(ScimUser user, KeycloakQueryParams queryParams)
        {
            if (queryParams.InMemoryFilter != null)
            {
                return queryParams.InMemoryFilter(user);
            }

            // If it's a simple Keycloak param, check if the user matches
            bool matches = true;

            if (queryParams.Username != null)
            {
                matches = matches && user.UserName.Equals(queryParams.Username, StringComparison.OrdinalIgnoreCase);
            }

            if (queryParams.Email != null)
            {
                matches = matches && user.Emails?.Any(e => e.Value.Equals(queryParams.Email, StringComparison.OrdinalIgnoreCase)) == true;
            }

            if (queryParams.FirstName != null)
            {
                matches = matches && user.Name?.GivenName?.Equals(queryParams.FirstName, StringComparison.OrdinalIgnoreCase) == true;
            }

            if (queryParams.LastName != null)
            {
                matches = matches && user.Name?.FamilyName?.Equals(queryParams.LastName, StringComparison.OrdinalIgnoreCase) == true;
            }

            if (queryParams.Search != null)
            {
                matches = matches && (
                    user.UserName.Contains(queryParams.Search, StringComparison.OrdinalIgnoreCase) ||
                    user.Emails?.Any(e => e.Value.Contains(queryParams.Search, StringComparison.OrdinalIgnoreCase)) == true ||
                    user.Name?.GivenName?.Contains(queryParams.Search, StringComparison.OrdinalIgnoreCase) == true ||
                    user.Name?.FamilyName?.Contains(queryParams.Search, StringComparison.OrdinalIgnoreCase) == true);
            }

            return matches;
        }

        private static bool HasMultipleKeycloakParams(KeycloakQueryParams queryParams)
        {
            int count = 0;
            if (queryParams.Username != null)
            {
                count++;
            }

            if (queryParams.Email != null)
            {
                count++;
            }

            if (queryParams.FirstName != null)
            {
                count++;
            }

            if (queryParams.LastName != null)
            {
                count++;
            }

            if (queryParams.Search != null)
            {
                count++;
            }

            return count > 1;
        }

        private static bool HasAnyKeycloakParam(KeycloakQueryParams queryParams)
        {
            return queryParams.Username != null ||
                   queryParams.Email != null ||
                   queryParams.FirstName != null ||
                   queryParams.LastName != null ||
                   queryParams.Search != null;
        }

        private static KeycloakQueryParams MergeParams(KeycloakQueryParams left, KeycloakQueryParams right)
        {
            return new KeycloakQueryParams(
                Username: left.Username ?? right.Username,
                Email: left.Email ?? right.Email,
                FirstName: left.FirstName ?? right.FirstName,
                LastName: left.LastName ?? right.LastName,
                Search: left.Search ?? right.Search,
                InMemoryFilter: left.InMemoryFilter ?? right.InMemoryFilter);
        }
    }
}
