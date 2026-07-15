namespace Wallow.Identity.Api.Contracts.Enums;

/// <summary>SAML NameID format for API contracts.</summary>
public enum ApiSamlNameIdFormat
{
    /// <summary>Email address format.</summary>
    Email = 0,

    /// <summary>Persistent identifier.</summary>
    Persistent = 1,

    /// <summary>Transient identifier.</summary>
    Transient = 2,

    /// <summary>Unspecified format.</summary>
    Unspecified = 3
}
