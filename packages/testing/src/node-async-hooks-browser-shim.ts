/**
 * Synchronous AsyncLocalStorage stand-in for Vitest browser module loading.
 * Alias node:async_hooks to this entry when tests import an uncompiled server graph.
 * It restores the previous store as soon as a callback returns and does not track
 * asynchronous work. It is not a server request-context implementation.
 */

/** Minimal `AsyncLocalStorage` with no async tracking: there is one scope. */
export class AsyncLocalStorage<T> {
  #store: T | undefined;

  /** Read the current synchronous store, or undefined when none is set. */
  getStore(): T | undefined {
    return this.#store;
  }

  /** Run a callback with a temporary store, restoring the previous store on return or throw. */
  run<R>(store: T, callback: () => R): R {
    const previous: T | undefined = this.#store;
    this.#store = store;
    try {
      return callback();
    } finally {
      this.#store = previous;
    }
  }

  /** Replace the current store until another operation changes it. */
  enterWith(store: T): void {
    this.#store = store;
  }

  /** Run a callback without a store, then restore the previous store. */
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
