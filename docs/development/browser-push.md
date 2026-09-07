# Browser Web Push

Wallow sends encrypted browser notifications for authenticated users in an organization.
The consumer's BFF owns authentication; the browser never needs a Wallow access token or
VAPID private key. Web Push uses platform `2`; FCM and APNs continue to use native token strings.

## Provision organization signing keys

An administrator with `PushConfigWrite` calls
`POST /v1/admin/push/config/web-push/keys` with
`{"subject":"mailto:push@example.com"}`. Wallow generates a P-256 signing key, encrypts
its private material in organization credential storage, and returns only `keyId` and
`publicKey`. Use a real monitored contact address or HTTPS contact URL for the subject.
The first key enables a new configuration; rotation preserves an existing configuration's
enabled/disabled state.

Ordinary users discover the current public key at
`GET /v1/push/web-push/public-key`. Missing, disabled or fully retired configuration returns
`409 WebPush.Unavailable`. Public keys are not secrets; the endpoint's authentication enforces
organization selection. Neither discovery nor key-management responses return private keys.

One organization owns the signing identity. Delivery reaches a user's active registrations
across that organization, not just one developer application. Applications sharing a browser
subscription must share its signing identity. This integration supports a single organization
per browser application; it does not implement organization switching or application-specific
push audiences.

## Subscribe through the SDK

Mount the SDK BFF API proxy as usual. Serve a bundled service worker at `/push-worker.js`
from the consumer origin, with scope `/`. Install/register it before enabling the subscription
button. Call the following function from a user click. Browser permission and subscription
must be voluntary. HTTPS is required outside browser-supported localhost development.

```ts
import {
  createWallowSdk,
  pushDevicesDeregisterDevice,
  pushDevicesGetUserDevices,
  pushDevicesGetWebPushPublicKey,
  pushDevicesRegisterDevice,
} from "@bc-solutions-coder/sdk";

export async function subscribeToPush() {
  if (await Notification.requestPermission() !== "granted") return;
  const sdk = createWallowSdk({ baseUrl: "/api" });
  const registration = await navigator.serviceWorker.ready;
  const current = await pushDevicesGetWebPushPublicKey({ client: sdk.client });
  let subscription = await registration.pushManager.getSubscription();
  const devices = await pushDevicesGetUserDevices({ client: sdk.client });
  const device = devices.find(item => item.token === subscription?.endpoint);

  // A new applicationServerKey requires a new browser subscription.
  const browserKey = subscription?.options.applicationServerKey;
  const encodedKey = browserKey
    ? btoa(String.fromCharCode(...new Uint8Array(browserKey)))
        .replaceAll("+", "-").replaceAll("/", "_").replace(/=+$/, "")
    : null;
  if (subscription && encodedKey !== current.publicKey) {
    if (device) {
      await pushDevicesDeregisterDevice({ client: sdk.client, path: { id: device.id } });
    }
    await subscription.unsubscribe();
    subscription = null;
  }
  subscription ??= await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: current.publicKey,
  });
  const serialized = subscription.toJSON();
  const p256dh = serialized.keys?.p256dh;
  const auth = serialized.keys?.auth;
  if (!p256dh || !auth) throw new Error("Browser did not provide subscription keys");
  await pushDevicesRegisterDevice({
    client: sdk.client,
    body: {
      platform: 2,
      subscription: {
        endpoint: subscription.endpoint,
        expirationTime: subscription.expirationTime,
        keys: { p256dh, auth },
      },
      signingKeyId: current.keyId,
    },
  });
}
```

Web Push requests omit `token`. The typed `subscription` contains the browser's endpoint,
keys and optional expiration time; `signingKeyId` identifies Wallow's signing version.
Requests with native tokens and browser subscription fields mixed together fail validation.
Endpoint identity determines repeat registration, independently of JSON field ordering.
An active endpoint cannot be claimed by another user. Updated encryption material creates a
new registration ID so queued work for the old registration cannot reach its replacement.
Endpoints must use HTTPS on port 443. The sender blocks private/internal destinations,
redirects and DNS rebinding; arbitrary vendor-specific endpoint formats are not assumed.

For unsubscribe, get the current browser subscription, find its endpoint in
`pushDevicesGetUserDevices`, deregister that ID, then call `subscription.unsubscribe()`.
Remove the server association before logout/account switching while the user's session is
still available; otherwise the old user's subscription can remain active. If any operation
fails, surface it and let the user retry. Do not claim unsubscribe succeeded after an API failure.
On a later visit, reconcile `getSubscription()` with server registration; do not rely solely
on `pushsubscriptionchange`, since an inactive service worker may lack a usable BFF session.

## Display and open a notification

