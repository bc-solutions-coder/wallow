/**
 * The branding editor an application row opens: display name, tagline, logo
 * upload, and the curated per-mode theme colours, beside a live preview of the
 * sign-in header they produce. Renders `organization-detail-branding-*`; Save
 * PUTs the whole branding in one multipart request, "Remove logo" deletes just
 * the logo, and "Preview sign-in" links to the real sign-in screen with this
 * client's `client_id`. The fork's own app name is refused as a display name
 * before the request is ever made, mirroring the server's reservation.
 */
import { errorText } from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import type { ClientBrandingDto, OrganizationClientResponse } from "@bc-solutions-coder/sdk";
import { Button, Card, CardHeader, Input, MutedText, Text } from "@bc-solutions-coder/ui";
import {
  type ClientBranding,
  forkBranding,
  mergeClientBranding,
  type ResolvedBranding,
} from "@bc-solutions-coder/styles";
import type { CSSProperties, ReactElement } from "react";
import { useEffect, useMemo, useState } from "react";
import { useRouteContext } from "@tanstack/react-router";

import { authUrl } from "@shared/lib/auth-url";
import {
  organizationClientBrandingDeleteLogoMutation,
  organizationClientBrandingGetBrandingOptions,
  organizationClientBrandingGetBrandingQueryKey,
  organizationClientBrandingUpsertBrandingMutation,
  queriesForOperation,
} from "../api";
import { isReservedDisplayName, RESERVED_DISPLAY_NAME_MESSAGE } from "./RegisterClient";

/** Every element in the editor hangs its test id off this prefix. */
const TEST_ID = "organization-detail-branding";

/** The curated theme fields, flattened for form state: one colour per input. */
interface ThemeDraft {
  readonly lightPrimary: string;
  readonly lightPrimaryForeground: string;
  readonly darkPrimary: string;
  readonly darkPrimaryForeground: string;
}

/** One colour out of a parsed `ThemeJson`, or `""` when absent or malformed. */
function modeColor(parsed: unknown, mode: string, key: string): string {
  if (typeof parsed !== "object" || parsed === null) {
    return "";
  }
  const modeValue: unknown = (parsed as Record<string, unknown>)[mode];
  if (typeof modeValue !== "object" || modeValue === null) {
    return "";
  }
  const value: unknown = (modeValue as Record<string, unknown>)[key];
  return typeof value === "string" ? value : "";
}

/** The saved `ThemeJson` split into the four inputs; all blank when there is none. */
function parseThemeDraft(themeJson: string | null): ThemeDraft {
  let parsed: unknown = null;
  if (themeJson !== null) {
    try {
      parsed = JSON.parse(themeJson);
    } catch {
      parsed = null;
    }
  }
  return {
    lightPrimary: modeColor(parsed, "light", "primary"),
    lightPrimaryForeground: modeColor(parsed, "light", "primaryForeground"),
    darkPrimary: modeColor(parsed, "dark", "primary"),
    darkPrimaryForeground: modeColor(parsed, "dark", "primaryForeground"),
  };
}

/** One mode's non-blank colours, or `undefined` when the mode sets neither. */
function modeEntries(primary: string, foreground: string): Record<string, string> | undefined {
  const entries: Record<string, string> = {};
  if (primary.trim() !== "") {
    entries.primary = primary.trim();
  }
  if (foreground.trim() !== "") {
    entries.primaryForeground = foreground.trim();
  }
  return Object.keys(entries).length === 0 ? undefined : entries;
}

/** The draft serialised back to `ThemeJson`, or `undefined` when every field is blank. */
function toThemeJson(theme: ThemeDraft): string | undefined {
  const light = modeEntries(theme.lightPrimary, theme.lightPrimaryForeground);
  const dark = modeEntries(theme.darkPrimary, theme.darkPrimaryForeground);
  if (light === undefined && dark === undefined) {
    return undefined;
  }
  return JSON.stringify({
    ...(light === undefined ? {} : { light }),
    ...(dark === undefined ? {} : { dark }),
  });
}

