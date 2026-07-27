import type { ReactElement } from "react";

export interface ForkAttributionProps {
  /** The fork's display name, emphasised inside the "A {appName} App" sentence. */
  readonly appName: string;
  /** The fork's icon URL (root-relative), rendered at `size-8`. */
  readonly iconUrl: string;
  /**
   * The fork's repository URL. When present the attribution renders as an
   * external link; when absent (or empty) it renders as a plain inline group.
   * Branding is accepted as props so packages/ui never imports app-local
   * `../lib/branding` (nor reaches into @bc-solutions-coder/styles itself).
   */
  readonly repositoryUrl?: string;
  /** App-owned test id, passed through onto the outer element. */
  readonly "data-testid"?: string;
}

/** The fork's icon, shown at the size the attribution footer uses. */
function ForkIcon({ appName, iconUrl }: { readonly appName: string; readonly iconUrl: string }) {
  return <img src={iconUrl} alt={appName} className="size-8" />;
}

/** "A {fork} App" — the fork's name emphasised within the sentence. */
function ForkAttributionText({ appName }: { readonly appName: string }) {
  return (
    <span className="text-xs text-muted-foreground">
      A <span className="font-semibold text-muted-foreground">{appName}</span> App
    </span>
  );
}

/**
 * The "A {fork} App" attribution generalized from wallow-auth's `auth-layout.tsx`
 * (`ForkAttribution`/`ForkIcon`/`ForkAttributionText`). With a repository URL it
 * renders an external link; without one (or with an empty string) it renders a
 * plain inline group.
 */
export function ForkAttribution({
  appName,
  iconUrl,
  repositoryUrl,
  "data-testid": testId,
}: ForkAttributionProps): ReactElement {
  const icon = <ForkIcon appName={appName} iconUrl={iconUrl} />;
  const text = <ForkAttributionText appName={appName} />;

  if (repositoryUrl === undefined || repositoryUrl === "") {
    return (
      <div className="flex items-center justify-center gap-1.5" data-testid={testId}>
        {icon}
        {text}
      </div>
    );
  }

  return (
    <a
      href={repositoryUrl}
      target="_blank"
      rel="noopener noreferrer"
      className="flex items-center justify-center gap-1.5 hover:opacity-80 transition-opacity"
      data-testid={testId}
    >
      {icon}
      {text}
    </a>
  );
}
