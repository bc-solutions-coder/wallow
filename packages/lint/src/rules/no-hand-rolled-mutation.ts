import { defineRule } from "@oxlint/plugins";

/**
 * Ban a hand-written `mutationFn`.
 *
 * Every write an app issues goes through a GENERATED `{operation}Mutation()`
 * factory from `@bc-solutions-coder/sdk/query`, which builds the request from the
 * OpenAPI snapshot. A hand-rolled `mutationFn` re-states that request, and the
 * re-statement is where the variables shape and the real endpoint drift apart: a
 * call site still passing a bare body where the factory wants `{ body }` compiles,
 * renders, and sends an empty request.
 *
 * Keyed on the property NAME, which is the whole shape — `mutationFn` is TanStack
 * Query's own option name, so there is no false-positive spelling of it, and both
 * `{ mutationFn: … }` and `{ mutationFn() {} }` are the same offence. Spreading a
 * generated factory (`...accountLoginMutation()`) names no such property and stays
 * legal, which is exactly the shape a screen with its own `onSuccess` needs.
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
