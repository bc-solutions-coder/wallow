/**
 * Vendored ESM re-implementation of
 * `use-sync-external-store/shim/with-selector` (Wallow-luni).
 *
 * The npm package ships ONLY CJS, and its `require("react")` degrades to a
 * runtime `createRequire` when Vite bundles it into a zoned app's SSR chunk
 * with react left external — loading a SECOND React out of node_modules at
 * SSR runtime beside the one Nitro bundles. This file is what the anchored
 * aliases in `app.ts` point that specifier at instead: as ESM, its
 * `import ... from "react"` stays external through the Vite pass and Nitro
 * rewrites it to the bundled `require_react()` — the linkage minimal-app
 * already proves correct.
 *
 * Logic is a faithful de-minification of
 * `use-sync-external-store/cjs/use-sync-external-store-shim/with-selector.production.js`
 * (v1.6.0), with `useSyncExternalStore` taken from React itself (React >= 18
 * exports it; the shim indirection the original goes through resolves to
 * react here anyway, via the aliases in `app.ts`).
 *
 * @license React
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * This source code is licensed under the MIT license.
 */
// oxlint-disable react-hooks/exhaustive-deps -- vendored: `inst` is a stable
// ref-held box the upstream implementation also omits from dependency arrays.
import { useDebugValue, useEffect, useMemo, useRef, useSyncExternalStore } from "react";

export function useSyncExternalStoreWithSelector(
  subscribe,
  getSnapshot,
  getServerSnapshot,
  selector,
  isEqual,
) {
  const instRef = useRef(null);
  let inst;
  if (instRef.current === null) {
    inst = { hasValue: false, value: null };
    instRef.current = inst;
  } else {
    inst = instRef.current;
  }

  // `inst` is a per-instance mutable box held in a ref: its identity never
  // changes across renders, so it is deliberately not a dependency — same as
  // the upstream implementation (hence the file-level exhaustive-deps
  // disable in the header).
  const [getSelection, getServerSelection] = useMemo(() => {
    let hasMemo = false;
    let memoizedSnapshot;
    let memoizedSelection;
    const memoizedSelector = (nextSnapshot) => {
      if (!hasMemo) {
        hasMemo = true;
        memoizedSnapshot = nextSnapshot;
        const nextSelection = selector(nextSnapshot);
        if (isEqual !== undefined && inst.hasValue) {
          const currentSelection = inst.value;
          if (isEqual(currentSelection, nextSelection)) {
            memoizedSelection = currentSelection;
            return currentSelection;
          }
        }
        memoizedSelection = nextSelection;
        return nextSelection;
      }
      const currentSelection = memoizedSelection;
      if (Object.is(memoizedSnapshot, nextSnapshot)) {
        return currentSelection;
      }
      const nextSelection = selector(nextSnapshot);
      if (isEqual !== undefined && isEqual(currentSelection, nextSelection)) {
        memoizedSnapshot = nextSnapshot;
        return currentSelection;
      }
      memoizedSnapshot = nextSnapshot;
      memoizedSelection = nextSelection;
      return nextSelection;
    };
    const maybeGetServerSnapshot = getServerSnapshot === undefined ? null : getServerSnapshot;
    return [
      () => memoizedSelector(getSnapshot()),
      maybeGetServerSnapshot === null
        ? undefined
        : () => memoizedSelector(maybeGetServerSnapshot()),
    ];
  }, [getSnapshot, getServerSnapshot, selector, isEqual]);

  const value = useSyncExternalStore(subscribe, getSelection, getServerSelection);

  useEffect(() => {
    inst.hasValue = true;
    inst.value = value;
  }, [inst, value]);

  useDebugValue(value);
  return value;
}

// zustand's ESM build default-imports the module and destructures off it —
// the shape CJS interop hands it from the real package. Mirror that shape.
const cjsInteropDefault = { useSyncExternalStoreWithSelector };
export default cjsInteropDefault;