/** A labelled text input, the plain-state sibling of the stepper's form fields. */
function LabeledInput(props: {
  label: string;
  value: string;
  onValueChange: (value: string) => void;
  placeholder?: string;
  testId: string;
}): ReactElement {
  const { label, value, onValueChange, placeholder, testId } = props;
  return (
    <label className="flex flex-col gap-1">
      <Text as="span" variant="bodySm" color="onCard" weight="medium">
        {label}
      </Text>
      <Input
        value={value}
        autoComplete="off"
        placeholder={placeholder}
        onChange={(event) => {
          onValueChange(event.target.value);
        }}
        data-testid={testId}
      />
    </label>
  );
}

/** The four curated colours, one input per mode-and-role pair. */
function ThemeFields(props: {
  theme: ThemeDraft;
  onThemeChange: (theme: ThemeDraft) => void;
}): ReactElement {
  const { theme, onThemeChange } = props;
  const set = (patch: Partial<ThemeDraft>): void => {
    onThemeChange({ ...theme, ...patch });
  };
  return (
    <div className="grid grid-cols-2 gap-3">
      <LabeledInput
        label="Light primary"
        value={theme.lightPrimary}
        onValueChange={(value) => {
          set({ lightPrimary: value });
        }}
        placeholder="oklch(0.55 0.2 260)"
        testId={`${TEST_ID}-light-primary`}
      />
      <LabeledInput
        label="Light primary foreground"
        value={theme.lightPrimaryForeground}
        onValueChange={(value) => {
          set({ lightPrimaryForeground: value });
        }}
        placeholder="#ffffff"
        testId={`${TEST_ID}-light-primary-foreground`}
      />
      <LabeledInput
        label="Dark primary"
        value={theme.darkPrimary}
        onValueChange={(value) => {
          set({ darkPrimary: value });
        }}
        placeholder="oklch(0.7 0.2 260)"
        testId={`${TEST_ID}-dark-primary`}
      />
      <LabeledInput
        label="Dark primary foreground"
        value={theme.darkPrimaryForeground}
        onValueChange={(value) => {
          set({ darkPrimaryForeground: value });
        }}
        placeholder="#111111"
        testId={`${TEST_ID}-dark-primary-foreground`}
      />
    </div>
  );
}

/** The logo picker, plus removal of the logo already saved when there is one. */
function LogoField(props: {
  hasSavedLogo: boolean;
  removing: boolean;
  onFileChange: (file: File | null) => void;
  onRemove: () => void;
}): ReactElement {
  const { hasSavedLogo, removing, onFileChange, onRemove } = props;
  return (
    <div className="flex flex-wrap items-end gap-3">
      <label className="flex min-w-64 flex-1 flex-col gap-1">
        <Text as="span" variant="bodySm" color="onCard" weight="medium">
          Logo (PNG, JPEG or WebP, up to 2 MB)
        </Text>
        <Input
          type="file"
          accept="image/png,image/jpeg,image/webp"
          onChange={(event) => {
            onFileChange(event.target.files?.[0] ?? null);
          }}
          data-testid={`${TEST_ID}-logo`}
        />
      </label>
      {hasSavedLogo ? (
        <Button
          type="button"
          variant="secondary"
          className="w-auto"
          disabled={removing}
          onClick={onRemove}
          data-testid={`${TEST_ID}-logo-remove`}
        >
          {removing ? "Removing…" : "Remove logo"}
        </Button>
      ) : null}
    </div>
  );
}

/**
 * The sign-in header the draft produces, rendered live. The wrapper carries the
 * draft's own custom properties inline, so the sample button's
 * `bg-primary text-primary-foreground` resolve to the DRAFT's colours rather
 * than the ledger page's — same variables, narrower scope.
 */
