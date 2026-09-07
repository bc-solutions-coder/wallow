import { handledFailure, useMutation, useQueryClient } from "@bc-solutions-coder/query";
import type { TelemetryConfigurationDto, TelemetryEnableResult } from "@bc-solutions-coder/sdk";
import { useRouteContext } from "@tanstack/react-router";
import { useState } from "react";
import {
  organizationClientsEnableObservabilityMutation,
  organizationClientsRotateObservabilityMutation,
  organizationClientsRevokeObservabilityMutation,
  organizationClientsDisableObservabilityMutation,
  organizationClientsListQueryKey,
  queriesForOperation,
} from "../api";

export function useClientObservability(orgId: string, clientId: string) {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const [configuration, setConfiguration] = useState<TelemetryConfigurationDto>();
  function refresh() {
    void queryClient.invalidateQueries(
      queriesForOperation(organizationClientsListQueryKey({ client: sdk.client, path: { orgId } })),
    );
  }
  function reveal(result: TelemetryEnableResult) {
    setConfiguration(result.configuration);
    refresh();
  }
  const mutation = useMutation({
    ...organizationClientsEnableObservabilityMutation({ client: sdk.client }),
    meta: handledFailure(),
    onSuccess: reveal,
  });
  const rotate = useMutation({
    ...organizationClientsRotateObservabilityMutation({ client: sdk.client }),
    meta: handledFailure(),
    onSuccess: reveal,
  });
  const revoke = useMutation({
    ...organizationClientsRevokeObservabilityMutation({ client: sdk.client }),
    meta: handledFailure(),
    onSuccess: refresh,
  });
  const disable = useMutation({
    ...organizationClientsDisableObservabilityMutation({ client: sdk.client }),
    meta: handledFailure(),
    onSuccess: refresh,
  });
  return {
    configuration,
    pending: mutation.isPending || rotate.isPending || revoke.isPending || disable.isPending,
    error: mutation.error ?? rotate.error ?? revoke.error ?? disable.error,
    handleEnable: () => {
      mutation.mutate({ path: { orgId, clientId } });
    },
    handleRotate: () => {
      rotate.mutate({ path: { orgId, clientId } });
    },
    handleRevoke: () => {
      revoke.mutate({ path: { orgId, clientId } });
    },
    handleDisable: () => {
      disable.mutate({ path: { orgId, clientId } });
    },
    handleClose: () => {
      setConfiguration(undefined);
      mutation.reset();
      rotate.reset();
    },
  };
}
