namespace Wallow.Billing.Application.Metering.DTOs;

/// <summary>
/// DTO for meter definitions.
/// </summary>
public sealed record MeterDefinitionDto(
    Guid Id,
    string Code,
    string DisplayName,
    string Unit,
    string Aggregation,
    bool IsBillable);
