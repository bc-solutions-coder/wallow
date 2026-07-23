import { describe, expect, it } from "vitest";

import { queryKeys } from "./keys";
import { settingsQueries } from "./settings";

describe("settingsQueries", () => {
  it("keys every option from the central factory", () => {
    // Settings is READ-ONLY (no profile mutation endpoint exists), so there is
    // no invalidation test — only the profile query is keyed off the factory.
    expect(settingsQueries.profile().queryKey).toEqual(queryKeys.settings.profile());
  });
});
