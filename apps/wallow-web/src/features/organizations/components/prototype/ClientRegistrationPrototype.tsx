/**
 * PROTOTYPE — throwaway (map #112, ticket #122).
 *
 * Three variants of the org-owner client registration journey, switchable via
 * `?variant=A|B|C` on the existing `/dashboard/organizations/$orgId` route.
 * State is in memory; nothing here calls the API.
 */
import { useState } from "react";

import { PrototypeSwitcher } from "@shared/components/PrototypeSwitcher";

import {
  seedClients,
  stubRegister,
  stubRotate,
  type ProtoClient,
  type RegisterClientRequest,
  type SecretReveal,
} from "./model";
import { VARIANT_A_NAME, VariantA } from "./VariantA";
import { VARIANT_B_NAME, VariantB } from "./VariantB";
import { VARIANT_C_NAME, VariantC } from "./VariantC";

export const PROTOTYPE_VARIANTS = [
  { key: "A", name: VARIANT_A_NAME },
  { key: "B", name: VARIANT_B_NAME },
  { key: "C", name: VARIANT_C_NAME },
] as const;

export function ClientRegistrationPrototype(props: { orgId: string; orgName: string; variant: string }) {
  const orgSlug = props.orgName.toLowerCase().replaceAll(/[^a-z0-9]+/g, "-").replaceAll(/^-|-$/g, "") || "org";
  const [clients, setClients] = useState<ProtoClient[]>(() => seedClients(orgSlug));
  const [reveal, setReveal] = useState<SecretReveal | null>(null);

  const onRegister = (request: RegisterClientRequest): void => {
    const result = stubRegister(orgSlug, request);
    setClients((prev) => [...prev, result.client]);
    setReveal(result);
  };
  const onRotate = (client: ProtoClient): void => {
    const result = stubRotate(client);
    setClients((prev) => prev.map((c) => (c.id === client.id ? result.client : c)));
    setReveal(result);
  };
  const shared = {
    orgId: props.orgId,
    clients,
    reveal,
    onRegister,
    onRotate,
    onDismissReveal: () => setReveal(null),
  };

  return (
    <>
      {props.variant === "B" ? <VariantB {...shared} /> : props.variant === "C" ? <VariantC {...shared} /> : <VariantA {...shared} />}
      <PrototypeSwitcher variants={PROTOTYPE_VARIANTS} current={props.variant} />
    </>
  );
}
