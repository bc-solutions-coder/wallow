import { applyNaming, type IR, type Plugin } from "@hey-api/openapi-ts";

type NamingOptions =
  Parameters<typeof applyNaming> extends [unknown, infer Naming, ...unknown[]] ? Naming : never;

/** Adds operation documentation to TanStack helpers before the generator renders them. */
export function createQueryDocumentationHooks(): NonNullable<Plugin.Hooks["$hooks"]> {
  const documentation = new Map<string, string[]>();

  function register(operation: IR.OperationObject, naming: NamingOptions, purpose: string): void {
    if (!operation.summary?.trim() || !operation.description?.trim()) {
      throw new Error(`Missing endpoint documentation for ${operation.id}`);
    }

    const lines = [operation.summary, "", purpose, "", operation.description];
    if (operation.deprecated) {
      lines.push("", "@deprecated");
    }
    documentation.set(
      applyNaming(operation.id, naming),
      lines.map((line) => line.replaceAll("*/", String.raw`*\/`)),
    );
  }

  return {
    events: {
      "plugin:handler:before": ({ plugin }) => {
        documentation.clear();
        const { config } = plugin.getPluginOrThrow("@tanstack/react-query");
        plugin.forEach("operation", ({ operation }) => {
          register(
            operation,
            config.queryKeys,
            "Builds the cache key for this query without sending a request.",
          );
          register(
            operation,
            config.queryOptions,
            "Builds TanStack Query options with the request function and cache key.",
          );
          register(
            operation,
            config.infiniteQueryKeys,
            "Builds the cache key for this infinite query without sending a request.",
          );
          register(
            operation,
            config.infiniteQueryOptions,
            "Builds TanStack Query options for fetching and caching successive pages.",
          );
          register(
            operation,
            config.mutationKeys,
            "Builds the mutation key without sending a request.",
          );
          register(
            operation,
            config.mutationOptions,
            "Builds TanStack Query mutation options. Calling the mutation sends the request; this factory does not.",
          );
        });
      },
      "node:set:before": ({ node }) => {
        if (!node?.exported || !("~dsl" in node) || node["~dsl"] !== "VarTsDsl") {
          return;
        }

        const name = node.name.toString();
        const lines = documentation.get(name);
        if (!lines || !("doc" in node) || typeof node.doc !== "function") {
          throw new Error(`No query helper documentation for ${name}`);
        }
        node.doc(lines);
      },
    },
  };
}
