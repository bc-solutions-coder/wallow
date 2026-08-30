/** The MFA feature's public contract. See `docs/development/frontend-setup.md`. */
// `MfaEnrollFlow` is deliberately absent: no route mounts it, only
// `MfaSettingsSection` does, so it stays internal.
export { mfaGetStatusOptions } from "./api";
export { MfaSettingsSection } from "./components/MfaSettingsSection";
