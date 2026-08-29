/**
 * PROTOTYPE — throwaway. Pieces the three variants share: the one-time secret
 * reveal, the scope picker over the org's catalog, and the "what would be sent"
 * request preview that surfaces the merged API shape after every action.
 */
import { Badge, Button, NoticeBanner, Text, Textarea, Toggle, ToggleGroup } from "@bc-solutions-coder/ui";
import { useState } from "react";

import {
  envBlock,
  PLATFORM,
  PROPOSED_API,
  SCOPE_CATALOG,
  type ClientKind,
  type RegisterClientRequest,
  type SecretReveal,
} from "./model";

export function KindBadge(props: { kind: ClientKind }) {
  return props.kind === "application" ? (
    <Badge variant="success">application</Badge>
  ) : (
    <Badge variant="neutral">service account</Badge>
  );
}

export function CopyButton(props: { text: string; label?: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <Button
      type="button"
      variant="secondary"
      className="w-auto"
      onClick={() => {
        void navigator.clipboard.writeText(props.text).then(() => {
          setCopied(true);
          setTimeout(() => setCopied(false), 1500);
        });
      }}
    >
      {copied ? "Copied" : (props.label ?? "Copy")}
    </Button>
  );
}

/** The one-time reveal: id + secret + env block + quickstart link. */
export function SecretRevealCard(props: { reveal: SecretReveal; onDone: () => void }) {
  const { reveal } = props;
  const block = envBlock(reveal, reveal.client);
  const quickstart =
    reveal.client.kind === "application" ? PLATFORM.quickstartUrl : PLATFORM.serviceQuickstartUrl;
  return (
    <div className="space-y-4 rounded-lg border-2 border-warning bg-card p-6">
      <Text as="h3" variant="subheading">
        {reveal.reason === "registered" ? "Registered" : "Secret rotated"}: {reveal.client.name}
      </Text>
      <NoticeBanner tone="warning">
        <Text as="p" variant="bodySm">
          Save the secret now — it is shown once. Rotating it later invalidates this one.
        </Text>
      </NoticeBanner>
      <div className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
        <Text as="span" variant="bodySm" color="muted">
          Client id
        </Text>
        <Text as="code">{reveal.client.clientId}</Text>
        <Text as="span" variant="bodySm" color="muted">
          Client secret
        </Text>
        <Text as="code">{reveal.clientSecret}</Text>
      </div>
      <Textarea readOnly value={block} rows={block.split("\n").length} className="font-mono text-xs" />
      <div className="flex flex-wrap gap-3">
        <CopyButton text={block} label="Copy .env block" />
        <Button
          render={<a href={quickstart} target="_blank" rel="noreferrer" />}
          nativeButton={false}
          variant="secondary"
          className="w-auto no-underline"
        >
          Open quickstart
        </Button>
        <Button type="button" className="w-auto" onClick={props.onDone}>
          I saved it
        </Button>
      </div>
    </div>
  );
}

/** Scope picker over the org's catalog; platform-only scopes render disabled. */
export function ScopePicker(props: { value: string[]; onChange: (next: string[]) => void }) {
  const categories = [...new Set(SCOPE_CATALOG.map((s) => s.category))];
  return (
    <div className="space-y-3">
      {categories.map((category) => (
        <div key={category} className="space-y-1">
          <Text as="span" variant="bodySm" color="muted">
            {category}
          </Text>
          <ToggleGroup multiple value={props.value} onValueChange={props.onChange}>
            {SCOPE_CATALOG.filter((s) => s.category === category).map((s) => (
              <Toggle key={s.code} value={s.code} disabled={s.platformOnly} title={s.description}>
                {s.code}
                {s.platformOnly ? " (platform only)" : ""}
              </Toggle>
            ))}
          </ToggleGroup>
        </div>
      ))}
    </div>
  );
}

/** Surfaces the state: the merged route + the body the form would send. */
export function RequestPreview(props: { request: RegisterClientRequest; orgId: string }) {
  const [open, setOpen] = useState(false);
  const body = JSON.stringify(props.request, null, 2);
  return (
    <div className="rounded-md border border-dashed border-border p-3">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="text-left"
        data-testid="proto-request-preview-toggle"
      >
        <Text as="span" variant="bodySm" color="muted">
          {open ? "▾" : "▸"} PROTOTYPE · what this would send · {PROPOSED_API.register.replace("{orgId}", props.orgId)}
        </Text>
      </button>
      {open ? (
        <div className="mt-2 space-y-2">
          <Textarea readOnly value={body} rows={body.split("\n").length} className="font-mono text-xs" />
          <Textarea
            readOnly
            value={Object.values(PROPOSED_API).join("\n")}
            rows={Object.values(PROPOSED_API).length}
            className="font-mono text-xs"
          />
        </div>
      ) : null}
    </div>
  );
}

export function FieldRow(props: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-1">
      <Text as="span" variant="bodySm" weight="medium">
        {props.label}
      </Text>
      {props.children}
      {props.hint ? (
        <Text as="span" variant="bodySm" color="muted">
          {props.hint}
        </Text>
      ) : null}
    </label>
  );
}
