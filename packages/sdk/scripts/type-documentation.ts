import { type IR, type Plugin } from "@hey-api/openapi-ts";

const LAST_REFERENCE_SEGMENT = -1;
const FIRST_USAGE_INDEX = 0;
const MAX_USAGE_EXAMPLES = 3;
const NO_REFERENCES = 0;

function schemaNameFromReference(reference: string): string | undefined {
  return reference
    .split("/")
    .at(LAST_REFERENCE_SEGMENT)
    ?.replaceAll("~1", "/")
    .replaceAll("~0", "~");
}

/** Documents shared request options and cache-key types emitted by SDK plugins. */
export function sharedTypeDocumentation(name: string): string[] | undefined {
  if (name === "Options") {
    return [
      "Request arguments and client overrides for an SDK operation. The data parameter selects its body, path, and query types; ThrowOnError controls rejection behavior and TResponse describes the result.",
    ];
  }
  if (name === "QueryKey") {
    return [
      "TanStack Query cache-key tuple containing the operation identity and request options. Generated key factories include request parameters and tags so queries can be distinguished and invalidated without sending a request.",
    ];
  }
  return undefined;
}

/** Adds metadata-derived documentation to exported SDK type aliases before rendering. */
export function createTypeDocumentationHooks(): NonNullable<Plugin.Hooks["$hooks"]> {
  const documentation = new Map<string, string[]>();

  return {
    events: {
      "plugin:handler:before": ({ plugin }) => {
        documentation.clear();
        const schemas = new Map<string, IR.SchemaObject>();
        const usages = new Map<string, Set<string>>();
        plugin.forEach("schema", ({ name, schema }) => schemas.set(name, schema));

        function recordUsage(value: unknown, usage: string, visited = new Set<string>()): void {
          if (!value || typeof value !== "object") {
            return;
          }
          if (Array.isArray(value)) {
            for (const item of value) {
              recordUsage(item, usage, visited);
            }
            return;
          }
          if ("$ref" in value && typeof value.$ref === "string") {
            const name = schemaNameFromReference(value.$ref);
            if (name && !visited.has(name)) {
              visited.add(name);
              const references = usages.get(name) ?? new Set<string>();
              references.add(usage);
              usages.set(name, references);
              recordUsage(schemas.get(name), usage, visited);
            }
          }
          for (const child of Object.values(value)) {
            recordUsage(child, usage, visited);
          }
        }

        plugin.forEach("operation", ({ operation }) => {
          const summary = operation.summary?.trim();
          if (!summary) {
            throw new Error(`Missing endpoint summary for ${operation.id}`);
          }
          const endpoint = `${operation.method.toUpperCase()} ${operation.path}`;
          const add = (role: string, purpose: string): void => {
            documentation.set(`operation:${operation.id}:${role}`, [
              purpose,
              "",
              summary,
              `Endpoint: ${endpoint}.`,
            ]);
          };
          add(
            "data",
            "Request arguments for this endpoint, including its URL and supported body, path, query, and header fields.",
          );
          add("errors", "Error response bodies indexed by HTTP status code for this endpoint.");
          add(
            "error",
            "Union of the documented error response bodies for this endpoint. This describes API payloads, not client-side transport exceptions.",
          );
          add(
            "responses",
            "Successful response bodies indexed by HTTP status code for this endpoint.",
          );
          add("response", "Union of successful response bodies returned by this endpoint.");
          recordUsage(operation.body, `Request body for ${endpoint}: ${summary}`);
          recordUsage(operation.parameters, `Request parameters for ${endpoint}: ${summary}`);
          for (const [status, response] of Object.entries(operation.responses ?? {})) {
            recordUsage(response, `Response ${status} from ${endpoint}: ${summary}`);
          }
        });

        for (const [name, schema] of schemas) {
          // Existing schema prose and deprecation annotations are emitted by the TypeScript plugin.
          if (!schema.description?.trim()) {
            const references = [...(usages.get(name) ?? [])];
            if (references.length === NO_REFERENCES) {
              throw new Error(`Schema ${name} has neither a description nor endpoint usage`);
            }
            documentation.set(`definition:${name}`, [
              "Data used directly or within nested payloads by the following API operations.",
              "",
              ...references.slice(FIRST_USAGE_INDEX, MAX_USAGE_EXAMPLES),
              ...(references.length > MAX_USAGE_EXAMPLES
                ? [
                    `Also used by ${references.length - MAX_USAGE_EXAMPLES} other endpoint/status combinations.`,
                  ]
                : []),
              ...(schema.deprecated ? ["", "@deprecated"] : []),
            ]);
          }
        }
        documentation.set("ClientOptions", [
          "API base URL configuration accepted by the generated client. The schema's server URL is suggested while other string URLs remain supported.",
        ]);
      },
      "node:set:before": ({ node }) => {
        if (!node?.exported || !("~dsl" in node) || node["~dsl"] !== "TypeAliasTsDsl") {
          return;
        }
        const meta = node.name.symbol?.meta;
        let key = node.name.toString();
        if (meta?.resource === "operation") {
          key = `operation:${meta.resourceId}:${meta.role}`;
        } else if (meta?.resource === "definition" && meta.resourceId) {
          key = `definition:${schemaNameFromReference(meta.resourceId)}`;
        }
        const lines = documentation.get(key) ?? sharedTypeDocumentation(node.name.toString());
        if (lines && "doc" in node && typeof node.doc === "function") {
          node.doc(lines.map((line) => line.replaceAll("*/", String.raw`*\/`)));
        }
      },
    },
  };
}

/** Documents the SDK's generic options type without changing operation function comments. */
export function createSdkTypeDocumentationHooks(): NonNullable<Plugin.Hooks["$hooks"]> {
  return {
    events: {
      "node:set:before": ({ node }) => {
        if (!node?.exported || !("~dsl" in node) || node["~dsl"] !== "TypeAliasTsDsl") {
          return;
        }
        const lines = sharedTypeDocumentation(node.name.toString());
        if (lines && "doc" in node && typeof node.doc === "function") {
          node.doc(lines);
        }
      },
    },
  };
}
