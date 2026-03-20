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
      // Add auth header here when Keycloak is wired up:
      // "Authorization": `Bearer ${getAccessToken()}`,
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

## Keycloak Auth (When Ready)

When you wire up Keycloak authentication, update `custom-fetch.ts` to include the Bearer token. With TanStack Start as a BFF, the recommended pattern is:

1. Store the Keycloak access token in an HTTP-only cookie (server-side)
2. Server functions read the cookie and attach it to API calls
3. Client-side hooks call server functions, not the API directly

This keeps tokens off the client. Libraries like `@auth/core` or `keycloak-js` can handle the OIDC flow.

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
