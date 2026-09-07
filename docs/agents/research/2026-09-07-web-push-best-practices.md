# Web Push recommendations for #221

**Research completed:** 2026-09-07. **Implementation decisions:** pending.

This reviews the proposed subscription format, key ownership and rotation, and browser behavior against primary sources. It does not establish successful browser delivery or authorize implementation.

## Revise the subscription contract

The browser's standard serialization contains `endpoint`, nullable `expirationTime`, and `keys` (`p256dh` and `auth`). It does not include Wallow ownership or a VAPID key identifier. A service-worker registration has an associated subscription; calling `subscribe()` with different options when one already exists rejects with `InvalidStateError`. These are browser API semantics, not tenant semantics. The cited December 2025 specification is a Working Draft. [W3C Push API, subscription and serialization](https://www.w3.org/TR/push-api/#pushsubscription-interface), [subscribe algorithm](https://www.w3.org/TR/push-api/#dom-pushmanager-subscribe)

**Recommendation:** accept a typed Web Push subscription object, preserving the browser field names. Keep native-platform tokens as strings. JSON encoded inside `token` would work as a Wallow-specific envelope, but is not a standard or a best-practice requirement. Given the repository's pre-release policy, compatibility alone does not justify that indirection. Store endpoint identity separately from encryption material; do not make JSON property order or optional fields determine ownership or repeat-registration identity. These are API/storage design recommendations inferred from the browser's structured contract.

## Revise key ownership; make rotation explicit

Google describes application-server keys as application keys: distribute the public key and keep the private key secret. An authenticated public-key endpoint is a possible Wallow authorization/discovery policy, not a confidentiality requirement for the public key itself. Do not generate keys per subscription or place private keys in browser bundles or consumer images. [Google application-server key guidance](https://web.dev/articles/push-notifications-subscribing-a-user#applicationserverkey_option)

RFC 8292 identifies the application server, not an organization or web origin. A restricted subscription requires the signing key used at subscription creation. Replacing that key requires a **new browser subscription**, and the sender must remember which key belongs to each subscription. The RFC also notes that gradual migration can reduce disruption to push-service reputation. [RFC 8292 §§4.2–5](https://www.rfc-editor.org/rfc/rfc8292.html#section-4.2)

**Recommendation:** choose a stable application-level signing identity aligned with the actual consumer deployment. Organization-owned keys are reasonable when organizations have separate applications/subscription scopes; they are not a universal default. One browser application switching organizations cannot silently replace its subscription's signing key. Settle whether Wallow supports that case before choosing credential ownership; no new credential-management architecture is justified by the RFC alone.

For routine rotation, record a server-managed key version on each subscription, publish the current public key/version, and explicitly unsubscribe/resubscribe clients during migration. Retain old signing keys only for a documented migration window, with a retirement policy for dormant clients. For compromise, retire the compromised key immediately and require fresh subscriptions; retaining it for continuity defeats that response. These retention and compromise policies are Wallow recommendations, not prescribed rotation schedules.

## Implement the protocol, not plaintext HTTP

Payload encryption uses the subscription's encryption material and `Content-Encoding: aes128gcm`; VAPID authentication is separate from that encryption. Use an established implementation and verify encrypted output independently. Account for encryption overhead: the RFC's interoperable 4096-byte encrypted-body allowance leaves at most 3993 plaintext bytes without extra padding. [RFC 8291 §§3–4](https://www.rfc-editor.org/rfc/rfc8291.html#section-3)

An HTTP 201 means the push service accepted the message, **not** that a browser received or displayed it. RFC 8030 specifies 404 when sending to an expired subscription and requires HTTPS. Endpoint URLs act as capabilities and need protection against disclosure, including logs. [RFC 8030 §5](https://www.rfc-editor.org/rfc/rfc8030.html#section-5), [§7.3](https://www.rfc-editor.org/rfc/rfc8030.html#section-7.3), [§8](https://www.rfc-editor.org/rfc/rfc8030.html#section-8), [§8.5](https://www.rfc-editor.org/rfc/rfc8030.html#section-8.5)

Mozilla's push-service documentation treats both 404 and 410 endpoints as unusable. It distinguishes authentication, payload and temporary-server failures; those must not all delete subscriptions. [Mozilla Autopush response semantics](https://autopush.readthedocs.io/en/latest/http.html#error-codes)

**Recommendation:** deactivate the exact failed registration on terminal endpoint responses, preserving #222 ownership/concurrency protections. Surface missing configuration as an explicit unavailable result; log-provider success is not browser delivery. Keep transient errors retryable according to provider guidance, separately from authentication/configuration errors.

Because the registered endpoint becomes a server-side HTTP destination, validate HTTPS and enforce an outbound policy that blocks private/internal destinations and redirect bypasses. Consider DNS resolution/rebinding at the network boundary; syntax validation alone is insufficient. Do not hard-code a single browser vendor's URL shape. [OWASP SSRF prevention guidance](https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html)

## Browser behavior and verification

For a classic service-worker implementation, display a user-visible notification on push receipt. Apple documents this requirement, and requests for permission should follow user interaction. Its iOS/iPadOS guidance also documents Home Screen installation requirements. [WebKit: Meet Web Push](https://webkit.org/blog/12945/meet-web-push/), [Web Push on iOS/iPadOS](https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/)

The latest Push API draft also describes declarative push, so a service worker is not a timeless requirement for every possible Web Push implementation. A classic service-worker baseline remains an implementation choice for this ticket; declarative support need not expand its scope. [W3C declarative push](https://www.w3.org/TR/push-api/#declarative-push-message)

Google's click example resolves a destination against the service-worker origin, focuses an existing matching window or opens one, and keeps the worker alive with `event.waitUntil()`. This is a useful baseline. [Google notification patterns](https://web.dev/articles/push-notifications-common-notification-patterns)

**Recommendation:** allow only destinations resolving to the consumer application's origin, with a safe fallback. Validate the resolved URL, not merely a leading slash (`//host` is not same-origin). This is a Wallow safety policy, not a universal Web Push restriction, and the relevant origin is the consumer application, not necessarily the Wallow API. Action buttons are optional; a safe default notification click is enough unless product requirements need more.

Verification should cover actual encrypted request/decryption and authentication, repeat registration, ownership isolation, terminal versus transient responses, missing credentials, routine rotation and unsubscribe. Separately provision a real browser subscription and send through its actual push service; observe the service-worker receipt, displayed notification, and click destination with the app tab closed. A mocked HTTP response, DevTools-synthesized push event, or queue log proves only its own layer. Record browser/OS and tested prerequisites; do not claim all-browser support from one browser's successful run. This test plan is an engineering recommendation derived from the acceptance/delivery distinction above.
