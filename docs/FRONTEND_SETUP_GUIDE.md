# Frontend Setup Guide — TanStack Start + Orval + Wallow API

This guide walks through setting up a TanStack Start frontend project that consumes the Wallow .NET API via auto-generated TypeScript clients from the OpenAPI spec.

## Architecture

```
Browser  ──►  TanStack Start (BFF, port 3000)  ──►  Wallow.Api (port 5000)
                     │                                      │
                     ├─ Server functions (auth, aggregation) │
                     ├─ Generated API client (orval)         │
                     └─ TanStack Query (caching, state)      ▼
                                                     Postgres / RabbitMQ / etc.
```

- **TanStack Start** acts as the BFF (Backend for Frontend) — server functions handle auth tokens, aggregate API calls, and keep secrets server-side
- **Orval** generates typed TanStack Query hooks from the Wallow OpenAPI spec
- The Wallow API serves its OpenAPI spec via Scalar at `http://localhost:5000/openapi/v1.json`

---

## Prerequisites

- Node.js 20+
- pnpm (recommended) or npm
- Wallow API running locally (`dotnet run --project src/Wallow.Api`)

---

## Step 1: Scaffold the TanStack Start Project

```bash
# From the repo root
cd /path/to/Wallow

# Create the frontend project
npx create-tsrouter-app@latest client --template file-router --add-ons tanstack-query,tailwind-css

cd client
pnpm install
```

This gives you:

```
client/
├── src/
│   ├── routes/           # file-based routing
│   ├── components/
│   └── ...
├── app.config.ts
├── package.json
└── tsconfig.json
```

---

## Step 2: Install Orval

```bash
cd client
pnpm add -D orval
pnpm add @tanstack/react-query
```

---

## Step 3: Configure Orval

Create `client/orval.config.ts`:

```ts
import { defineConfig } from "orval";

export default defineConfig({
  wallow: {
    input: {
      // Points to the Wallow API's OpenAPI spec served by Scalar
      // Use the local file path if you want to generate without the API running
      target: "http://localhost:5000/openapi/v1.json",
    },
    output: {
      // Split generated code by API tag (maps to .NET controller groups)
      mode: "tags-split",

      // Where the generated hooks go
      target: "./src/api/generated",

      // Where the generated TypeScript types go
      schemas: "./src/api/models",

      // Generate TanStack Query hooks
      client: "react-query",

      // Use fetch (not axios) — lighter, no extra dependency
      httpClient: "fetch",

      // Use a custom fetch wrapper for auth, base URL, error handling
      override: {
        mutator: {
          path: "./src/api/custom-fetch.ts",
          name: "customFetch",
        },
        query: {
          useQuery: true,
          useMutation: true,
          useSuspenseQuery: true,
        },
      },
    },
  },
});
```

---

## Step 4: Create the Custom Fetch Wrapper

Create `client/src/api/custom-fetch.ts`:

```ts
// Base URL for the Wallow API
const API_BASE_URL =
  import.meta.env.VITE_API_URL ?? "http://localhost:5000";

type RequestConfig = {
  url: string;
  method: string;
  params?: Record<string, string>;
  data?: unknown;
  headers?: Record<string, string>;
  signal?: AbortSignal;
};

export const customFetch = async <T>({
  url,
  method,
  params,
  data,
  headers,
  signal,
}: RequestConfig): Promise<T> => {
  const queryString = params
    ? `?${new URLSearchParams(params).toString()}`
    : "";

  const response = await fetch(`${API_BASE_URL}${url}${queryString}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      ...headers,
      // Auth is handled by the BFF server functions — see "Authentication" section below.
      // For server-side calls, use authenticatedFetch() from ~/api/server-api.ts instead.
    },
    body: data ? JSON.stringify(data) : undefined,
    signal,
    credentials: "include",
  });

  if (!response.ok) {
    // Wallow API returns RFC 7807 Problem Details on errors
    const problem = await response.json().catch(() => null);
    throw {
      status: response.status,
      statusText: response.statusText,
      detail: problem?.detail ?? response.statusText,
      title: problem?.title ?? "API Error",
      errors: problem?.errors,
    };
  }

  // Handle 204 No Content
  if (response.status === 204) {
    return undefined as T;
  }

  return response.json();
};