function BrandingPreview(props: { resolved: ResolvedBranding }): ReactElement {
  const { resolved } = props;
  const style: CSSProperties = resolved.cssVars[resolved.defaultMode] as CSSProperties;
  return (
    <div
      style={style}
      className="rounded-lg border border-border bg-background p-6 text-center"
      data-testid={`${TEST_ID}-preview`}
    >
      {resolved.logoUrl === null ? null : (
        <img
          src={resolved.logoUrl}
          alt=""
          className="mx-auto mb-3 block size-16 object-contain"
          data-testid={`${TEST_ID}-preview-logo`}
        />
      )}
      <Text
        as="span"
        variant="heading"
        weight="bold"
        className="block"
        data-testid={`${TEST_ID}-preview-name`}
      >
        {resolved.name}
      </Text>
      {resolved.tagline === null ? null : (
        <MutedText className="mt-1" data-testid={`${TEST_ID}-preview-tagline`}>
          {resolved.tagline}
        </MutedText>
      )}
      <Button type="button" className="mt-4 w-auto">
        Sign in
      </Button>
    </div>
  );
}

/** The footer: the real sign-in screen link, Close, and Save. */
function EditorActions(props: {
  previewHref: string;
  saving: boolean;
  saveDisabled: boolean;
  onSave: () => void;
  onDone: () => void;
}): ReactElement {
  const { previewHref, saving, saveDisabled, onSave, onDone } = props;
  return (
    <div className="mt-6 flex flex-wrap items-center justify-end gap-2">
      <Button
        render={<a href={previewHref} target="_blank" rel="noreferrer" />}
        nativeButton={false}
        variant="secondary"
        className="w-auto no-underline"
        data-testid={`${TEST_ID}-preview-signin`}
      >
        Preview sign-in
      </Button>
      <Button
        type="button"
        variant="secondary"
        className="w-auto"
        disabled={saving}
        onClick={onDone}
        data-testid={`${TEST_ID}-close`}
      >
        Close
      </Button>
      <Button
        type="button"
        className="w-auto"
        disabled={saveDisabled || saving}
        onClick={onSave}
        data-testid={`${TEST_ID}-save`}
      >
        {saving ? "Saving…" : "Save branding"}
      </Button>
    </div>
  );
}

/** The left column: the identity fields, theme colours, and logo picker. */
function BrandingFields(props: {
  displayName: string;
  onDisplayNameChange: (value: string) => void;
  tagline: string;
  onTaglineChange: (value: string) => void;
  theme: ThemeDraft;
  onThemeChange: (theme: ThemeDraft) => void;
  hasSavedLogo: boolean;
  removingLogo: boolean;
  onLogoFileChange: (file: File | null) => void;
  onLogoRemove: () => void;
}): ReactElement {
  return (
    <div className="flex flex-col gap-4">
      <LabeledInput
        label="Display name"
        value={props.displayName}
        onValueChange={props.onDisplayNameChange}
        testId={`${TEST_ID}-display-name`}
      />
      <LabeledInput
        label="Tagline"
        value={props.tagline}
        onValueChange={props.onTaglineChange}
        placeholder="Shown under the name; leave blank for none."
        testId={`${TEST_ID}-tagline`}
      />
      <ThemeFields theme={props.theme} onThemeChange={props.onThemeChange} />
      <LogoField
        hasSavedLogo={props.hasSavedLogo}
        removing={props.removingLogo}
        onFileChange={props.onLogoFileChange}
        onRemove={props.onLogoRemove}
      />
    </div>
  );
}

