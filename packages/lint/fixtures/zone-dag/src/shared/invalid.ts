// A spec gets no exemption here, so this file's `.test` twin would report too:
// shared/ is what features are built FROM.
// expect-error: wallow/zone-dag
import { LoginScreen } from "@features/login";

export const leaked = LoginScreen;