export default customFetch;
```

---

## Step 5: Add Scripts to package.json

Update `client/package.json`:

```json
{
  "scripts": {
    "dev": "vinxi dev",
    "build": "vinxi build",
    "start": "vinxi start",
    "generate-api": "orval",
    "generate-api:watch": "orval --watch"
  }
}
```

---

## Step 6: Generate the API Client

Make sure the Wallow API is running first:

```bash
# Terminal 1 — start the API
cd /path/to/Wallow
dotnet run --project src/Wallow.Api

# Terminal 2 — generate the client
cd /path/to/Wallow/client
pnpm generate-api
```

This produces:

```
client/src/api/
├── generated/
│   ├── billing/
│   │   └── billing.ts          # useGetInvoices, useCreateInvoice, etc.
│   ├── identity/
│   │   └── identity.ts         # useGetUsers, etc.
│   ├── inquiries/
│   │   └── inquiries.ts        # useGetInquiries, useSubmitInquiry, etc.
│   ├── notifications/
│   │   └── notifications.ts
│   ├── storage/
│   │   └── storage.ts
│   └── ...
├── models/
│   ├── invoiceResponse.ts      # TypeScript types matching your C# DTOs
│   ├── createInquiryRequest.ts
│   └── ...
└── custom-fetch.ts
```

---

## Step 7: Use Generated Hooks in Components

### Basic Query

```tsx
// src/routes/inquiries.tsx
import { useGetInquiries } from "~/api/generated/inquiries/inquiries";

export default function InquiriesPage() {
  const { data, isLoading, error } = useGetInquiries();

  if (isLoading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.detail}</div>;

  return (
    <ul>
      {data?.map((inquiry) => (
        <li key={inquiry.id}>{inquiry.title}</li>
      ))}
    </ul>
  );
}
```

### Mutation

```tsx
import { useSubmitInquiry } from "~/api/generated/inquiries/inquiries";

function NewInquiryForm() {
  const { mutate, isPending } = useSubmitInquiry();

  const handleSubmit = (formData: FormData) => {
    mutate({
      data: {
        title: formData.get("title") as string,
        description: formData.get("description") as string,
      },
    });
  };

  return (
    <form onSubmit={(e) => { e.preventDefault(); handleSubmit(new FormData(e.currentTarget)); }}>
      <input name="title" placeholder="Title" required />
      <textarea name="description" placeholder="Description" required />
      <button type="submit" disabled={isPending}>
        {isPending ? "Submitting..." : "Submit"}
      </button>
    </form>
  );
}
```

---

## Step 8: Environment Variables

Create `client/.env`:

```env
VITE_API_URL=http://localhost:5000
```

Create `client/.env.production`:

```env
VITE_API_URL=https://api.yoursite.com
```

---

## Step 9: CORS

The Wallow API already has CORS configured. Add your frontend's origin to `appsettings.Development.json`:

```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"]
  }
}
```

---

## Offline / CI Generation (Without Running the API)

To generate the client in CI or without the API running, export the spec to a file:

```bash
# From repo root, after building the API
dotnet run --project src/Wallow.Api -- --urls "http://localhost:0" &
sleep 3
curl -o client/openapi.json http://localhost:5000/openapi/v1.json
kill %1
```

Or commit the spec and update it periodically. Then point orval at the file:

```ts
// orval.config.ts
input: {
  target: "./openapi.json",
},
```

---

## Project Structure Summary

```
Wallow/
├── src/
│   ├── Wallow.Api/                  # .NET API backend
│   ├── Modules/                      # domain modules
│   └── Shared/                       # shared contracts
├── client/                           # TanStack Start frontend (BFF)
│   ├── src/
│   │   ├── api/
│   │   │   ├── generated/            # orval output (can be gitignored)
│   │   │   ├── models/               # generated TypeScript types
│   │   │   └── custom-fetch.ts       # auth + base URL wrapper
│   │   ├── routes/                   # file-based routing
│   │   ├── components/               # UI components
│   │   └── ...
│   ├── orval.config.ts
│   ├── .env
│   └── package.json
├── docker/                           # infrastructure
└── docs/
```

---

## Authentication — OpenIddict OIDC + BFF Pattern

Wallow uses OpenIddict as its OIDC provider. The frontend connects via the **Authorization Code Flow with PKCE**, with TanStack Start acting as the BFF to keep tokens server-side.

### Auth Architecture

```
Browser ──► TanStack Start BFF (port 3000)  ──► Wallow.Api (port 5000)
               │                                      │
               ├─ /auth/login   → redirect to OIDC    │
               ├─ /auth/callback → exchange code       │
               ├─ /auth/logout  → end session          │
               ├─ /auth/me      → return user info     │
               │                                      │
               │  Session cookie (HTTP-only)           │
               │  stores access + refresh tokens       │
               └─ Server functions attach Bearer ──────┘
