namespace Wallow.Billing.Application.CustomFields.Queries.GetCustomFieldDefinitions;

public sealed record GetCustomFieldDefinitionsQuery(string EntityType, bool IncludeInactive = false);
