import {
  createServiceClient,
  type WallowServiceClient,
} from "@bc-solutions-coder/sdk/server/service";

/**
 * PROTOTYPE — the consumer side of the M2M path decided in #121, written
 * against the SDK subpath that does not exist yet (`packages/sdk/src/server/
 * service.ts` on this branch is a signature-only stub that throws).
 *
 * Shape to react to: one lazily-built service client per process, the SAME
 * typed client as a user session, so a generated operation is called the same
 * way (`inquiriesCreate({ client: service.client, body })`).
 */
let service: WallowServiceClient | undefined;

export async function submitInquiry(request: Request): Promise<Response> {
  if ((process.env.OIDC_SERVICE_CLIENT_ID ?? "") === "") {
    return new Response("service account not configured (OIDC_SERVICE_*)", { status: 503 });
  }
  const body: unknown = await request.json();
  try {
    service ??= createServiceClient();
    // Real call once the subpath ships and `inquiries.write` is granted:
    //   await inquiriesCreate({ client: service.client, body: { ... } });
    return Response.json({ accepted: true, echo: body });
  } catch (error: unknown) {
    return new Response(error instanceof Error ? error.message : "failed", { status: 502 });
  }
}