/** The editor proper; mounts only once the saved branding (or its absence) is known. */
function BrandingForm(props: {
  orgId: string;
  client: OrganizationClientResponse;
  saved: ClientBrandingDto | null;
  onDone: () => void;
}): ReactElement {
  const { orgId, client, saved, onDone } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const [displayName, setDisplayName] = useState(saved?.displayName ?? client.name);
  const [tagline, setTagline] = useState(saved?.tagline ?? "");
  const [theme, setTheme] = useState<ThemeDraft>(() => parseThemeDraft(saved?.themeJson ?? null));
  const [logoFile, setLogoFile] = useState<File | null>(null);

  // The picked file previews through an object URL, revoked when replaced.
  const pendingLogoUrl = useMemo(
    () => (logoFile === null ? null : URL.createObjectURL(logoFile)),
    [logoFile],
  );
  useEffect(
    () => (): void => {
      if (pendingLogoUrl !== null) {
        URL.revokeObjectURL(pendingLogoUrl);
      }
    },
    [pendingLogoUrl],
  );

  const invalidateBranding = (): void => {
    void queryClient.invalidateQueries(
      queriesForOperation(
        organizationClientBrandingGetBrandingQueryKey({
          client: sdk.client,
          path: { orgId, clientId: client.clientId },
        }),
      ),
    );
  };
  const save = useMutation({
    ...organizationClientBrandingUpsertBrandingMutation({ client: sdk.client }),
    onSuccess: (): void => {
      invalidateBranding();
      onDone();
    },
  });
  const removeLogo = useMutation({
    ...organizationClientBrandingDeleteLogoMutation({ client: sdk.client }),
    onSuccess: invalidateBranding,
  });

  const trimmedName = displayName.trim();
  const reserved = isReservedDisplayName(displayName);
  const themeJson = toThemeJson(theme);
  const draft: ClientBranding = {
    clientId: client.clientId,
    displayName: trimmedName === "" ? client.name : trimmedName,
    tagline: tagline.trim() === "" ? null : tagline.trim(),
    logoUrl: pendingLogoUrl ?? saved?.logoUrl ?? null,
    themeJson: themeJson ?? null,
  };
  const resolved = mergeClientBranding(forkBranding, draft);
  const onSave = (): void => {
    const trimmedTagline = tagline.trim();
    save.mutate({
      path: { orgId, clientId: client.clientId },
      body: {
        DisplayName: trimmedName,
        ...(trimmedTagline === "" ? {} : { Tagline: trimmedTagline }),
        ...(themeJson === undefined ? {} : { ThemeJson: themeJson }),
        ...(logoFile === null ? {} : { logo: logoFile }),
      },
    });
  };
  const errors = [
    reserved ? RESERVED_DISPLAY_NAME_MESSAGE : null,
    save.isError ? errorText(save.error, "Could not save the branding.") : null,
    removeLogo.isError ? errorText(removeLogo.error, "Could not remove the logo.") : null,
  ].filter((message): message is string => message !== null);

  return (
    <Card data-testid={`${TEST_ID}-card`}>
      <CardHeader
        title={`Branding for ${client.name}`}
        titleTestId={`${TEST_ID}-heading`}
        description="What end users see on the sign-in screen for this application."
      />
      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <BrandingFields
          displayName={displayName}
          onDisplayNameChange={setDisplayName}
          tagline={tagline}
          onTaglineChange={setTagline}
          theme={theme}
          onThemeChange={setTheme}
          hasSavedLogo={typeof saved?.logoUrl === "string"}
          removingLogo={removeLogo.isPending}
          onLogoFileChange={setLogoFile}
          onLogoRemove={() => {
            removeLogo.mutate({ path: { orgId, clientId: client.clientId } });
          }}
        />
        <BrandingPreview resolved={resolved} />
      </div>
      {errors.map((message) => (
        <MutedText key={message} className="mt-4 text-destructive" data-testid={`${TEST_ID}-error`}>
          {message}
        </MutedText>
      ))}
      <EditorActions
        previewHref={`${authUrl()}/login?client_id=${encodeURIComponent(client.clientId)}`}
        saving={save.isPending}
        saveDisabled={trimmedName === "" || reserved}
        onSave={onSave}
        onDone={onDone}
      />
    </Card>
  );
}

/**
 * The branding editor for one application. Reads the saved branding first so
 * the form opens on what is already there; a failed read (the row is created at
 * registration, so normally only an outage) degrades to editing from the
 * application's own name rather than blocking the screen.
 */
export function ClientBrandingEditor(props: {
  orgId: string;
  client: OrganizationClientResponse;
  onDone: () => void;
}): ReactElement {
  const { orgId, client, onDone } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const branding = useQuery(
    organizationClientBrandingGetBrandingOptions({
      client: sdk.client,
      path: { orgId, clientId: client.clientId },
    }),
  );
  if (branding.isPending) {
    return (
      <Card data-testid={`${TEST_ID}-card`}>
        <MutedText data-testid={`${TEST_ID}-loading`}>Loading branding…</MutedText>
      </Card>
    );
  }
  return (
    <BrandingForm orgId={orgId} client={client} saved={branding.data ?? null} onDone={onDone} />
  );
}
