import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useSyncExternalStore,
  type ReactElement,
  type ReactNode,
} from "react";

/**
 * Theme activation for the fork (Wallow-lrlm.1.2).
 *
 * `packages/styles` already emits `.light` / `.dark` custom-property blocks from
 * `api/branding.json`; nothing in either app ever put those classes on the
 * document except the hardcoded `className={branding.defaultMode}` in each
 * `__root.tsx`. This folder is what makes the emitted CSS reachable: a pre-paint
 * inline script that stamps the class BEFORE React runs, plus a provider that
 * reads what the script stamped and keeps it in sync afterwards.
 *
 * Nothing here imports `@bc-solutions-coder/styles` — packages/ui takes branding
 * as props (the same rule `document-styles.tsx` and `fork-attribution.tsx`
 * follow), so `defaultMode` arrives from the app shell.
 */

/** A resolved colour scheme — the class that lands on `<html>`. */
export type ThemeMode = "light" | "dark";

/**
 * What the visitor asked for. `"system"` means "follow the OS", which is the
 * default and the state a three-way toggle can always return to — the reason the
 * persisted value is a PREFERENCE and not a mode.
 */
export type ThemePreference = ThemeMode | "system";

/** The `localStorage` key holding the visitor's {@link ThemePreference}. */
export const THEME_STORAGE_KEY = "wallow-theme";

/** The two classes this module owns on `document.documentElement`. */
const THEME_CLASSES: readonly ThemeMode[] = ["light", "dark"];

/** The three inputs the active theme is resolved from. */
export interface ThemeResolutionInput {
  /**
   * The raw `localStorage` value — anything at all, including `null` and junk
   * left by another app on the same origin, which resolves as "no preference".
   */
  readonly stored: string | null;
  /**
   * What the OS asks for, or `null` when it states no preference at all
   * (neither `prefers-color-scheme: dark` nor `: light` matches). `null` is what
   * gives the fork's own `defaultMode` something to decide.
   */
  readonly systemMode: ThemeMode | null;
  /** The fork's `theme.defaultMode` from `api/branding.json`, the last resort. */
  readonly defaultMode: ThemeMode;
}

/**
 * The resolution order, lowest priority first: the fork's default, then the OS,
 * then the visitor's persisted choice. Pure, so the full precedence table is
 * provable without emulating a media query.
 *
 * Anything that is not literally `"light"` or `"dark"` — `null`, `"system"`, or
 * junk another app on this origin left behind — hands the decision one level
 * down rather than pinning a mode.
 */
export function resolveThemeMode(input: ThemeResolutionInput): ThemeMode {
  if (input.stored === "light" || input.stored === "dark") {
    return input.stored;
  }
  return input.systemMode ?? input.defaultMode;
}

/**
 * The source of the blocking inline script each app's `<head>` runs before first
 * paint. It resolves the same three inputs {@link resolveThemeMode} does — read
 * synchronously from `localStorage` and `matchMedia` — and stamps the class on
 * `document.documentElement`, so the correct palette is in place before React
 * hydrates and there is no flash of the wrong theme.
 *
 * The returned source must contain no `<`: React does not escape a text child of
 * `<script>` (that is what lets {@link ThemeScript} avoid
 * `dangerouslySetInnerHTML`, exactly as `DocumentStyles` does for `<style>`), so
 * a stray `</script` in the source would end the element early.
 */
export function themeInitScript(defaultMode: ThemeMode): string {
  return `(function () {
  var stored = null;
  try {
    stored = window.localStorage.getItem(${JSON.stringify(THEME_STORAGE_KEY)});
  } catch (error) {
    stored = null;
  }
  var mode = stored === "light" || stored === "dark" ? stored : null;
  if (mode === null) {
    if (window.matchMedia("(prefers-color-scheme: dark)").matches) {
      mode = "dark";
    } else if (window.matchMedia("(prefers-color-scheme: light)").matches) {
      mode = "light";
    } else {
      mode = ${JSON.stringify(defaultMode)};
    }
  }
  var root = document.documentElement;
  root.classList.remove("light", "dark");
  root.classList.add(mode);
})();`;
}

/** What the OS asks for right now, or `null` when it states no preference. */
function currentSystemMode(): ThemeMode | null {
  if (globalThis.matchMedia("(prefers-color-scheme: dark)").matches) {
    return "dark";
  }
  if (globalThis.matchMedia("(prefers-color-scheme: light)").matches) {
    return "light";
  }
  return null;
}

/**
 * The persisted choice, normalised. `localStorage` throws outright under a
 * blocked-cookies policy and in some private-browsing modes, and it is shared
 * per ORIGIN so this key can hold anything; both degrade to `"system"`.
 */
function readStoredPreference(): ThemePreference {
  try {
    const raw: string | null = globalThis.localStorage.getItem(THEME_STORAGE_KEY);
    return raw === "light" || raw === "dark" || raw === "system" ? raw : "system";
  } catch {
    return "system";
  }
}

/** Persists a choice, silently accepting a storage that refuses to be written. */
function writeStoredPreference(preference: ThemePreference): void {
  try {
    globalThis.localStorage.setItem(THEME_STORAGE_KEY, preference);
  } catch {
    // A blocked storage costs the visitor persistence across reloads, not the
    // theme they just picked — the class below still lands.
  }
}

/**
 * The mode currently ON the document, which is what the pre-paint script
 * decided. Reading the DOM rather than re-resolving is the point: the provider
 * must publish what is already painted, never a second opinion.
 */
