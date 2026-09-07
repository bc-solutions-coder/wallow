import type { OrganizationClientResponse } from "@bc-solutions-coder/sdk";
import { Badge, Button, Dialog, MutedText, useFailureMessage } from "@bc-solutions-coder/ui";
import { useClientObservability } from "../hooks/use-client-observability";
import { telemetryEnvBlock, telemetryStatusLabel } from "./telemetry-config";

export function ClientObservability({
  orgId,
  client,
}: {
  orgId: string;
  client: OrganizationClientResponse;
}) {
  const telemetry = useClientObservability(orgId, client.clientId);
  const failure = useFailureMessage(telemetry.error);
  const env =
    telemetry.configuration === undefined ? "" : telemetryEnvBlock(telemetry.configuration);
  return (
    <>
      {client.telemetry === undefined ||
      client.telemetry === null ||
      client.telemetry.status === "disabled" ? (
        <Button
          type="button"
          variant="secondary"
          className="w-auto"
          disabled={telemetry.pending}
          onClick={telemetry.handleEnable}
        >
          {telemetry.pending ? "Enabling observability…" : "Enable observability"}
        </Button>
      ) : (
        <Badge variant={client.telemetry.status === "active" ? "success" : "neutral"}>
          Observability: {telemetryStatusLabel(client.telemetry.status)}
        </Badge>
      )}
      {client.telemetry && client.telemetry.status !== "disabled" ? (
        <LifecycleControls
          status={client.telemetry.status}
          pending={telemetry.pending}
          onRotate={telemetry.handleRotate}
          onRevoke={telemetry.handleRevoke}
          onDisable={telemetry.handleDisable}
        />
      ) : null}
      {client.telemetry?.previousCredentialExpiresAt ? (
        <MutedText>
          Previous credential expires{" "}
          {new Date(client.telemetry.previousCredentialExpiresAt).toLocaleString()}.
        </MutedText>
      ) : null}
      {client.telemetry?.failure ? <MutedText>{client.telemetry.failure}</MutedText> : null}
      {failure ? <MutedText>{failure}</MutedText> : null}
      <ConfigurationDialog
        env={env}
        open={telemetry.configuration !== undefined}
        onClose={telemetry.handleClose}
      />
    </>
  );
}

function LifecycleControls({
  status,
  pending,
  onRotate,
  onRevoke,
  onDisable,
}: {
  status: string;
  pending: boolean;
  onRotate: () => void;
  onRevoke: () => void;
  onDisable: () => void;
}) {
  const revoking = status === "pending-revocation";
  return (
    <div className="flex flex-wrap gap-2">
      <Button
        type="button"
        variant="secondary"
        disabled={pending || status !== "active"}
        onClick={onRotate}
      >
        Rotate telemetry credential
      </Button>
      <Button type="button" variant="secondary" disabled={pending || revoking} onClick={onRevoke}>
        Revoke telemetry credential
      </Button>
      <Button type="button" variant="secondary" disabled={pending || revoking} onClick={onDisable}>
        Disable observability
      </Button>
    </div>
  );
}

function ConfigurationDialog({
  env,
  open,
  onClose,
}: {
  env: string;
  open: boolean;
  onClose: () => void;
}) {
  return (
    <Dialog.Root
      open={open}
      onOpenChange={(next) => {
        if (!next) {
          onClose();
        }
      }}
    >
      <Dialog.Portal>
        <Dialog.Backdrop />
        <ConfigurationPopup env={env} onClose={onClose} />
      </Dialog.Portal>
    </Dialog.Root>
  );
}

function ConfigurationPopup({ env, onClose }: { env: string; onClose: () => void }) {
  return (
    <Dialog.Popup>
      <Dialog.Title>Observability server configuration</Dialog.Title>
      <Dialog.Description>
        Copy this server configuration now. The credential is shown once. Provisioning continues
        automatically.
      </Dialog.Description>
      <pre className="overflow-x-auto rounded-md border border-border bg-background p-4 font-mono text-sm text-foreground">
        {env}
      </pre>
      <Button
        type="button"
        onClick={() => {
          void navigator.clipboard.writeText(env);
        }}
      >
        Copy server configuration
      </Button>
      <Button type="button" variant="secondary" onClick={onClose}>
        Done
      </Button>
    </Dialog.Popup>
  );
}
