import { describe, expect, it } from "vitest";
import { resolveWebPushClickUrl } from "./web-push";

describe("notification clicks", () => {
  it("resolves a destination on the consumer origin", () => {
    expect(resolveWebPushClickUrl("/messages/42", "https://consumer.example")).toBe(
      "https://consumer.example/messages/42",
    );
  });
  it.each([
    "//attacker.example",
    "https://attacker.example",
    // Deliberate untrusted notification destination.
    // eslint-disable-next-line no-script-url
    "javascript:alert(1)",
    String.raw`/\\attacker.example`,
    null,
    { url: "/messages" },
  ])("falls back safely for %j", (destination) => {
    expect(resolveWebPushClickUrl(destination, "https://consumer.example")).toBe(
      "https://consumer.example/",
    );
  });
});
