/**
 * PROTOTYPE — Variant C: "Connect wizard with live .env preview".
 *
 * Registration is a two-column "Connect your app" surface: the form on the
 * left, a LIVE .env block on the right that fills in as you type (secret and
 * id as placeholders until you register). One switch flips between application
 * and service account. Submit fills the secret into the same preview — no
 * separate reveal screen. Existing clients are compact chips above; each chip
 * has "Regenerate", which reuses the same preview for the rotated secret.
 * Branding and logout URIs are deferred to a "Settings" link, not asked here.
 */
import { Button, Input, Text, Textarea, Toggle, ToggleGroup } from "@bc-solutions-coder/ui";
import { useState } from "react";

import {
  EMPTY_REQUEST,
  envBlock,
  PLATFORM,
  type ClientKind,
  type ProtoClient,
  type RegisterClientRequest,
  type SecretReveal,
} from "./model";
import { CopyButton, FieldRow, KindBadge, RequestPreview, ScopePicker } from "./shared";

export const VARIANT_C_NAME = "Connect wizard, live .env preview";

interface VariantProps {
  orgId: string;
  clients: ProtoClient[];
  reveal: SecretReveal | null;
  onRegister: (request: RegisterClientRequest) => void;
  onRotate: (client: ProtoClient) => void;
  onDismissReveal: () => void;
}

export function VariantC(props: VariantProps) {
  const [kind, setKind] = useState<ClientKind>("application");
  const [name, setName] = useState("");
  const [redirect, setRedirect] = useState("");
  const [scopes, setScopes] = useState<string[]>([]);

  const draft: RegisterClientRequest = {
    ...EMPTY_REQUEST,
    kind,
    name,
    redirectUris: kind === "application" && redirect ? [redirect] : [],
    scopes,
  };
  const valid = name.trim() !== "" && scopes.length > 0 && (kind !== "application" || redirect.startsWith("http"));
  const block = envBlock(props.reveal, draft);
  const revealed = props.reveal !== null;

  return (
    <section className="space-y-6">
      <div className="space-y-2">
        <Text as="h2" variant="subheading">
          Connected clients
        </Text>
        <div className="flex flex-wrap gap-2">
          {props.clients.map((c) => (
            <div key={c.id} className="flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1">
              <KindBadge kind={c.kind} />
              <Text as="span" variant="bodySm" weight="medium">
                {c.name}
              </Text>
              <Text as="code" className="text-xs">
                {c.clientId}
              </Text>
              <button type="button" className="text-xs underline" onClick={() => props.onRotate(c)}>
                Regenerate secret
              </button>
              <button type="button" className="text-xs underline">
                Settings
              </button>
            </div>
          ))}
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="space-y-4 rounded-lg border border-border bg-card p-6">
          <Text as="h3" variant="body" weight="medium">
            {revealed ? "Registered" : "Connect a new client"}
          </Text>
          {revealed ? (
            <div className="space-y-3">
              <Text as="p" variant="bodySm">
                The .env block on the right now holds the real id and secret. Copy it before leaving — the secret is not
                shown again.
              </Text>
              <Button type="button" className="w-auto" onClick={props.onDismissReveal}>
                Done, hide secret
              </Button>
            </div>
          ) : (
            <>
              <ToggleGroup value={[kind]} onValueChange={(v) => v[0] && setKind(v[0] as ClientKind)}>
                <Toggle value="application">My app logs users in</Toggle>
                <Toggle value="service-account">My server calls the API on its own</Toggle>
              </ToggleGroup>
              <FieldRow label="Name">
                <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Website" />
              </FieldRow>
              {kind === "application" ? (
                <FieldRow label="Your app's callback URL" hint="Usually https://<your-domain>/bff/callback">
                  <Input value={redirect} onChange={(e) => setRedirect(e.target.value)} placeholder="https://bcordes.dev/bff/callback" />
                </FieldRow>
              ) : null}
              <FieldRow label="What may it access?">
                <ScopePicker value={scopes} onChange={setScopes} />
              </FieldRow>
              <Text as="p" variant="bodySm" color="muted">
                Logout URLs and login-screen branding live under Settings after you connect.
              </Text>
              <Button type="button" className="w-auto" disabled={!valid} onClick={() => props.onRegister(draft)}>
                Connect
              </Button>
              <RequestPreview request={draft} orgId={props.orgId} />
            </>
          )}
        </div>

        <div className="space-y-3 rounded-lg border border-border bg-card p-6">
          <div className="flex items-center justify-between">
            <Text as="h3" variant="body" weight="medium">
              .env — {revealed ? "ready to paste" : "preview"}
            </Text>
            {revealed ? <CopyButton text={block} label="Copy" /> : null}
          </div>
          <Textarea readOnly value={block} rows={block.split("\n").length + 1} className="font-mono text-xs" />
          <Text as="p" variant="bodySm" color="muted">
            Then: <Text as="code">pnpm add @bc-solutions-coder/sdk</Text>, mount{" "}
            {kind === "application" ? (
              <>
                <Text as="code">/bff/*</Text> and <Text as="code">/api/*</Text>
              </>
            ) : (
              <Text as="code">createServiceClient()</Text>
            )}
            , done.{" "}
            <a href={kind === "application" ? PLATFORM.quickstartUrl : PLATFORM.serviceQuickstartUrl} target="_blank" rel="noreferrer">
              Quickstart
            </a>
          </Text>
        </div>
      </div>
    </section>
  );
}
