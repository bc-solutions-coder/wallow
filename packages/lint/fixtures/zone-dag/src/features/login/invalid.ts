// expect-error: wallow/zone-dag
import { thing } from "../../shared/lib/thing";
// expect-error: wallow/zone-dag
import { SignupScreen } from "@features/signup";
// expect-error: wallow/zone-dag
import { router } from "@app/router";
// expect-error: wallow/zone-dag
import { outside } from "../../../outside";
// expect-error: wallow/zone-dag
export { loginRequest } from "@features/login/api";

// One specifier, two independent violations: it reaches past a sibling
// feature's barrel AND reaches sideways at all.
// expect-error: wallow/zone-dag
// expect-error: wallow/zone-dag
export * from "@features/signup/api";

export async function lazy() {
  // expect-error: wallow/zone-dag
  return import("@app/router");
}

export const used = [thing, SignupScreen, router, outside];
