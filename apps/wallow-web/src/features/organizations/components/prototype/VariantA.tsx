/**
 * PROTOTYPE — Variant A: "Two ledgers, inline stepper".
 *
 * Applications and service accounts are two separate lists on the org page,
 * each with its own Register button. Registering expands an inline multi-step
 * form under that list (Basics → Redirects → Scopes → Branding for an
 * application; Basics → Scopes for a service account). Everything is collected
 * up front; the reveal replaces the stepper.
 */
import { Button, Input, ListCard, ListRow, Text, Textarea } from "@bc-solutions-coder/ui";
import { useState } from "react";

import {
  EMPTY_REQUEST,
  splitLines,
  type ClientKind,
  type ProtoClient,
  type RegisterClientRequest,
  type SecretReveal,
} from "./model";
import { FieldRow, KindBadge, RequestPreview, ScopePicker, SecretRevealCard } from "./shared";

export const VARIANT_A_NAME = "Two ledgers, inline stepper";

interface VariantProps {
  orgId: string;
  clients: ProtoClient[];
  reveal: SecretReveal | null;
  onRegister: (request: RegisterClientRequest) => void;
  onRotate: (client: ProtoClient) => void;
  onDismissReveal: () => void;
}

const APP_STEPS = ["Basics", "Redirects", "Scopes", "Branding"] as const;
const SA_STEPS = ["Basics", "Scopes"] as const;

function Stepper(props: {
  kind: ClientKind;
  orgId: string;
  onCancel: () => void;
  onSubmit: (request: RegisterClientRequest) => void;
}) {
  const steps = props.kind === "application" ? APP_STEPS : SA_STEPS;
  const [step, setStep] = useState(0);
  const [draft, setDraft] = useState<RegisterClientRequest>({ ...EMPTY_REQUEST, kind: props.kind });
  const [redirects, setRedirects] = useState("");
  const [postLogout, setPostLogout] = useState("");
  const [brandName, setBrandName] = useState("");
  const [tagline, setTagline] = useState("");

  const request: RegisterClientRequest = {
    ...draft,
    redirectUris: splitLines(redirects),
    postLogoutRedirectUris: splitLines(postLogout),
    branding: brandName.trim() ? { displayName: brandName, tagline } : null,
  };
  const label = steps[step];
  const last = step === steps.length - 1;

  return (
    <div className="space-y-4 rounded-lg border border-border bg-card p-6">
      <div className="flex gap-2">
        {steps.map((s, i) => (
          <Text key={s} as="span" variant="bodySm" weight={i === step ? "medium" : undefined} color={i === step ? undefined : "muted"}>
            {i + 1}. {s}
          </Text>
        ))}
      </div>

      {label === "Basics" ? (
        <FieldRow label="Name" hint={`Becomes ${props.kind === "application" ? "app" : "sa"}-<org>-<name>`}>
          <Input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
        </FieldRow>
      ) : null}
      {label === "Redirects" ? (
        <>
          <FieldRow label="Redirect URIs (one per line)" hint="HTTPS only; localhost may use HTTP">
            <Textarea rows={3} value={redirects} onChange={(e) => setRedirects(e.target.value)} />
          </FieldRow>
          <FieldRow label="Post-logout redirect URIs (one per line)">
            <Textarea rows={2} value={postLogout} onChange={(e) => setPostLogout(e.target.value)} />
          </FieldRow>
          <FieldRow label="Back-channel logout URI (optional)">
            <Input
              value={draft.backchannelLogoutUri ?? ""}
              onChange={(e) => setDraft({ ...draft, backchannelLogoutUri: e.target.value || null })}
            />
          </FieldRow>
        </>
      ) : null}
      {label === "Scopes" ? (
        <ScopePicker value={draft.scopes} onChange={(scopes) => setDraft({ ...draft, scopes })} />
      ) : null}
      {label === "Branding" ? (
        <>
          <FieldRow label="Display name on the login screen">
            <Input value={brandName} onChange={(e) => setBrandName(e.target.value)} />
          </FieldRow>
          <FieldRow label="Tagline">
            <Input value={tagline} onChange={(e) => setTagline(e.target.value)} />
          </FieldRow>
        </>
      ) : null}

      <RequestPreview request={request} orgId={props.orgId} />

      <div className="flex gap-3">
        <Button type="button" variant="secondary" className="w-auto" onClick={props.onCancel}>
          Cancel
        </Button>
        {step > 0 ? (
          <Button type="button" variant="secondary" className="w-auto" onClick={() => setStep(step - 1)}>
            Back
          </Button>
        ) : null}
        {last ? (
          <Button type="button" className="w-auto" disabled={!request.name.trim() || request.scopes.length === 0} onClick={() => props.onSubmit(request)}>
            Register {props.kind === "application" ? "application" : "service account"}
          </Button>
        ) : (
          <Button type="button" className="w-auto" disabled={step === 0 && !request.name.trim()} onClick={() => setStep(step + 1)}>
            Next
          </Button>
        )}
      </div>
    </div>
  );
}

function Ledger(props: {
  kind: ClientKind;
  title: string;
  clients: ProtoClient[];
  orgId: string;
  reveal: SecretReveal | null;
  onRegister: (request: RegisterClientRequest) => void;
  onRotate: (client: ProtoClient) => void;
  onDismissReveal: () => void;
}) {
  const [open, setOpen] = useState(false);
  const showReveal = props.reveal !== null && props.reveal.client.kind === props.kind;
  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <Text as="h2" variant="subheading">
          {props.title}
        </Text>
        <Button type="button" className="w-auto" onClick={() => setOpen(true)} disabled={open}>
          Register {props.kind === "application" ? "application" : "service account"}
        </Button>
      </div>
      <ListCard name={`proto-a-${props.kind}`}>
        {props.clients.map((c) => (
          <ListRow key={c.id} name={`proto-a-${props.kind}-row`}>
            <div className="flex flex-1 items-center gap-3">
              <div className="flex-1">
                <Text as="span" variant="bodySm" weight="medium">
                  {c.name}
                </Text>
                <Text as="code" className="ml-3 text-xs">
                  {c.clientId}
                </Text>
              </div>
              <Text as="span" variant="bodySm" color="muted">
                {c.scopes.join(" ")}
              </Text>
              <Button type="button" variant="secondary" className="w-auto" onClick={() => props.onRotate(c)}>
                Rotate secret
              </Button>
            </div>
          </ListRow>
        ))}
      </ListCard>
      {showReveal && props.reveal ? (
        <SecretRevealCard reveal={props.reveal} onDone={props.onDismissReveal} />
      ) : null}
      {open && !showReveal ? (
        <Stepper
          kind={props.kind}
          orgId={props.orgId}
          onCancel={() => setOpen(false)}
          onSubmit={(request) => {
            props.onRegister(request);
            setOpen(false);
          }}
        />
      ) : null}
    </section>
  );
}

export function VariantA(props: VariantProps) {
  return (
    <div className="space-y-10">
      <Ledger
        {...props}
        kind="application"
        title="Applications"
        clients={props.clients.filter((c) => c.kind === "application")}
      />
      <Ledger
        {...props}
        kind="service-account"
        title="Service accounts"
        clients={props.clients.filter((c) => c.kind === "service-account")}
      />
      <Text as="p" variant="bodySm" color="muted">
        <KindBadge kind="application" /> logs users in · <KindBadge kind="service-account" /> calls the API as itself
      </Text>
    </div>
  );
}
