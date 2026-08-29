/**
 * PROTOTYPE — Variant B: "One table, register panel, Setup takeover".
 *
 * A single Clients table (kind as a badge column). One Register button opens a
 * side panel that asks ONLY for what registration needs — kind, name, the
 * redirect URI(s) for an application, scopes — everything else is "edit later".
 * Success replaces the whole section with a Setup page: tabs for Environment /
 * Install & mount / Done. Clicking a row opens the same panel in edit mode with
 * the later fields (post-logout, back-channel, branding) and Rotate secret.
 */
import { Button, Input, Tabs, Text, Textarea, Toggle, ToggleGroup } from "@bc-solutions-coder/ui";
import { useState } from "react";

import {
  EMPTY_REQUEST,
  envBlock,
  PLATFORM,
  splitLines,
  type ClientKind,
  type ProtoClient,
  type RegisterClientRequest,
  type SecretReveal,
} from "./model";
import { CopyButton, FieldRow, KindBadge, RequestPreview, ScopePicker } from "./shared";

export const VARIANT_B_NAME = "One table, register panel, Setup takeover";

interface VariantProps {
  orgId: string;
  clients: ProtoClient[];
  reveal: SecretReveal | null;
  onRegister: (request: RegisterClientRequest) => void;
  onRotate: (client: ProtoClient) => void;
  onDismissReveal: () => void;
}

function Panel(props: {
  orgId: string;
  editing: ProtoClient | null;
  onClose: () => void;
  onRegister: (request: RegisterClientRequest) => void;
  onRotate: (client: ProtoClient) => void;
}) {
  const { editing } = props;
  const [kind, setKind] = useState<ClientKind>(editing?.kind ?? "application");
  const [name, setName] = useState(editing?.name ?? "");
  const [redirects, setRedirects] = useState(editing?.redirectUris.join("\n") ?? "");
  const [scopes, setScopes] = useState<string[]>(editing?.scopes ?? []);
  const [postLogout, setPostLogout] = useState(editing?.postLogoutRedirectUris.join("\n") ?? "");
  const [backchannel, setBackchannel] = useState(editing?.backchannelLogoutUri ?? "");
  const [brandName, setBrandName] = useState(editing?.branding?.displayName ?? "");

  const request: RegisterClientRequest = {
    ...EMPTY_REQUEST,
    kind,
    name,
    redirectUris: kind === "application" ? splitLines(redirects) : [],
    postLogoutRedirectUris: editing ? splitLines(postLogout) : [],
    backchannelLogoutUri: editing && backchannel ? backchannel : null,
    scopes,
    branding: editing && brandName ? { displayName: brandName, tagline: "" } : null,
  };
  const valid = name.trim() !== "" && scopes.length > 0 && (kind !== "application" || request.redirectUris.length > 0);

  return (
    <div className="fixed inset-y-0 right-0 z-40 w-full max-w-lg overflow-y-auto border-l border-border bg-card p-6 shadow-xl">
      <div className="space-y-5">
        <div className="flex items-center justify-between">
          <Text as="h2" variant="subheading">
            {editing ? `Edit ${editing.name}` : "Register a client"}
          </Text>
          <Button type="button" variant="secondary" className="w-auto" onClick={props.onClose}>
            Close
          </Button>
        </div>

        {editing ? (
          <div className="flex items-center gap-3">
            <KindBadge kind={editing.kind} />
            <Text as="code" className="text-xs">
              {editing.clientId}
            </Text>
          </div>
        ) : (
          <ToggleGroup value={[kind]} onValueChange={(v) => v[0] && setKind(v[0] as ClientKind)}>
            <Toggle value="application">Application — logs users in</Toggle>
            <Toggle value="service-account">Service account — calls the API as itself</Toggle>
          </ToggleGroup>
        )}

        <FieldRow label="Name">
          <Input value={name} onChange={(e) => setName(e.target.value)} disabled={editing !== null} />
        </FieldRow>
        {kind === "application" ? (
          <FieldRow label="Redirect URI(s)" hint="Where Wallow sends the user back after login. One per line.">
            <Textarea rows={2} value={redirects} onChange={(e) => setRedirects(e.target.value)} />
          </FieldRow>
        ) : null}
        <FieldRow label="Scopes">
          <ScopePicker value={scopes} onChange={setScopes} />
        </FieldRow>

        {editing ? (
          <div className="space-y-4 border-t border-border pt-4">
            <Text as="h3" variant="body" weight="medium">
              Later settings
            </Text>
            {editing.kind === "application" ? (
              <>
                <FieldRow label="Post-logout redirect URIs">
                  <Textarea rows={2} value={postLogout} onChange={(e) => setPostLogout(e.target.value)} />
                </FieldRow>
                <FieldRow label="Back-channel logout URI">
                  <Input value={backchannel} onChange={(e) => setBackchannel(e.target.value)} />
                </FieldRow>
                <FieldRow label="Login-screen display name (branding)">
                  <Input value={brandName} onChange={(e) => setBrandName(e.target.value)} />
                </FieldRow>
              </>
            ) : null}
            <div className="flex gap-3">
              <Button type="button" className="w-auto" onClick={props.onClose}>
                Save
              </Button>
              <Button type="button" variant="destructive" className="w-auto" onClick={() => props.onRotate(editing)}>
                Rotate secret
              </Button>
            </div>
          </div>
        ) : (
          <>
            <Text as="p" variant="bodySm" color="muted">
              Post-logout URIs, back-channel logout and login-screen branding can be set after registration.
            </Text>
            <Button type="button" className="w-auto" disabled={!valid} onClick={() => props.onRegister(request)}>
              Register and show secret
            </Button>
          </>
        )}
        <RequestPreview request={request} orgId={props.orgId} />
      </div>
    </div>
  );
}

