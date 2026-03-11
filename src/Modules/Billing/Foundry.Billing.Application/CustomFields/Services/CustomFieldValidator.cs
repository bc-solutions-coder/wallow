using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Foundry.Billing.Application.CustomFields.Interfaces;
using Foundry.Billing.Domain.CustomFields.Entities;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Billing.Application.CustomFields.Services;

public sealed partial class CustomFieldValidator(
    ICustomFieldDefinitionRepository repository,
    ITenantContext tenantContext) : ICustomFieldValidator
{

    public async Task<CustomFieldValidationResult> ValidateAsync<T>(
        T entity,
        CancellationToken cancellationToken = default) where T : IHasCustomFields
    {
        if (!tenantContext.IsResolved)
        {
            return CustomFieldValidationResult.Success(); // No tenant = no validation
        }

        string entityType = typeof(T).Name;

        if (!CustomFieldRegistry.IsSupported(entityType))
        {
            return CustomFieldValidationResult.Success(); // Entity type not configured for custom fields
        }

        IReadOnlyList<CustomFieldDefinition> definitions = await repository.GetByEntityTypeAsync(
            entityType,
            includeInactive: false,
            cancellationToken);

        if (definitions.Count == 0)
        {
            return CustomFieldValidationResult.Success(); // No fields defined
        }

        List<CustomFieldValidationError> errors = [];
        Dictionary<string, object> customFields = entity.CustomFields ?? [];

        foreach (CustomFieldDefinition definition in definitions)
        {
            List<CustomFieldValidationError> fieldErrors = ValidateField(definition, customFields);
            errors.AddRange(fieldErrors);
        }

        return errors.Count > 0
            ? CustomFieldValidationResult.Failure(errors)
            : CustomFieldValidationResult.Success();
    }

    private static List<CustomFieldValidationError> ValidateField(
        CustomFieldDefinition definition,
        Dictionary<string, object> customFields)
    {
        List<CustomFieldValidationError> errors = [];
        string fieldKey = definition.FieldKey;
        string displayName = definition.DisplayName;

        bool hasValue = customFields.TryGetValue(fieldKey, out object? rawValue);
        object? value = NormalizeValue(rawValue);
        string stringValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        bool isEmpty = string.IsNullOrWhiteSpace(stringValue);

        // Required check
        if (definition.IsRequired && (!hasValue || isEmpty))
        {
            errors.Add(new(fieldKey, $"{displayName} is required"));
            return errors; // No point validating further
        }

        // If no value provided and not required, skip validation
        if (!hasValue || isEmpty)
        {
            return errors;
        }

        // Type validation
        string? typeError = ValidateType(definition, value, displayName);
        if (typeError != null)
        {
            errors.Add(new(fieldKey, typeError));
            return errors; // Type mismatch, skip rule validation
        }

        // Rule validation
        FieldValidationRules? rules = definition.GetValidationRules();
        if (rules != null)
        {
            List<string> ruleErrors = ValidateRules(definition, value, stringValue, rules, displayName);
            errors.AddRange(ruleErrors.Select(msg => new CustomFieldValidationError(fieldKey, msg)));
        }

        // Options validation for dropdown/multi-select
        if (definition.FieldType == CustomFieldType.Dropdown ||
            definition.FieldType == CustomFieldType.MultiSelect)
        {
            List<string> optionErrors = ValidateOptions(definition, value, displayName);
            errors.AddRange(optionErrors.Select(msg => new CustomFieldValidationError(fieldKey, msg)));
        }

        return errors;
    }

    private static object? NormalizeValue(object? value)
    {
        // Handle JsonElement from deserialized JSON
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.Number => jsonElement.TryGetInt64(out long l) ? l : jsonElement.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => jsonElement.EnumerateArray().Select(e => e.GetString()).ToList(),
                JsonValueKind.Null => null,
                _ => jsonElement.ToString()
            };
        }
        return value;
    }

    private static string? ValidateType(
        CustomFieldDefinition definition,
        object? value,
        string displayName)
    {
        if (value == null)
        {
            return null;
        }

        return definition.FieldType switch
        {
            CustomFieldType.Number when !IsInteger(value)
                => $"{displayName} must be a whole number",

            CustomFieldType.Decimal when !IsNumeric(value)
                => $"{displayName} must be a number",

            CustomFieldType.Date when !IsDate(value)
                => $"{displayName} must be a valid date",

            CustomFieldType.DateTime when !IsDateTime(value)
                => $"{displayName} must be a valid date and time",

            CustomFieldType.Boolean when value is not bool && !IsBoolString(value)
                => $"{displayName} must be true or false",

            CustomFieldType.Email when !IsEmail(Convert.ToString(value, CultureInfo.InvariantCulture))
                => $"{displayName} must be a valid email address",

            CustomFieldType.Url when !IsUrl(Convert.ToString(value, CultureInfo.InvariantCulture))
                => $"{displayName} must be a valid URL",

            CustomFieldType.MultiSelect when value is not IEnumerable<object> and not List<string>
                => $"{displayName} must be a list of values",

            _ => null
        };
    }

    private static List<string> ValidateRules(
        CustomFieldDefinition _,
        object? value,
        string stringValue,
        FieldValidationRules rules,
        string displayName)
    {
        List<string> errors = [];

        // String length validation
        if (rules.MinLength.HasValue && stringValue.Length < rules.MinLength.Value)
        {
            errors.Add($"{displayName} must be at least {rules.MinLength.Value} characters");
        }

        if (rules.MaxLength.HasValue && stringValue.Length > rules.MaxLength.Value)
        {
            errors.Add($"{displayName} must be at most {rules.MaxLength.Value} characters");
        }

        // Numeric range validation
        if (IsNumeric(value) && decimal.TryParse(stringValue, out decimal numericValue))
        {
            if (rules.Min.HasValue && numericValue < rules.Min.Value)
            {
                errors.Add($"{displayName} must be at least {rules.Min.Value}");
            }

            if (rules.Max.HasValue && numericValue > rules.Max.Value)
            {
                errors.Add($"{displayName} must be at most {rules.Max.Value}");
            }
        }

        // Date range validation
        if (IsDate(value) && DateTime.TryParse(stringValue, out DateTime dateValue))
        {
            if (rules.MinDate.HasValue && dateValue < rules.MinDate.Value)
            {
                errors.Add($"{displayName} must be on or after {rules.MinDate.Value:yyyy-MM-dd}");
            }

            if (rules.MaxDate.HasValue && dateValue > rules.MaxDate.Value)
            {
                errors.Add($"{displayName} must be on or before {rules.MaxDate.Value:yyyy-MM-dd}");
            }
        }

        // Pattern validation
        if (!string.IsNullOrEmpty(rules.Pattern))
        {
            try
            {
                if (!Regex.IsMatch(stringValue, rules.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
                {
                    errors.Add(rules.PatternMessage ?? $"{displayName} format is invalid");
                }
            }
            catch (RegexMatchTimeoutException)
            {
                errors.Add($"{displayName} validation timed out");
            }
        }

        return errors;
    }

    private static List<string> ValidateOptions(
        CustomFieldDefinition definition,
        object? value,
        string displayName)
    {
        List<string> errors = [];
        IReadOnlyList<CustomFieldOption> options = definition.GetOptions();

        if (options.Count == 0)
        {
            return errors;
        }

        HashSet<string> validValues = options
            .Where(o => o.IsActive)
            .Select(o => o.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (definition.FieldType == CustomFieldType.MultiSelect)
        {
            // value is guaranteed non-null here due to early return in ValidateField
            IEnumerable<string> selectedValues = value switch
            {
                IEnumerable<string> list => list,
                IEnumerable<object> list => list.Select(o => Convert.ToString(o, CultureInfo.InvariantCulture) ?? ""),
                _ => [Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""]
            };

            foreach (string selectedValue in selectedValues)
            {
                if (!validValues.Contains(selectedValue))
                {
                    errors.Add($"{displayName} contains invalid value: {selectedValue}");
                }
            }
        }
        else // Dropdown
        {
            string selectedValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            if (!validValues.Contains(selectedValue))
            {
                errors.Add($"{displayName} must be one of: {string.Join(", ", validValues)}");
            }
        }

        return errors;
    }

    // Type checking helpers
    private static bool IsInteger(object? value)
    {
        if (value is int or long)
        {
            return true;
        }

        return value != null && long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out _);
    }

    private static bool IsNumeric(object? value)
    {
        if (value is int or long or float or double or decimal)
        {
            return true;
        }

        return value != null && decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out _);
    }

    private static bool IsDate(object? value)
    {
        if (value is DateTime or DateOnly)
        {
            return true;
        }

        return value != null && DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out _);
    }

    private static bool IsDateTime(object? value)
    {
        if (value is DateTime)
        {
            return true;
        }

        return value != null && DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out _);
    }

    private static bool IsBoolString(object? value)
    {
        string? str = Convert.ToString(value, CultureInfo.InvariantCulture)?.ToLowerInvariant();
        return str is "true" or "false" or "1" or "0";
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailRegex();

    private static bool IsEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        EmailRegex().IsMatch(value);

    private static bool IsUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
