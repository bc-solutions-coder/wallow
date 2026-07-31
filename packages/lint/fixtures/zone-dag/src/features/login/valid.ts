import { thing } from "@shared/lib/thing";
import externalDefault from "some-package";

import { LoginScreen } from "./index";

export async function lazyThing() {
  return import("@shared/lib/thing");
}

export const valid = [thing, externalDefault, LoginScreen];