```

**Key principle:** The browser never sees tokens. The BFF stores them in an encrypted HTTP-only session cookie and proxies API calls with the Bearer header attached server-side.

---

### OIDC Endpoints (Wallow.Api)

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/connect/authorize` | GET | Start authorization (redirect user here) |
| `/connect/token` | POST | Exchange code for tokens |
| `/connect/logout` | GET/POST | End session |
| `/connect/userinfo` | GET/POST | Get user profile claims |

---

### Dev Client (Pre-Registered)

Wallow seeds a development client for local frontend work:

| Setting | Value |
|---------|-------|
| Client ID | `wallow-dev-client` |
| Client Type | Public (no secret) |
| Redirect URIs | `http://localhost:5000/callback`, `http://localhost:3000/callback` |
| Post-Logout URIs | `http://localhost:5000`, `http://localhost:3000` |
| Flows | Authorization Code + Refresh Token |
| PKCE | **Required** (S256) |
| Scopes | `openid`, `profile`, `email`, `roles`, `api` |

---

### Step-by-Step: Wire Up Auth in TanStack Start

#### 1. Install Dependencies

```bash
cd client
pnpm add arctic    # lightweight OIDC client (handles PKCE, token exchange)
pnpm add iron-session  # encrypted cookie sessions
```

#### 2. Configure the OIDC Provider

Create `client/src/auth/oidc.ts`:

```ts
import { OAuth2Client } from "arctic";

const ISSUER = process.env.WALLOW_API_URL ?? "http://localhost:5000";

export const wallowOAuth = new OAuth2Client(
  "wallow-dev-client",
  null, // no client secret (public client)
  `${ISSUER}/connect/authorize`,
  `${ISSUER}/connect/token`,
);

export const OIDC_CONFIG = {
  issuer: ISSUER,
  clientId: "wallow-dev-client",
  redirectUri: "http://localhost:3000/auth/callback",
  postLogoutRedirectUri: "http://localhost:3000",
  scopes: ["openid", "profile", "email", "roles"],
  userInfoEndpoint: `${ISSUER}/connect/userinfo`,
  endSessionEndpoint: `${ISSUER}/connect/logout`,
} as const;
```

#### 3. Configure Session Storage

Create `client/src/auth/session.ts`:

```ts
import { getIronSession } from "iron-session";

export interface SessionData {
  accessToken?: string;
  refreshToken?: string;
  expiresAt?: number;
  user?: {
    sub: string;
    email: string;
    name: string;
    roles: string[];
  };
}

export const SESSION_OPTIONS = {
  password: process.env.SESSION_SECRET ?? "dev-secret-at-least-32-characters-long!!",
  cookieName: "wallow_session",
  cookieOptions: {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    maxAge: 60 * 60 * 24 * 7, // 7 days
  },
};

// Use in server functions:
// const session = await getIronSession<SessionData>(cookies, SESSION_OPTIONS);
```

#### 4. Create Auth Routes

**Login route** — `client/src/routes/auth/login.ts`:

