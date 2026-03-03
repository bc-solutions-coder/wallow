namespace Foundry.Identity.Infrastructure;

public sealed class KeycloakOptions
{
    public const string SectionName = "Identity:Keycloak";

    public string Realm { get; set; } = "foundry";
    public string AuthorityUrl { get; set; } = "http://localhost:8080/";
    public string AdminClientId { get; set; } = "foundry-api";
    public string AdminClientSecret { get; set; } = "";
}
