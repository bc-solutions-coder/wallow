// The ./server entry. It runs only in the Node server process, so a Node built-in
// here is legal — this file drawing nothing is what proves the server exemption.
import { randomBytes } from "node:crypto";

export const NONCE: string = randomBytes(16).toString("hex");
