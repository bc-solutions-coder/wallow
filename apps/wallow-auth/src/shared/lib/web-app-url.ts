/**
 * Where a sign-in with nowhere else to go lands: the public URL of the main app
 * (wallow-web), resolved from the ENVIRONMENT — `WALLOW_WEB_URL`, read in
 * `src/app/start.ts`, the one place a `process.env` read is legal — so one image
 * serves any deployment. Same crossing mechanism as `fork-links.ts`: the shell
 * states the server's answer in the document as an inline script and the
 * browser reads it back, so a server render and its hydration agree.
 *
 * `undefined` is a real answer: with no URL configured the login page shows its
 * signed-in banner and stops, exactly as before there was a knob. A bare origin
 * is never invented — this app cannot know which sibling serves the site root.
 *
 * A plain function, not a hook and not a context: fixed for the life of the
 * document, and importing nothing from `@tanstack/react-start`.
 */

/** The environment variable naming the main app's public URL. */
export const WEB_APP_URL_VAR = "WALLOW_WEB_URL";

/** The global the shell publishes the resolved URL on, for the browser to read back. */
export const WEB_APP_URL_GLOBAL_KEY = "__WALLOW_WEB_APP_URL__";

/** `<` as a JavaScript string escape — the one character an inline script must not carry. */
const LT_ESCAPE = String.raw`\u003c`;

/**
 * The configured URL, or `undefined` when unset, blank, or not an absolute
 * `http(s)` URL — an unsubstituted `WALLOW_WEB_URL=` in a compose file is
 * "unset", and a value that would not survive `location.href` is not a
 * destination.
 */
export function resolveWebAppUrl(
  env: Readonly<Record<string, string | undefined>> = {},
): string | undefined {
  const raw: string | undefined = env[WEB_APP_URL_VAR];
  if (raw === undefined) {
    return undefined;
  }

  const candidate: string = raw.trim();
  if (candidate === "") {
    return undefined;
  }

  let parsed: URL;
  try {
    parsed = new URL(candidate);
  } catch {
    return undefined;
  }

  return parsed.protocol === "http:" || parsed.protocol === "https:" ? parsed.href : undefined;
}

/**
 * The source of the inline `<script>` publishing one deployment's answer, with
 * `<` escaped so a hostile value cannot end the element early.
 */
export function webAppUrlScript(url: string | undefined): string {
  const payload: string = JSON.stringify(url ?? null);
  return `window[${JSON.stringify(WEB_APP_URL_GLOBAL_KEY)}]=${payload.replaceAll("<", LT_ESCAPE)};`;
}

/** The URL {@link webAppUrlScript} published on `scope`, or `undefined` for anything else. */
export function readInjectedWebAppUrl(scope: unknown): string | undefined {
  if (typeof scope !== "object" || scope === null) {
    return undefined;
  }

  const injected: unknown = (scope as Record<string, unknown>)[WEB_APP_URL_GLOBAL_KEY];
  return typeof injected === "string" && injected !== "" ? injected : undefined;
}

/** This document's main-app URL, as any component in the tree sees it. */
export function webAppUrl(): string | undefined {
  return readInjectedWebAppUrl(globalThis);
}
