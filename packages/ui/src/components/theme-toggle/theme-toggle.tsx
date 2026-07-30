import type { ReactElement } from "react";

import { Button, type ButtonProps } from "../button";
import { useTheme, type ThemeMode, type ThemePreference } from "../theme-provider";
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
  onPreferenceChange,
  className,
  ...rest
}: ThemeToggleProps): ReactElement {
  const theme = useTheme();
  const active: ThemePreference = preference ?? theme.preference;
  const activeMode: ThemeMode = mode ?? theme.mode;
  const next: ThemePreference = nextPreference(active);

  // The accessible name announces the DESTINATION, not the current state: the
  // visible label already says where the control is, and with three states
  // "Theme: system" leaves a screen-reader user guessing what a press will do.
  return (
    <Button
      variant="secondary"
      data-theme-preference={active}
      data-theme-mode={activeMode}
      aria-label={`Switch to ${next} theme`}
      className={cn(themeToggleRecipe(), className)}
      onClick={() => {
        if (onPreferenceChange === undefined) {
          theme.setPreference(next);
        } else {
          onPreferenceChange(next);
        }
      }}
      {...rest}
    >
      {THEME_PREFERENCE_LABELS[active]}
    </Button>
  );
}