```ts
import { createAPIFileRoute } from "@tanstack/start/api";
import { generateCodeVerifier, generateState } from "arctic";
import { wallowOAuth, OIDC_CONFIG } from "~/auth/oidc";
import { setCookie } from "vinxi/http";

export const APIRoute = createAPIFileRoute("/auth/login")({
  GET: async ({ request }) => {
    const state = generateState();
    const codeVerifier = generateCodeVerifier();

    const url = wallowOAuth.createAuthorizationURL(OIDC_CONFIG.redirectUri, state, {
      codeVerifier,
      scopes: OIDC_CONFIG.scopes,
    });

    // Store state + verifier in short-lived cookies for the callback
    setCookie("oidc_state", state, { httpOnly: true, maxAge: 600, path: "/" });
    setCookie("oidc_code_verifier", codeVerifier, { httpOnly: true, maxAge: 600, path: "/" });

    return new Response(null, {
      status: 302,
      headers: { Location: url.toString() },
    });
  },
});
```

**Callback route** — `client/src/routes/auth/callback.ts`:

```ts
import { createAPIFileRoute } from "@tanstack/start/api";
import { wallowOAuth, OIDC_CONFIG } from "~/auth/oidc";
import { getIronSession } from "iron-session";
import { SESSION_OPTIONS, type SessionData } from "~/auth/session";
import { getCookie, deleteCookie } from "vinxi/http";

export const APIRoute = createAPIFileRoute("/auth/callback")({
  GET: async ({ request }) => {
    const url = new URL(request.url);
    const code = url.searchParams.get("code");
    const state = url.searchParams.get("state");
    const storedState = getCookie("oidc_state");
    const codeVerifier = getCookie("oidc_code_verifier");

    // Validate state to prevent CSRF
    if (!code || !state || state !== storedState || !codeVerifier) {
      return new Response("Invalid callback", { status: 400 });
    }

    // Clean up temporary cookies
    deleteCookie("oidc_state");
    deleteCookie("oidc_code_verifier");

    // Exchange authorization code for tokens
    const tokens = await wallowOAuth.validateAuthorizationCode(
      code,
      OIDC_CONFIG.redirectUri,
      codeVerifier,
    );

    // Fetch user info from the OIDC provider
    const userInfoResponse = await fetch(OIDC_CONFIG.userInfoEndpoint, {
      headers: { Authorization: `Bearer ${tokens.accessToken()}` },
    });
    const userInfo = await userInfoResponse.json();

    // Store tokens in encrypted session cookie
    const session = await getIronSession<SessionData>(request, new Response(), SESSION_OPTIONS);
    session.accessToken = tokens.accessToken();
    session.refreshToken = tokens.refreshToken();
    session.expiresAt = tokens.accessTokenExpiresAt()?.getTime();
    session.user = {
      sub: userInfo.sub,
      email: userInfo.email,
      name: userInfo.name ?? userInfo.given_name,
      roles: Array.isArray(userInfo.role) ? userInfo.role : [userInfo.role].filter(Boolean),
    };
    await session.save();

    return new Response(null, {
      status: 302,
      headers: { Location: "/" },
    });
  },
});
```

**Me route** — `client/src/routes/auth/me.ts`:

```ts
import { createAPIFileRoute } from "@tanstack/start/api";
import { getIronSession } from "iron-session";
import { SESSION_OPTIONS, type SessionData } from "~/auth/session";

export const APIRoute = createAPIFileRoute("/auth/me")({
  GET: async ({ request }) => {
    const session = await getIronSession<SessionData>(request, new Response(), SESSION_OPTIONS);

    if (!session.user) {
      return new Response(JSON.stringify({ authenticated: false }), {
        status: 401,
        headers: { "Content-Type": "application/json" },
      });
    }

    return new Response(
      JSON.stringify({ authenticated: true, user: session.user }),
      { headers: { "Content-Type": "application/json" } },
    );
  },
});
```

**Logout route** — `client/src/routes/auth/logout.ts`:

