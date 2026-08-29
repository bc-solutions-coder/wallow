import { Text } from "@bc-solutions-coder/ui";
import type { ReactElement } from "react";

import type { PasswordStrength } from "@shared/lib/password-strength";

/**
 * The password-strength meter: a progress track plus the rating's label,
 * rendered under a password field once the value is non-empty (the caller owns
 * that gate — `passwordStrength` returns `null` for an empty password, and this
 * component takes only a real rating).
 *
 * Promoted from the register feature so the setup screen can render the same
 * meter — `wallow/zone-dag` forbids feature→feature imports, and `shared/` is
 * the one zone both may reach. `testId` is passed in because each screen owns
 * its `{screen}-password-strength` E2E contract.
 */
export function StrengthMeter({
  strength,
  testId,
}: {
  readonly strength: PasswordStrength;
  readonly testId: string;
}): ReactElement {
  return (
    <div className="space-y-1" data-testid={testId}>
      <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
        <div
          className={`h-full ${strength.indicatorClass}`}
          style={{ width: `${strength.percent}%` }}
        />
      </div>
      <Text as="p" variant="caption" color="muted">
        {strength.label}
      </Text>
    </div>
  );
}
