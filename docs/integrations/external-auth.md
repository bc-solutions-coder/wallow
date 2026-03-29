# External Authentication Providers Setup

This guide explains how to configure external identity providers (Google, GitHub, Microsoft, Apple) for social login in Wallow.

## Overview

Wallow supports sign-in via external identity providers alongside traditional email/password authentication. When configured, social login buttons appear on the Login and Register pages. Providers that are not configured are automatically hidden from the UI.

External authentication works alongside OpenIddict -- the external provider authenticates the user, then Wallow links (or creates) a local account and issues its own OpenIddict tokens with tenant context.

## How It Works

```
User clicks "Sign in with Google"
    |
    v
GET /api/v1/identity/auth/external-login?provider=Google&returnUrl=...
    |
    v
Browser redirects to provider (e.g. accounts.google.com)
    |
    v
User authenticates with provider
    |
    v
Provider redirects back to callback:
GET /api/v1/identity/auth/external-login-callback?returnUrl=...
    |
    +-- Existing linked account --> Sign in immediately
    |
    +-- Email matches verified account --> Auto-link provider, sign in
    |
    +-- New user --> Redirect to /accept-terms --> Create account
```

## Provider Setup

Each provider requires creating an OAuth/OIDC application in that provider's developer console and obtaining credentials. Only providers with valid credentials will appear as login options.

### Google

1. Go to [Google Cloud Console > Credentials](https://console.cloud.google.com/apis/credentials)
2. Create a new project (or select existing)
3. Click **Create Credentials > OAuth client ID**
4. Application type: **Web application**
5. Add authorized redirect URI: `https://<your-domain>/signin-google`
   - For local development: `http://localhost:5001/signin-google`
6. Copy the **Client ID** and **Client Secret**

**Configuration:**
```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-client-id.apps.googleusercontent.com",
      "ClientSecret": "GOCSPX-your-client-secret"
    }
  }
}
```

### GitHub

1. Go to [GitHub > Settings > Developer settings > OAuth Apps](https://github.com/settings/developers)
2. Click **New OAuth App**
3. Set **Authorization callback URL**: `https://<your-domain>/signin-github`
   - For local development: `http://localhost:5001/signin-github`
4. Copy the **Client ID** and generate a **Client Secret**

**Configuration:**
```json
{
  "Authentication": {
    "GitHub": {
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret"
    }
  }
}
```

### Microsoft (Azure AD / Entra ID)

1. Go to [Azure Portal > App registrations](https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationsListBlade)
2. Click **New registration**
3. Supported account types: choose based on your needs (personal, work, or both)
4. Add redirect URI (Web): `https://<your-domain>/signin-microsoft`
   - For local development: `http://localhost:5001/signin-microsoft`
5. Go to **Certificates & secrets > New client secret**
6. Copy the **Application (client) ID** and the **secret value**

**Configuration:**
```json
{
  "Authentication": {
    "Microsoft": {
      "ClientId": "your-application-id",
      "ClientSecret": "your-client-secret"
    }
  }
}
```

### Apple

Apple Sign In requires an Apple Developer Program membership ($99/year).

1. Go to [Apple Developer > Certificates, Identifiers & Profiles](https://developer.apple.com/account/resources)
2. Register an **App ID** with "Sign In with Apple" capability
3. Register a **Services ID** (this is your `ServiceId` / `ClientId`)
   - Add your domain and return URL: `https://<your-domain>/signin-apple`
4. Create a **Key** with "Sign In with Apple" enabled
   - Download the `.p8` key file (you only get one download)
   - Note the **Key ID**
5. Your **Team ID** is shown in the top-right of the developer portal

**Configuration:**
```json
{
  "Authentication": {
    "Apple": {
      "ServiceId": "com.yourcompany.yourapp",
      "TeamId": "YOUR_TEAM_ID",
      "KeyId": "YOUR_KEY_ID"
    }
  }
}
```

> **Note:** The Apple `.p8` private key file must be accessible to the application at runtime for client secret generation. See the [AspNet.Security.OAuth.Apple](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/tree/dev/src/AspNet.Security.OAuth.Apple) documentation for key file configuration options.

## Configuration Methods

### User Secrets (Local Development)

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "your-client-id" --project src/Wallow.Api
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-secret" --project src/Wallow.Api
```

### Environment Variables (Production)

```bash
export Authentication__Google__ClientId="your-client-id"
export Authentication__Google__ClientSecret="your-secret"
```

### appsettings.json (Development Only)

Add to `src/Wallow.Api/appsettings.Development.json`. Never commit secrets to source control.

## How Provider Visibility Works

The backend conditionally registers authentication schemes only when credentials are present (see `IdentityInfrastructureExtensions.cs`). The API exposes a `GET /api/v1/identity/auth/external-providers` endpoint that returns the list of configured providers. The Login and Register pages query this endpoint and only render buttons for enabled providers.

If no external providers are configured, the "Or continue with" separator and all social buttons are hidden entirely.

## Configuration Reference

| Provider | Required Keys | Optional |
|----------|--------------|----------|
| Google | `ClientId`, `ClientSecret` | -- |
| GitHub | `ClientId`, `ClientSecret` | -- |
| Microsoft | `ClientId`, `ClientSecret` | -- |
| Apple | `ServiceId`, `TeamId`, `KeyId` | -- |

## Redirect URIs

Each provider requires a redirect URI registered in its developer console. The format is:

```
https://<your-api-domain>/signin-<provider>
```

| Provider | Redirect URI Path |
|----------|------------------|
| Google | `/signin-google` |
| GitHub | `/signin-github` |
| Microsoft | `/signin-microsoft` |
| Apple | `/signin-apple` |

For local development, the API typically runs on `http://localhost:5001`. Some providers (notably Apple) require HTTPS even for development.

## Troubleshooting

| Issue | Cause | Fix |
|-------|-------|-----|
| Button not showing | Provider credentials missing or empty | Verify config values are set and non-empty |
| "external_login_failed" error | Redirect URI mismatch | Ensure the redirect URI in the provider console exactly matches your API URL |
| "external_login_failed" error | Client secret expired/rotated | Regenerate and update the secret |
| Apple sign-in fails | `.p8` key not accessible | Ensure key file path is correct and readable |
| No email claim returned | Provider privacy settings | Some providers allow users to hide email; GitHub requires `user:email` scope (already configured) |