```ts
import { createAPIFileRoute } from "@tanstack/start/api";
import { getIronSession } from "iron-session";
import { SESSION_OPTIONS, type SessionData } from "~/auth/session";
import { OIDC_CONFIG } from "~/auth/oidc";

export const APIRoute = createAPIFileRoute("/auth/logout")({
  POST: async ({ request }) => {
    const session = await getIronSession<SessionData>(request, new Response(), SESSION_OPTIONS);
    session.destroy();

    // Redirect to Wallow's OIDC end session endpoint
    const logoutUrl = new URL(OIDC_CONFIG.endSessionEndpoint);
    logoutUrl.searchParams.set("post_logout_redirect_uri", OIDC_CONFIG.postLogoutRedirectUri);

    return new Response(null, {
      status: 302,
      headers: { Location: logoutUrl.toString() },
    });
  },
});
```

#### 5. Update Custom Fetch to Attach Bearer Tokens

Replace the placeholder in `client/src/api/custom-fetch.ts`:

```ts
import { getIronSession } from "iron-session";
import { SESSION_OPTIONS, type SessionData } from "~/auth/session";

const API_BASE_URL = process.env.WALLOW_API_URL ?? "http://localhost:5000";

type RequestConfig = {
  url: string;
  method: string;
  params?: Record<string, string>;
  data?: unknown;
  headers?: Record<string, string>;
  signal?: AbortSignal;
};

export const customFetch = async <T>({
  url,
  method,
  params,
  data,
  headers,
  signal,
}: RequestConfig): Promise<T> => {
  const queryString = params
    ? `?${new URLSearchParams(params).toString()}`
    : "";

  // In server functions, attach the Bearer token from the session
  const authHeaders: Record<string, string> = {};
  if (typeof window === "undefined") {
    // Server-side: read token from session
    // The session must be passed via async context or request
    // This is a simplified example — adapt to your server function pattern
  }

  const response = await fetch(`${API_BASE_URL}${url}${queryString}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      ...authHeaders,
      ...headers,
    },
    body: data ? JSON.stringify(data) : undefined,
    signal,
  });

  if (!response.ok) {
    if (response.status === 401) {
      // Token expired — trigger refresh or redirect to login
      throw { status: 401, detail: "Session expired", redirect: "/auth/login" };
    }

    const problem = await response.json().catch(() => null);
    throw {
      status: response.status,
      statusText: response.statusText,
      detail: problem?.detail ?? response.statusText,
      title: problem?.title ?? "API Error",
      errors: problem?.errors,
    };
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json();
};

export default customFetch;
```

#### 6. Create a Server Function Helper for Authenticated API Calls

Create `client/src/api/server-api.ts`:

```ts
import { createServerFn } from "@tanstack/start";
import { getIronSession } from "iron-session";
import { SESSION_OPTIONS, type SessionData } from "~/auth/session";
import { getWebRequest } from "vinxi/http";

const API_BASE_URL = process.env.WALLOW_API_URL ?? "http://localhost:5000";

/**
 * Makes an authenticated API call from the BFF to Wallow.
 * Reads the access token from the encrypted session cookie
 * and attaches it as a Bearer header.
 */
export async function authenticatedFetch<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const request = getWebRequest();
  const session = await getIronSession<SessionData>(request, new Response(), SESSION_OPTIONS);

  if (!session.accessToken) {
    throw new Error("Not authenticated");
  }

  // TODO: Check session.expiresAt and refresh if needed
  // using wallowOAuth.refreshAccessToken(session.refreshToken)

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${session.accessToken}`,
      ...options.headers,
    },
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw {
      status: response.status,
      detail: problem?.detail ?? response.statusText,
    };
  }

  if (response.status === 204) return undefined as T;
  return response.json();
}

// Example server function using authenticated fetch:
export const getMyInvoices = createServerFn({ method: "GET" })
  .handler(async () => {
    return authenticatedFetch("/api/v1/billing/invoices");
  });
```

---

### Token Refresh

Wallow access tokens are short-lived. The BFF should handle refresh transparently:

```ts
// In authenticatedFetch, before making the API call:
if (session.expiresAt && Date.now() > session.expiresAt - 30_000) {
  // Token expires within 30 seconds — refresh it
  const newTokens = await wallowOAuth.refreshAccessToken(session.refreshToken!);
  session.accessToken = newTokens.accessToken();
  session.refreshToken = newTokens.refreshToken();
  session.expiresAt = newTokens.accessTokenExpiresAt()?.getTime();
  await session.save();
}
```

