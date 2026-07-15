using System.Globalization;
using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Infrastructure.Scim;

public sealed record ScimFilterParams(
    string? UserName = null,
    string? Email = null,
    string? FirstName = null,
    string? LastName = null,
    string? Search = null,
    Func<ScimUser, bool>? InMemoryFilter = null);

public sealed class ScimAttributeMapper
{
    private readonly ScimFilterLexer _lexer = new();
    private readonly ScimFilterParser _parser = new();

    public ScimFilterParams Translate(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return new ScimFilterParams();
        }

        IReadOnlyList<ScimToken> tokens = _lexer.Tokenize(filter);
        ScimFilterNode ast = _parser.Parse(tokens);
        ScimFilterVisitor visitor = new();
        return ast.Accept(visitor);
    }

    private sealed class ScimFilterVisitor : IScimFilterVisitor<ScimFilterParams>
    {
        public ScimFilterParams Visit(ComparisonNode node)
        {
            string normalizedAttr = NormalizeAttributePath(node.AttributePath);
            string op = node.Operator;
            string value = Convert.ToString(node.Value, CultureInfo.InvariantCulture) ?? string.Empty;

            if (op == "eq")
            {
                return normalizedAttr switch
                {
                    "username" => new ScimFilterParams(UserName: value),
                    "email" => new ScimFilterParams(Email: value),
                    "firstname" => new ScimFilterParams(FirstName: value),
                    "lastname" => new ScimFilterParams(LastName: value),
                    _ => CreateInMemoryFilter(node)
                };
            }

            if (op == "co")
            {
                if (normalizedAttr is "username" or "email" or "firstname" or "lastname")
                {
                    return new ScimFilterParams(Search: value);
                }
            }

            return CreateInMemoryFilter(node);
        }

        public ScimFilterParams Visit(LogicalNode node)
        {
            ScimFilterParams left = node.Left.Accept(this);
            ScimFilterParams right = node.Right.Accept(this);

            if (left.InMemoryFilter != null || right.InMemoryFilter != null)
            {
                return CombineWithInMemoryFilter(left, right, node.Operator);
            }

            if (HasMultipleParams(left) || HasMultipleParams(right) ||
                (HasAnyParam(left) && HasAnyParam(right)))
            {
                return CombineWithInMemoryFilter(left, right, node.Operator);
            }

            return MergeParams(left, right);
        }

        public ScimFilterParams Visit(NotNode node)
        {
            return new ScimFilterParams(
                InMemoryFilter: user => !EvaluateFilter(user, node.InnerExpression));
        }

        public ScimFilterParams Visit(PresenceNode node)
        {
            string normalizedAttr = NormalizeAttributePath(node.AttributePath);
            return new ScimFilterParams(
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

        private static ScimFilterParams CreateInMemoryFilter(ComparisonNode node)
        {
            return new ScimFilterParams(
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
                    ? EvaluateFilter(user, l.Left) && EvaluateFilter(user, l.Right) : EvaluateFilter(user, l.Left) || EvaluateFilter(user, l.Right),
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

        private static ScimFilterParams CombineWithInMemoryFilter(
            ScimFilterParams left,
            ScimFilterParams right,
            string logicOp)
        {
            Func<ScimUser, bool> combinedFilter = logicOp == "and"
                ? user => ApplyFilter(user, left) && ApplyFilter(user, right) : user => ApplyFilter(user, left) || ApplyFilter(user, right);

            return new ScimFilterParams(InMemoryFilter: combinedFilter);
        }

        private static bool ApplyFilter(ScimUser user, ScimFilterParams filterParams)
        {
            if (filterParams.InMemoryFilter != null)
            {
                return filterParams.InMemoryFilter(user);
            }

            bool matches = true;

            if (filterParams.UserName != null)
            {
                matches = matches && user.UserName.Equals(filterParams.UserName, StringComparison.OrdinalIgnoreCase);
            }

            if (filterParams.Email != null)
            {
                matches = matches && user.Emails?.Any(e => e.Value.Equals(filterParams.Email, StringComparison.OrdinalIgnoreCase)) == true;
            }

            if (filterParams.FirstName != null)
            {
                matches = matches && user.Name?.GivenName?.Equals(filterParams.FirstName, StringComparison.OrdinalIgnoreCase) == true;
            }

            if (filterParams.LastName != null)
            {
                matches = matches && user.Name?.FamilyName?.Equals(filterParams.LastName, StringComparison.OrdinalIgnoreCase) == true;
            }

            if (filterParams.Search != null)
            {
                matches = matches && (
                    user.UserName.Contains(filterParams.Search, StringComparison.OrdinalIgnoreCase) ||
                    user.Emails?.Any(e => e.Value.Contains(filterParams.Search, StringComparison.OrdinalIgnoreCase)) == true ||
                    user.Name?.GivenName?.Contains(filterParams.Search, StringComparison.OrdinalIgnoreCase) == true ||
                    user.Name?.FamilyName?.Contains(filterParams.Search, StringComparison.OrdinalIgnoreCase) == true);
            }

            return matches;
        }

        private static bool HasMultipleParams(ScimFilterParams filterParams)
        {
            int count = 0;

            if (filterParams.UserName != null)
            {
                count++;
            }

            if (filterParams.Email != null)
            {
                count++;
            }

            if (filterParams.FirstName != null)
            {
                count++;
            }

            if (filterParams.LastName != null)
            {
                count++;
            }

            if (filterParams.Search != null)
            {
                count++;
            }
            return count > 1;
        }

        private static bool HasAnyParam(ScimFilterParams filterParams)
        {
            return filterParams.UserName != null ||
                   filterParams.Email != null ||
                   filterParams.FirstName != null ||
                   filterParams.LastName != null ||
                   filterParams.Search != null;
        }

        private static ScimFilterParams MergeParams(ScimFilterParams left, ScimFilterParams right)
        {
            return new ScimFilterParams(
                UserName: left.UserName ?? right.UserName,
                Email: left.Email ?? right.Email,
                FirstName: left.FirstName ?? right.FirstName,
                LastName: left.LastName ?? right.LastName,
                Search: left.Search ?? right.Search,
                InMemoryFilter: left.InMemoryFilter ?? right.InMemoryFilter);
        }
    }
}
