# Production Notes: Keycloak Security

The `realm-export.json` ships with development defaults that **must** be changed before any production or staging deployment.

## Checklist

| Setting | Dev Value | Production Action |
|---------|-----------|-------------------|
| **SSL required** | `"external"` | Already set — do **not** lower to `"none"`. Ensure TLS terminates at the load balancer or Keycloak itself. |
| **Admin credentials** | `admin@wallow.dev` / `Admin123!` | Rotate immediately. Use a strong, unique password and restrict admin console access by IP or VPN. |
| **Direct access grants** | `true` | Set `directAccessGrantsEnabled` to `false` on all clients. Direct grant (Resource Owner Password) bypasses the browser login flow. |
| **Email verification** | `false` | Set `verifyEmail` to `true` to require users to confirm their email before access. |
| **Open registration** | `true` | Set `registrationAllowed` to `false` unless self-service signup is an intentional product feature. |
| **Brute force protection** | Disabled | Enable under Realm Settings > Security Defenses. Configure max login failures, wait increment, and lockout duration. |
| **Token lifetimes** | Keycloak defaults | Shorten access token lifespan (e.g., 5 minutes), set refresh token lifespan appropriately, and enable refresh token rotation. |
| **CORS / Redirect URIs** | Wildcards / localhost | Replace wildcard redirect URIs with exact production URIs. Restrict Web Origins to your production domain(s). |