---

### Using Auth in Components

```tsx
// src/hooks/useAuth.ts
import { useQuery } from "@tanstack/react-query";

export function useAuth() {
  return useQuery({
    queryKey: ["auth", "me"],
    queryFn: () => fetch("/auth/me").then((r) => r.json()),
    staleTime: 5 * 60 * 1000, // 5 minutes
    retry: false,
  });
}

// src/components/AuthButton.tsx
import { useAuth } from "~/hooks/useAuth";

export function AuthButton() {
  const { data, isLoading } = useAuth();

  if (isLoading) return null;

  if (data?.authenticated) {
    return (
      <div>
        <span>{data.user.name}</span>
        <form method="post" action="/auth/logout">
          <button type="submit">Logout</button>
        </form>
      </div>
    );
  }

  return <a href="/auth/login">Login</a>;
}
```

---

### Environment Variables (Auth)

Add to `client/.env`:

```env
VITE_API_URL=http://localhost:5000
WALLOW_API_URL=http://localhost:5000
SESSION_SECRET=dev-secret-at-least-32-characters-long!!
```

`WALLOW_API_URL` is server-only (no `VITE_` prefix) — it never reaches the browser.

---

### Default Dev Credentials

| Field | Value |
|-------|-------|
| Email | `admin@wallow.dev` |
| Password | `Admin123!` |

---

### API Scopes Reference

Request these scopes in the `openid` authorization to access specific API features:

| Category | Scopes |
|----------|--------|
| **Standard OIDC** | `openid`, `profile`, `email`, `roles` |
| **Billing** | `billing.read`, `billing.manage`, `invoices.read`, `invoices.write`, `payments.read`, `payments.write`, `subscriptions.read`, `subscriptions.write` |
| **Identity** | `users.read`, `users.write`, `users.manage`, `roles.read`, `roles.write`, `organizations.read`, `organizations.manage` |
| **Storage** | `storage.read`, `storage.write` |
| **Messaging** | `messaging.access`, `announcements.read`, `announcements.manage`, `notifications.read`, `notifications.write` |
| **Inquiries** | `inquiries.read`, `inquiries.write` |
| **Configuration** | `configuration.read`, `configuration.manage` |

---

### Rate Limits

| Endpoint | Limit | Window |
|----------|-------|--------|
| Auth endpoints | 3 requests | 10 minutes |
| Global (per tenant) | 1000 requests | 1 hour |
| File uploads | 10 requests | 1 hour |

The API returns `429 Too Many Requests` with a `Retry-After` header when limits are exceeded.

---

### Security Checklist

- [ ] PKCE is used for all authorization code flows (enforced by Wallow)
- [ ] Tokens stored in HTTP-only encrypted cookies, never in `localStorage`
- [ ] `state` parameter validated on callback to prevent CSRF
- [ ] `WALLOW_API_URL` and `SESSION_SECRET` are server-only env vars (no `VITE_` prefix)
- [ ] Frontend origin added to `Cors:AllowedOrigins` in API config
- [ ] Token refresh handled server-side before expiry
- [ ] Logout clears both the BFF session and the Wallow OIDC session

---

## Regenerating After API Changes

Whenever the Wallow API changes (new endpoints, modified DTOs), regenerate:

```bash
cd client
pnpm generate-api
```

Orval diffs the spec and updates only what changed. Review the generated code for breaking changes in your components.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `orval` can't reach the API | Make sure `dotnet run --project src/Wallow.Api` is running |
| Spec URL 404 | Verify the spec is at `http://localhost:5000/openapi/v1.json` (not `/swagger/`) |
| CORS errors in browser | Add `http://localhost:3000` to `Cors:AllowedOrigins` in `appsettings.Development.json` |
| Generated types are `any` | Check that your .NET DTOs have proper XML docs or `[Required]` attributes |
| Auth 401 errors | Ensure the Bearer token is being passed in `custom-fetch.ts` |
