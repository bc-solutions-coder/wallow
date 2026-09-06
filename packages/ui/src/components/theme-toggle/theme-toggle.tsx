import type { ReactElement, ReactNode } from "react";

import { Button, type ButtonProps } from "../button";
import { useTheme, type ThemeMode, type ThemePreference } from "../theme-provider";
import { Tooltip } from "../tooltip";
import { cn } from "../../core/cn";
import { themeToggleRecipe, type ThemeToggleRecipeProps } from "./theme-toggle.styles";

/**
 * The control that changes the fork's theme (Wallow-lrlm.1.2).
 *
 * THREE STATES, NOT TWO — `light -> dark -> system -> light`. A two-state
 * `aria-pressed` toggle can express "I want dark" but has no way back to
 * "follow the OS": once pressed, the choice is pinned forever. Since the
 * persisted value is a {@link ThemePreference} and `"system"` is its default,
 * the control has to be able to return there, so it cycles rather than toggles
 * and carries NO `aria-pressed` (which is a two-state attribute and would
 * misreport the third). The current state is announced through the accessible
 * name and exposed to tests as `data-theme-preference`.
 *
 * It composes `../button` — a deliberate catalog reuse, so the toggle inherits
 * the button recipe's box, focus ring and disabled treatment rather than
 * forking them.
 */

/** The cycle order a press walks, pinned so the spec and the component agree. */
export const THEME_PREFERENCE_CYCLE = ["light", "dark", "system"] as const;

/** The visible label for each state — what the control currently IS. */
const THEME_PREFERENCE_LABELS: Readonly<Record<ThemePreference, string>> = {
  light: "Light",
  dark: "Dark",
  system: "System",
};

const themeIcons: Readonly<Record<ThemePreference, ReactNode>> = {
  light: (
    <>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2m0 16v2M2 12h2m16 0h2M4.93 4.93l1.42 1.42m11.3 11.3 1.42 1.42M4.93 19.07l1.42-1.42m11.3-11.3 1.42-1.42" />
    </>
  ),
  dark: <path d="M20.9 13A9 9 0 0 1 11 3.1 9 9 0 1 0 20.9 13Z" />,
  system: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 3a9 9 0 0 1 0 18Z" fill="currentColor" stroke="none" />
    </>
  ),
};

function ThemeIcon({ preference }: { preference: ThemePreference }): ReactElement {
  return (
    <svg
      aria-hidden="true"
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {themeIcons[preference]}
    </svg>
  );
}

function ThemeTooltip({ preference }: { preference: ThemePreference }): ReactElement {
  return (
    <Tooltip.Portal>
      <Tooltip.Positioner side="right" sideOffset={8}>
        <Tooltip.Popup role="tooltip">{THEME_PREFERENCE_LABELS[preference]} theme</Tooltip.Popup>
      </Tooltip.Positioner>
    </Tooltip.Portal>
  );
}

/** The state one press moves to, wrapping `system` back round to `light`. */
function nextPreference(current: ThemePreference): ThemePreference {
  const index: number = THEME_PREFERENCE_CYCLE.indexOf(current);
  return THEME_PREFERENCE_CYCLE[(index + 1) % THEME_PREFERENCE_CYCLE.length] ?? "light";
}

export interface ThemeToggleProps
  extends
    Omit<ButtonProps, "className" | "children" | "onClick" | "variant">,
    ThemeToggleRecipeProps {
  /**
   * `className` is narrowed back to `string`, as everywhere in this catalog:
   * Base UI widens it to a state callback, which `cn()` cannot merge.
   */
  readonly className?: string;
  /**
   * The preference to display. Omitted, the toggle reads the nearest
   * {@link ThemeProvider} — which is how both apps use it. Supplying it makes
   * the control fully controlled, which is what lets a story render the
   * `light` / `dark` / `system` faces without touching the real document.
   */
  readonly preference?: ThemePreference;
  /**
   * The resolved mode behind that preference (what `system` currently means).
   * Omitted, it comes from the provider alongside `preference`.
   */
  readonly mode?: ThemeMode;
  /**
   * Called with the NEXT preference in the cycle. Omitted, the press goes to the
   * provider's `setPreference`.
   */
  readonly onPreferenceChange?: (preference: ThemePreference) => void;
}

/**
 * A theme control that cycles `light -> dark -> system`. Uncontrolled it reads
 * and writes the nearest {@link ThemeProvider}; controlled (both `preference`
 * and `onPreferenceChange` supplied) it renders exactly the face it is given.
 */
export function ThemeToggle({
  preference,
  mode,
  presentation = "text",
  onPreferenceChange,
  className,
  ...rest
}: ThemeToggleProps): ReactElement {
  const theme = useTheme();
  const active: ThemePreference = preference ?? theme.preference;
  const activeMode: ThemeMode = mode ?? theme.mode;
  const next: ThemePreference = nextPreference(active);

  const button = (
    <Button
      variant="secondary"
      data-theme-preference={active}
      data-theme-mode={activeMode}
      aria-label={
        presentation === "icon"
          ? `${THEME_PREFERENCE_LABELS[active]} theme. Switch to ${next} theme`
          : `Switch to ${next} theme`
      }
      className={cn(themeToggleRecipe({ presentation }), className)}
      onClick={() => {
        if (onPreferenceChange === undefined) {
          theme.setPreference(next);
        } else {
          onPreferenceChange(next);
        }
      }}
      {...rest}
    >
      {presentation === "icon" ? (
        <ThemeIcon preference={active} />
      ) : (
        THEME_PREFERENCE_LABELS[active]
      )}
    </Button>
  );
  if (presentation !== "icon") {
    return button;
  }
  return (
    <Tooltip.Provider>
      <Tooltip.Root>
        <Tooltip.Trigger render={button} />
        <ThemeTooltip preference={active} />
      </Tooltip.Root>
    </Tooltip.Provider>
  );
}
