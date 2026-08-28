// A browser-owned file whose imports are all fine: relative modules and a workspace
// package are not Node built-ins, whatever the specifier looks like.
import { HOST } from "./log-event";
import { clamp } from "@bc-solutions-coder/utils/number";

export const WIRE: string = `${HOST}:${clamp(1, 0, 1)}`;
