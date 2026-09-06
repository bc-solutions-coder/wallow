import {
  ApiFailure,
  ErrorCode,
  defineFailureMessages,
  resolveFailureMessage,
} from "@bc-solutions-coder/api-errors";
import { byTestId } from "@bc-solutions-coder/testing/locators";
import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it } from "vitest";

import {
  FailureMessagesProvider,
  useFailureMessage,
  type UseFailureMessageOptions,
} from "./failure-messages";

/*
 * The registry hook resolves the sentence for a failure through the provider's
 * registry, falling back to api-errors' shipped copy when no provider is
 * mounted. A provider REPLACES the registry above it; nothing merges.
 */

const UNAUTHENTICATED = new ApiFailure({
  status: 401,
  code: ErrorCode.AUTH_UNAUTHENTICATED,
  title: "Unauthorized",
});

const TEAPOT = new ApiFailure({ status: 418, code: "Kitchen.Teapot", title: "Teapot" });

function Probe({
  error,
  options,
}: {
  readonly error: unknown;
  readonly options?: UseFailureMessageOptions;
}): ReactElement {
  const message = useFailureMessage(error, options);
  return <output data-testid="message">{message ?? "(null)"}</output>;
}

describe("useFailureMessage", () => {
  it("returns null for a nullish error", async () => {
    await render(
      <>
        <Probe error={null} />
        <Probe error={undefined} />
      </>,
    );

    for (const output of document.querySelectorAll('[data-testid="message"]')) {
      expect(output.textContent).toBe("(null)");
    }
  });

  it("resolves through the shipped copy when no provider is mounted", async () => {
    await render(<Probe error={UNAUTHENTICATED} />);

    expect(byTestId("message").textContent).toBe(resolveFailureMessage(UNAUTHENTICATED));
  });

  it("resolves through the provider's registry", async () => {
    const registry = defineFailureMessages({
      [ErrorCode.AUTH_UNAUTHENTICATED]: () => "Registry says sign in.",
    });

    await render(
      <FailureMessagesProvider registry={registry}>
        <Probe error={UNAUTHENTICATED} />
      </FailureMessagesProvider>,
    );

    expect(byTestId("message").textContent).toBe("Registry says sign in.");
  });

  it("lets call-site messages win over the registry and honours the fallback", async () => {
    const registry = defineFailureMessages({
      [ErrorCode.AUTH_UNAUTHENTICATED]: () => "Registry says sign in.",
    });

    await render(
      <FailureMessagesProvider registry={registry}>
        <Probe
          error={UNAUTHENTICATED}
          options={{ messages: { [ErrorCode.AUTH_UNAUTHENTICATED]: () => "Call site wins." } }}
        />
        <Probe error={TEAPOT} options={{ fallback: "Short and stout." }} />
      </FailureMessagesProvider>,
    );

    const [callSite, fallback] = document.querySelectorAll('[data-testid="message"]');
    expect(callSite?.textContent).toBe("Call site wins.");
    expect(fallback?.textContent).toBe("Short and stout.");
  });

  it("replaces, never merges, a registry above it", async () => {
    const outer = defineFailureMessages({
      [ErrorCode.AUTH_UNAUTHENTICATED]: () => "Outer registry.",
    });
    const inner = defineFailureMessages({ "Kitchen.Teapot": () => "Inner registry." });

    await render(
      <FailureMessagesProvider registry={outer}>
        <FailureMessagesProvider registry={inner}>
          <Probe error={UNAUTHENTICATED} />
        </FailureMessagesProvider>
      </FailureMessagesProvider>,
    );

    expect(byTestId("message").textContent).toBe(resolveFailureMessage(UNAUTHENTICATED));
  });
});
