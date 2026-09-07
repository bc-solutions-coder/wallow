# @bc-solutions-coder/utils

Private workspace helpers with no runtime dependencies or host APIs. Add
`"@bc-solutions-coder/utils": "workspace:*"` to a consuming workspace package.
This package is not installed from a registry. Import a subpath; there is no root entry.

```ts
import { asString, scalarToString } from "@bc-solutions-coder/utils/guards";
import { toSlug } from "@bc-solutions-coder/utils/string";
import { formatLongDate } from "@bc-solutions-coder/utils/format";

asString(12); // undefined
scalarToString(false); // "false"
toSlug("Microsoft Entra ID"); // "microsoft-entra-id"
formatLongDate(new Date(2026, 0, 5)); // "January 5, 2026"
```

`asString` preserves empty strings. `scalarToString` accepts strings, numbers, and
booleans; objects, arrays, and null return `undefined`. `toSlug` keeps ASCII letters
and digits and can return an empty string for other input.

`formatLongDate` accepts a string, a `Date`, or epoch milliseconds. It uses `en-US`
and the host time zone, so the same instant can display different dates on different
hosts. Unparseable input returns `"Invalid Date"`.
