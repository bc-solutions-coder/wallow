// Zone "root" — a policy spec directly under src/ — sits outside the product
// graph, so none of these report despite crossing every boundary below.
import { router } from "@app/router";
import { loginRequest } from "@features/login/api";
import { thing } from "../vite.config";

export const inspected = [router, loginRequest, thing];
