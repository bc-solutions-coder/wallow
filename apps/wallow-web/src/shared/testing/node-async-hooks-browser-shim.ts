/**
 * Browser-mode stand-in for `node:async_hooks`, used ONLY by the vitest browser
 * project (wired as a `resolve.alias` in vitest.config.ts). Nothing in the app
 * imports it.
 *
 * Why it is needed: `@tanstack/react-start` reaches Start's per-request context
 * through `@tanstack/start-storage-context`, whose module body runs
 * `new AsyncLocalStorage()` at import time. In a real client build the Start
 * vite plugin compiles `createIsomorphicFn().client(...).server(...)` down to the
 * client branch, so that module never enters the browser graph. The vitest
 * browser project does NOT run the Start plugin, so it loads the server branch
 * and dies at import with "AsyncLocalStorage is not a constructor" — vitest
 * externalises `node:async_hooks` to a throwing proxy — taking down every spec
 * that imports `src/router.tsx`.
 *
 * The shim restores the browser's REAL semantics rather than faking a context:
 * there is no request scope in a browser, so `getStore()` answers `undefined`,
 * Start's `getStartContext()` throws its own documented error, and
 * `getRouter()`'s guard falls back to the same-origin browser SDK — exactly what
 * the compiled client build does.
 */

/** Minimal `AsyncLocalStorage` with no async tracking: there is one scope. */
export class AsyncLocalStorage<T> {
  #store: T | undefined;

  getStore(): T | undefined {
    return this.#store;
  }

  run<R>(store: T, callback: () => R): R {
    const previous: T | undefined = this.#store;
    this.#store = store;
    try {
      return callback();
    } finally {
      this.#store = previous;
    }
  }

  enterWith(store: T): void {
    this.#store = store;
  }

  exit<R>(callback: () => R): R {
    const previous: T | undefined = this.#store;
    this.#store = undefined;
    try {
      return callback();
    } finally {
      this.#store = previous;
    }
  }
}
