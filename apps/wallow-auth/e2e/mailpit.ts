import type { APIRequestContext } from "@playwright/test";

/**
 * Mailpit REST helper for the backend-dependent specs that read an emailed link
 * or code back out of the inbox (magic-link.spec.ts, otp-login.spec.ts,
 * mfa.spec.ts's verify-email confirmation, reset-password.spec.ts). NOT a spec
 * file — its name is outside Playwright's `*.spec.ts` glob, so the runner never
 * treats it as a test.
 *
 * The docker/docker-compose.test.yml stack scripts/e2e.sh boots publishes
 * Mailpit's HTTP API on the host at :8035 (container :8025). The API's SMTP is
 * wired to that same Mailpit (`Smtp__Host: mailpit`), so the magic-link URL and
 * the OTP code the passwordless flow emails land here and can be read back out.
 * Override the base URL with E2E_MAILPIT_URL for a differently-mapped stack.
 *
 * The host is 127.0.0.1, not `localhost`: compose publishes the port as
 * `127.0.0.1:8035:8025` (IPv4 only), and `localhost` resolves to IPv6 `::1`
 * first on many hosts, where the connection is refused.
 */
const MAILPIT_URL: string = process.env.E2E_MAILPIT_URL ?? "http://127.0.0.1:8035";

interface MailpitRecipient {
  readonly Address: string;
}

interface MailpitSummary {
  readonly ID: string;
  readonly Subject: string;
  readonly To: readonly MailpitRecipient[] | null;
}

interface MailpitListResponse {
  readonly messages: readonly MailpitSummary[] | null;
}

interface MailpitMessage {
  readonly HTML?: string;
  readonly Text?: string;
}

const POLL_INTERVAL_MS = 500;
const DEFAULT_TIMEOUT_MS = 20_000;

function delay(ms: number): Promise<void> {
  return new Promise<void>((resolve) => {
    setTimeout(resolve, ms);
  });
}

/** One poll pass: return the matching email's body, or undefined if none yet. */
async function findEmailBody(
  request: APIRequestContext,
  subject: string,
  target: string,
): Promise<string | undefined> {
  const listResponse = await request.get(`${MAILPIT_URL}/api/v1/messages?limit=50`);

  if (!listResponse.ok()) {
    return undefined;
  }

  const list = (await listResponse.json()) as MailpitListResponse;
  // Mailpit returns newest-first, so the first match is the latest send.
  const match: MailpitSummary | undefined = (list.messages ?? []).find(
    (message) =>
      message.Subject === subject &&
      (message.To ?? []).some((recipient) => recipient.Address.toLowerCase() === target),
  );

  if (match === undefined) {
    return undefined;
  }

  const detailResponse = await request.get(`${MAILPIT_URL}/api/v1/message/${match.ID}`);
  const detail = (await detailResponse.json()) as MailpitMessage;
  return detail.HTML ?? detail.Text ?? "";
}

/**
 * Poll Mailpit until an email to `to` with an exact `subject` arrives, then
 * return its HTML body (falling back to the plaintext part). The stack starts
 * from empty volumes every run (scripts/e2e.sh does `down -v` first), so the
 * newest message matching the disjoint subjects "Your Magic Link" / "Your Login
 * Code" is unambiguously the one the test just triggered — no destructive
 * mailbox clear is needed, which keeps the two email specs safe to run in
 * parallel workers against the shared inbox.
 *
 * Recursion rather than a while-loop keeps each `await` off a loop body (the poll
 * is inherently sequential; there is nothing to parallelise).
 */
export function waitForEmailBody(
  request: APIRequestContext,
  options: { readonly to: string; readonly subject: string; readonly timeoutMs?: number },
): Promise<string> {
  const { to, subject, timeoutMs = DEFAULT_TIMEOUT_MS } = options;
  const target: string = to.toLowerCase();
  const deadline: number = Date.now() + timeoutMs;

  const attempt = async (): Promise<string> => {
    const body: string | undefined = await findEmailBody(request, subject, target);

    if (body !== undefined) {
      return body;
    }

    if (Date.now() >= deadline) {
      throw new Error(
        `No email to ${to} with subject "${subject}" arrived within ${timeoutMs}ms (Mailpit ${MAILPIT_URL}).`,
      );
    }

    await delay(POLL_INTERVAL_MS);
    return attempt();
  };

  return attempt();
}
