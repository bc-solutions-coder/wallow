// A spec inside the gated directory. Specs are not shipped in either bundle, so the
// rule stays out of them — what a spec may import is `wallow/no-source-tests`' business.
import { hostname } from "node:os";

export const TEST_HOST: string = hostname();
