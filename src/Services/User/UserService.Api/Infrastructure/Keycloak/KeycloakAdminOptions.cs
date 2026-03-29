namespace UserService.Api.Infrastructure.Keycloak;

public sealed class KeycloakAdminOptions
{
    public const string SectionName = "Keycloak";

    public string AdminUrl { get; set; } = "http://localhost:8080";
    public string Realm { get; set; } = "urfu-link";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