function SetupPage(props: { reveal: SecretReveal; onDone: () => void }) {
  const { reveal } = props;
  const block = envBlock(reveal, reveal.client);
  const isApp = reveal.client.kind === "application";
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Text as="h2" variant="subheading">
          Set up {reveal.client.name}
        </Text>
        <KindBadge kind={reveal.client.kind} />
      </div>
      <Tabs.Root defaultValue="env">
        <Tabs.List>
          <Tabs.Tab value="env">1 · Environment</Tabs.Tab>
          <Tabs.Tab value="mount">2 · {isApp ? "Install & mount" : "Install & call"}</Tabs.Tab>
          <Tabs.Tab value="done">3 · Done</Tabs.Tab>
          <Tabs.Indicator />
        </Tabs.List>
        <Tabs.Panel value="env" className="space-y-3 pt-4">
          <Text as="p" variant="bodySm">
            Paste into your server environment. The secret is shown only here, only now.
          </Text>
          <Textarea readOnly value={block} rows={block.split("\n").length} className="font-mono text-xs" />
          <CopyButton text={block} label="Copy .env block" />
        </Tabs.Panel>
        <Tabs.Panel value="mount" className="space-y-3 pt-4">
          <Textarea
            readOnly
            className="font-mono text-xs"
            rows={isApp ? 9 : 7}
            value={
              isApp
                ? [
                    "pnpm add @bc-solutions-coder/sdk   # needs a GitHub Packages read token in ~/.npmrc",
                    "",
                    "// src/routes/bff/$.ts   and   src/routes/api/$.ts",
                    'import { createWallowBffServer } from "@bc-solutions-coder/sdk/server";',
                    "const server = createWallowBffServer();",
                    "export const Route = createFileRoute('/bff/$')({ server: { handlers: { ANY: ({ request }) => server.handleBff(request) } } });",
                    "",
                    `// then: <a href="/bff/login">Sign in</a>  — register ${reveal.client.redirectUris[0] ?? "<redirect uri>"} as the callback`,
                  ].join("\n")
                : [
                    "pnpm add @bc-solutions-coder/sdk",
                    "",
                    'import { createServiceClient } from "@bc-solutions-coder/sdk/server/service";',
                    "const wallow = createServiceClient();           // reads OIDC_SERVICE_* from env",
                    "await inquiriesCreate({ client: wallow.client, body: { ... } });",
                  ].join("\n")
            }
          />
        </Tabs.Panel>
        <Tabs.Panel value="done" className="space-y-3 pt-4">
          <Text as="p" variant="bodySm">
            {isApp
              ? "Open your app, click Sign in, and you land back on your redirect URI with a session. Full guide:"
              : "Run your job — the first API call fetches and caches a token. Full guide:"}
          </Text>
          <Text as="code" className="text-xs">
            {isApp ? PLATFORM.quickstartUrl : PLATFORM.serviceQuickstartUrl}
          </Text>
          <div>
            <Button type="button" className="w-auto" onClick={props.onDone}>
              Back to clients
            </Button>
          </div>
        </Tabs.Panel>
      </Tabs.Root>
    </div>
  );
}

export function VariantB(props: VariantProps) {
  const [panel, setPanel] = useState<"closed" | "register" | ProtoClient>("closed");

  if (props.reveal) {
    return <SetupPage reveal={props.reveal} onDone={props.onDismissReveal} />;
  }

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <Text as="h2" variant="subheading">
          Clients
        </Text>
        <Button type="button" className="w-auto" onClick={() => setPanel("register")}>
          Register client
        </Button>
      </div>
      <div className="overflow-x-auto rounded-lg border border-border bg-card">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border text-left">
              <th className="p-3 font-medium">Name</th>
              <th className="p-3 font-medium">Kind</th>
              <th className="p-3 font-medium">Client id</th>
              <th className="p-3 font-medium">Scopes</th>
              <th className="p-3 font-medium">Secret</th>
            </tr>
          </thead>
          <tbody>
            {props.clients.map((c) => (
              <tr
                key={c.id}
                className="cursor-pointer border-b border-border last:border-0 hover:bg-accent/40"
                onClick={() => setPanel(c)}
              >
                <td className="p-3">{c.name}</td>
                <td className="p-3">
                  <KindBadge kind={c.kind} />
                </td>
                <td className="p-3 font-mono text-xs">{c.clientId}</td>
                <td className="p-3 text-xs">{c.scopes.join(", ")}</td>
                <td className="p-3 text-xs">{c.secretRotatedAt ? `rotated ${c.secretRotatedAt.slice(0, 10)}` : `issued ${c.createdAt.slice(0, 10)}`}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {panel !== "closed" ? (
        <Panel
          orgId={props.orgId}
          editing={panel === "register" ? null : panel}
          onClose={() => setPanel("closed")}
          onRegister={(request) => {
            props.onRegister(request);
            setPanel("closed");
          }}
          onRotate={(client) => {
            props.onRotate(client);
            setPanel("closed");
          }}
        />
      ) : null}
    </section>
  );
}