Bundle this worker with the SDK. A push payload has `title`, `body` and optional `clickPath`.
The destination is a local absolute path, resolved against the **consumer** origin. It is
not resolved against Wallow's API or the browser vendor's push-service origin.

```js
import { resolveWebPushClickUrl } from "@bc-solutions-coder/sdk";

self.addEventListener("push", event => {
  let payload;
  try { payload = event.data?.json(); } catch { payload = null; }
  const title = typeof payload?.title === "string" ? payload.title : "Notification";
  const body = typeof payload?.body === "string" ? payload.body : "Open the app for details.";
  const url = resolveWebPushClickUrl(payload?.clickPath, self.location.origin);
  event.waitUntil(self.registration.showNotification(title, { body, data: { url } }));
});

self.addEventListener("notificationclick", event => {
  event.notification.close();
  // Revalidate notification data at the navigation boundary.
  const stored = event.notification.data?.url;
  const path = typeof stored === "string" && stored.startsWith(self.location.origin + "/")
    ? stored.slice(self.location.origin.length) : "/";
  const url = resolveWebPushClickUrl(path, self.location.origin);
  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
    const existing = windows.find(window => window.url === url);
    if (existing) await existing.focus();
    else await self.clients.openWindow(url);
  })());
});
```

This is the classic service-worker integration. Always display a visible notification for
push receipt. Action-button UI and declarative push are outside this contract. The SDK's
`resolveWebPushClickUrl` rejects external, scheme-relative and backslash destinations and
falls back to the consumer home page.

Use `pushDevicesSendPush` with `title`, `body`, `notificationType` and optional `clickPath`
for an ordinary user's self-test. It targets only that user, respects push preferences, and
returns queue acceptance. Keep JSON payloads within 3993 UTF-8 bytes, including field names
and escaping; encryption overhead raises this to the interoperable 4096-byte wire limit.

## Rotate and retire keys

1. Call the privileged key-creation endpoint again. It makes a new current version and retains
   earlier versions for existing subscriptions.
2. On a consumer visit, run the reconciliation above to create a fresh subscription with the
   current public key. Existing active registrations can repeat-register their retained version;
   new registrations must use the current version.
3. Inspect `GET /v1/admin/push/config/web-push/keys`. It returns public metadata with `current`
   and `retired` flags. After the chosen migration window, call
   `DELETE /v1/admin/push/config/web-push/keys/{keyId}` to retire an old version. Repeating this
   operation is safe. It removes private signing material but retains an immutable public
   tombstone; a retired key cannot be revived under another identifier.
4. Dormant browsers still using that version must resubscribe on their next visit. Retiring
   the current version leaves no discoverable current key until a new key is generated.

For compromise, retire the affected key immediately instead of waiting for migration.
A request already sent to a push service cannot be recalled by retirement. Retirement prevents
subsequent sender attempts from using that key; it cannot revoke an attacker's stolen private
key at every vendor push service. Clients must replace the affected browser subscriptions.

The generic credential-upload API remains available to trusted administrators for provisioning
and validates the complete keyring. Preserve every previous public key/version, set retired
private keys to null, and never reuse identifiers or public keys. Whole Web Push configuration
deletion is rejected to preserve retirement history; disable the configuration or retire its
keys instead. Native-platform configuration deletion is unaffected. Organization credentials are authoritative; there is no deployment-level Web Push key fallback.

## Understand results and verify delivery

A push-service success is `Accepted`, not proof of browser receipt or display. Missing or
invalid configuration fails explicitly rather than succeeding through a log-only provider.
Unavailable registrations are skipped when healthy targets exist; a self-test with registered
targets but none eligible returns `409 WebPush.Unavailable`.
404/410 delivery responses deactivate only the matching registration version. Authentication
errors preserve subscriptions; 429 and transient server/network failures schedule at most two
retries, with provider Retry-After delays bounded to one day. Each attempt rechecks the current
registration and signing-key state. Endpoints, encryption secrets and raw vendor error bodies
are not included in the Web Push provider's logs/errors.

For acceptance, use a provisioned browser subscription and its actual push service. Close the
consumer tab, send a self-test, observe the visible notification, and click it to verify the
consumer destination. Also verify unsubscribe/re-registration and rotation. Record browser/OS
and permission prerequisites. iOS/iPadOS Web Push requires a supported Home Screen web app.
A mocked provider, DevTools-generated push event or successful HTTP request does not replace
this browser check. One browser run does not certify all browsers.

Protocol references: [RFC 8291](https://www.rfc-editor.org/rfc/rfc8291.html),
[RFC 8292](https://www.rfc-editor.org/rfc/rfc8292.html),
[RFC 8030](https://www.rfc-editor.org/rfc/rfc8030.html), and
[WebKit's browser prerequisites](https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/).
