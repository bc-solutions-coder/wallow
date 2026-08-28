import type { APIRequestContext } from "@playwright/test";

/**
 * Mailpit REST helper for the backend-dependent specs that read an emailed link
 * or code back out of the inbox (magic-link.spec.ts, otp-login.spec.ts,
 * mfa.spec.ts's verify-email confirmation, reset-password.spec.ts). NOT a spec
 * file — its name is outside Playwright's `*.spec.ts` glob, so the runner never
 * treats it as a test.
 *
 * Which Mailpit holds the mail is a property of the BACKEND, so the runner that
 * supplies a non-local backend names it in `E2E_MAILPIT_URL`; the default below
 * describes a local backend only. `./scripts/e2e.sh` exports it -- the Mailpit
 * port scripts/e2e.sh chose for this run (classic default :8035, which is how
 * docker-compose.test.yml publishes the Mailpit its API's `Smtp__Host` points
 * at) -- on every path it takes.
 *
 * Deriving it instead from `E2E_BASE_URL` does NOT work, though it looks like it
 * should: that variable selects how the wallow-auth APP is served, not which API
 * is behind it. e2e.sh's default mode leaves it unset while still driving the
 * containerised API, so the inference reads :8025 against a stack whose mail is
 * on the allocated Mailpit port (classic default :8035) and every email spec
 * times out.
 *
 * The default suits a local backend: appsettings.Development.json's `Smtp` block
 * sends to `localhost:1025`, so the inbox is whichever Mailpit owns host port
 * 1025 — the dev-infra one `pnpm backend:infra` brings up, published on :8025.
 * One local case still needs the variable set explicitly: the Aspire AppHost
 * declares a Mailpit of its own whose HTTP endpoint takes a DYNAMIC host port
 * (`WithHttpEndpoint(targetPort: 8025)` names no host port), so when that is the
 * live inbox, read the port off the Aspire dashboard and pass it in.
 *
 * The host is 127.0.0.1, not `localhost`: both Mailpits publish IPv4-only
 * (`127.0.0.1:8035:8025` / `127.0.0.1:8025:8025`), and `localhost` resolves to
 * IPv6 `::1` first on many hosts, where the connection is refused. Do not
 * "simplify" it.
 */
const MAILPIT_URL: string = process.env.E2E_MAILPIT_URL ?? "http://127.0.0.1:8025";

interface MailpitRecipient {
  readonly Address: string;
}

interface MailpitSummary {
  readonly ID: string;
  readonly Subject: string;
  readonly To: readonly MailpitRecipient[] | null;
}

/** Messages already in the inbox — nothing to ignore. */
const NOTHING_IGNORED: ReadonlySet<string> = new Set<string>();

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

/** Every message currently matching `to` + `subject`, newest first. */
async function listMatches(
  request: APIRequestContext,
  subject: string,
  target: string,
): Promise<readonly MailpitSummary[]> {
  const listResponse = await request.get(`${MAILPIT_URL}/api/v1/messages?limit=50`);

  if (!listResponse.ok()) {
    return [];
  }

  const list = (await listResponse.json()) as MailpitListResponse;

  return (list.messages ?? []).filter(
    (message) =>
      message.Subject === subject &&
      (message.To ?? []).some((recipient) => recipient.Address.toLowerCase() === target),
  );
}

/**
 * The IDs of the messages already matching `to` + `subject`. Capture this BEFORE
 * triggering the send and hand it to `waitForEmailBody` as `ignore`.
 *
 * Required whenever the recipient is a fixed address (admin@wallow.dev) rather
 * than a per-run one, because a local backend's Mailpit is long-lived: the
 * newest match may be a previous run's email, whose one-time token is already
 * consumed (magic link, OTP) or invalidated by that run's own password change
 * (reset token). The spec then drives a dead token and fails with no hint that
 * it read the wrong email. Comparing IDs rather than timestamps keeps this exact
 * — no clock is shared between the runner and Mailpit's container.
 */
export async function seenEmailIds(
  request: APIRequestContext,
  options: { readonly to: string; readonly subject: string },
): Promise<ReadonlySet<string>> {
  const matches: readonly MailpitSummary[] = await listMatches(
    request,
    options.subject,
    options.to.toLowerCase(),
  );

  return new Set(matches.map((message) => message.ID));
}

/** One poll pass: return the matching email's body, or undefined if none yet. */
async function findEmailBody(
  request: APIRequestContext,
  subject: string,
  target: string,
  ignore: ReadonlySet<string>,
): Promise<string | undefined> {
  const matches: readonly MailpitSummary[] = await listMatches(request, subject, target);
  // Mailpit returns newest-first, so the first unseen match is the latest send.
  const match: MailpitSummary | undefined = matches.find((message) => !ignore.has(message.ID));

  if (match === undefined) {
    return undefined;
  }

  const detailResponse = await request.get(`${MAILPIT_URL}/api/v1/message/${match.ID}`);
  const detail = (await detailResponse.json()) as MailpitMessage;
  return detail.HTML ?? detail.Text ?? "";
}

/**
 * Poll Mailpit until an email to `to` with an exact `subject` arrives, then
 * return its HTML body (falling back to the plaintext part). Matching is by
 * recipient plus exact subject, and the subjects in play are disjoint ("Your
 * Magic Link" / "Your Login Code" / "Password Reset Request" / "Verify your
 * email address"), so nothing here is destructive and the email specs stay safe
 * to run in parallel workers against one shared inbox.
 *
 * Pass `ignore` (from `seenEmailIds`, captured before the send) whenever the
 * recipient is a fixed address; without it a long-lived local Mailpit hands back
 * a previous run's dead token. Runs against `scripts/e2e.sh` start from empty
 * volumes (`down -v`), so there the set is simply empty.
 *
 * Recursion rather than a while-loop keeps each `await` off a loop body (the poll
 * is inherently sequential; there is nothing to parallelise).
 */
export function waitForEmailBody(
  request: APIRequestContext,
  options: {
    readonly to: string;
    readonly subject: string;
    readonly timeoutMs?: number;
    readonly ignore?: ReadonlySet<string>;
  },
): Promise<string> {
  const { to, subject, timeoutMs = DEFAULT_TIMEOUT_MS, ignore = NOTHING_IGNORED } = options;
  const target: string = to.toLowerCase();
  const deadline: number = Date.now() + timeoutMs;

  const attempt = async (): Promise<string> => {
    const body: string | undefined = await findEmailBody(request, subject, target, ignore);

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