function readStampedMode(fallback: ThemeMode): ThemeMode {
  const classes: DOMTokenList = document.documentElement.classList;
  if (classes.contains("dark")) {
    return "dark";
  }
  if (classes.contains("light")) {
    return "light";
  }
  return fallback;
}

/** Swaps the mode class, leaving every class this module does not own alone. */
function applyThemeMode(mode: ThemeMode): void {
  const classes: DOMTokenList = document.documentElement.classList;
  classes.remove(...THEME_CLASSES);
  classes.add(mode);
}

/**
 * The `useSyncExternalStore` subscribers. The store here IS the document plus
 * `localStorage`, neither of which emits an event for its own tab, so writes go
 * through {@link notifyThemeChange}.
 */
const themeListeners = new Set<() => void>();

function subscribeToTheme(onStoreChange: () => void): () => void {
  themeListeners.add(onStoreChange);
  return () => {
    themeListeners.delete(onStoreChange);
  };
}

function notifyThemeChange(): void {
  for (const listener of themeListeners) {
    listener();
  }
}

/** What {@link useTheme} hands a consumer — the current theme and its setter. */
export interface ThemeContextValue {
  /** The mode currently on `document.documentElement`. */
  readonly mode: ThemeMode;
  /** The visitor's stored choice, `"system"` when they have made none. */
  readonly preference: ThemePreference;
  /** Persists a new choice and re-stamps the document class. */
  readonly setPreference: (preference: ThemePreference) => void;
}

const ThemeContext = createContext<ThemeContextValue>({
  mode: "light",
  preference: "system",
  setPreference: () => {
    // No provider above: a catalog component must still render, so the default
    // context is inert rather than throwing.
  },
});

/** Reads the active theme from the nearest {@link ThemeProvider}. */
export function useTheme(): ThemeContextValue {
  return useContext(ThemeContext);
}

/** {@link ThemeScript}'s props. */
export interface ThemeScriptProps {
  /** The fork's `theme.defaultMode`, passed through to {@link themeInitScript}. */
  readonly defaultMode: ThemeMode;
}

/**
 * The pre-paint `<script>`, rendered in `<head>` alongside `DocumentStyles`.
 * Deliberately carries neither `defer` nor `async`: it must run synchronously,
 * before the body paints, or the flash it exists to prevent happens anyway.
 */
export function ThemeScript({ defaultMode }: ThemeScriptProps): ReactElement {
  return <script>{themeInitScript(defaultMode)}</script>;
}

/** {@link ThemeProvider}'s props. */
export interface ThemeProviderProps {
  /** The fork's `theme.defaultMode` — the resolution order's last resort. */
  readonly defaultMode: ThemeMode;
  readonly children?: ReactNode;
}

/**
 * Publishes the already-stamped theme to the tree and owns the setter the toggle
 * calls.
 *
 * It must NOT compute the initial class in an effect: the script has already
 * decided, and recomputing on mount is what produces a hydration flash. The
 * provider therefore renders nothing of its own and its SSR output is
 * byte-identical to its client output.
 *
 * Both values come from `useSyncExternalStore`, which is what keeps that true
 * through hydration as well: React renders the SERVER snapshot (the fork
 * default, exactly what the SSR markup carries) while hydrating and swaps to the
 * live one immediately after, so the visitor's real choice reaches the tree
 * without the server and client ever disagreeing about the first render.
 */
export function ThemeProvider({ defaultMode, children }: ThemeProviderProps): ReactElement {
  const getMode = useCallback((): ThemeMode => readStampedMode(defaultMode), [defaultMode]);
  const getServerMode = useCallback((): ThemeMode => defaultMode, [defaultMode]);

  const mode: ThemeMode = useSyncExternalStore(subscribeToTheme, getMode, getServerMode);
  const preference: ThemePreference = useSyncExternalStore(
    subscribeToTheme,
    readStoredPreference,
    serverPreference,
  );

  const setPreference = useCallback(
    (next: ThemePreference): void => {
      writeStoredPreference(next);
      applyThemeMode(
        resolveThemeMode({ stored: next, systemMode: currentSystemMode(), defaultMode }),
      );
      notifyThemeChange();
    },
    [defaultMode],
  );

  // Re-resolve when something OUTSIDE this tree moves: the OS colour scheme
  // flipping (which only matters while the preference is "system") or another
  // tab on this origin writing the key. Deliberately not run on mount — the
  // script already stamped the class, and re-stamping it here is the flash.
  useEffect(() => {
    const media: MediaQueryList = globalThis.matchMedia("(prefers-color-scheme: dark)");
    const sync = (): void => {
      const stored: ThemePreference = readStoredPreference();
      applyThemeMode(
        resolveThemeMode({
          stored: stored === "system" ? null : stored,
          systemMode: currentSystemMode(),
          defaultMode,
        }),
      );
      notifyThemeChange();
    };

    media.addEventListener("change", sync);
    globalThis.addEventListener("storage", sync);
    return () => {
      media.removeEventListener("change", sync);
      globalThis.removeEventListener("storage", sync);
    };
  }, [defaultMode]);

  const value: ThemeContextValue = useMemo(
    () => ({ mode, preference, setPreference }),
    [mode, preference, setPreference],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

/** The preference the SERVER renders: nobody's `localStorage` is readable there. */
function serverPreference(): ThemePreference {
  return "system";
}
