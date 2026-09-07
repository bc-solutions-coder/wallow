import { defineRule } from "@oxlint/plugins";

/**
 * Reject object properties named mutationFn where this rule is enabled.
 * Use generated SDK mutation options for API writes. The rule matches property
 * names without checking which library receives the object.
 */
export const noHandRolledMutation = defineRule({
  meta: {
    type: "problem",
    docs: { description: "Ban a hand-written mutationFn in favour of the generated factory." },
    schema: [],
  },

  createOnce(context) {
    return {
      Property(node) {
        const key = node.key;

        if (key.type === "Identifier") {
          if (key.name !== "mutationFn") {
            return;
          }
        } else if (key.type !== "Literal" || key.value !== "mutationFn") {
          return;
        }

        context.report({
          node,
          message:
            "A hand-written `mutationFn` re-states a request the generator already knows how to " +
            "build. Use the generated `{operation}Mutation()` factory from " +
            "`@bc-solutions-coder/sdk/query` (through the feature's `api.ts` seam) and spread it " +
            "into `useMutation` if the call site adds its own `onSuccess`.",
        });
      },
    };
  },
});
