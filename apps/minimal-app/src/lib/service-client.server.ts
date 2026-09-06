/**
 * The anonymous half of the example: `POST /contact` reaches the platform with
 * NO user signed in, authenticating as this deployment's registered service
 * account instead (client-credentials).
 *
 * `createServiceClient()` reads its own five-variable environment —
 * `OIDC_ISSUER`, `OIDC_SERVICE_CLIENT_ID`, `OIDC_SERVICE_CLIENT_SECRET`,
 * `OIDC_SERVICE_SCOPES`, `BFF_API_BASE_URL` — caches the token (in Valkey when
 * `REDIS_URL` is set, so replicas share one) and retries a rejected token
 * exactly once. Memoised on first use for the same reason as `bff.server.ts`.
 */
import { isApiFailure, resolveFailureMessage } from "@bc-solutions-coder/api-errors";
import { inquiriesSubmit, type SubmitInquiryRequest } from "@bc-solutions-coder/sdk";
import {
  createServiceClient,
  type WallowServiceClient,
} from "@bc-solutions-coder/sdk/server/service";

const HTTP_OK = 200;
const HTTP_BAD_REQUEST = 400;
const HTTP_SERVICE_UNAVAILABLE = 503;

let service: WallowServiceClient | undefined;

/** What the page's contact form actually asks for. */
interface ContactMessage {
  readonly name: string;
  readonly email: string;
  readonly message: string;
}

function json(status: number, body: unknown): Response {
  return Response.json(body, { status });
}

export async function submitInquiry(request: Request): Promise<Response> {
  // The service account is optional wiring: without it the route answers 503
  // rather than the app failing to boot.
  if ((process.env.OIDC_SERVICE_CLIENT_ID ?? "") === "") {
    return json(HTTP_SERVICE_UNAVAILABLE, {
      error: "No service account is configured (OIDC_SERVICE_CLIENT_ID).",
    });
  }

  const contact = (await request.json()) as Partial<ContactMessage>;
  if (!contact.name || !contact.email || !contact.message) {
    return json(HTTP_BAD_REQUEST, { error: "name, email and message are required." });
  }

  // The platform's inquiry shape asks for more than a contact form does; the
  // extra fields are filled with honest placeholders rather than invented UI.
  const body: SubmitInquiryRequest = {
    name: contact.name,
    email: contact.email,
    phone: "not provided",
    company: null,
    projectType: "contact",
    budgetRange: "not provided",
    timeline: "not provided",
    message: contact.message,
  };

  try {
    service ??= createServiceClient();
    const inquiry = await inquiriesSubmit({ client: service.client, body });
    return json(HTTP_OK, { id: inquiry.id, status: "received" });
  } catch (error: unknown) {
    // A failure the platform (or the SDK's transport) reported: answer with its
    // status and the sentence the package resolves for its code. Anything
    // else is this route's own bug and stays a 500.
    if (isApiFailure(error)) {
      return json(error.status, { error: resolveFailureMessage(error) });
    }
    throw error;
  }
}
