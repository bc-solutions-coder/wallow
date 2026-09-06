import { readFileSync, writeFileSync } from "node:fs";

// TypeScript emits inferred query-key symbol references without their imports.
// Keep this in declaration emission until the compiler preserves those imports.
const declaration = new URL("../dist/generated/@tanstack/react-query.gen.d.ts", import.meta.url);
const imports = 'import type { dataTagSymbol, dataTagErrorSymbol } from "@tanstack/react-query";\n';
const content = readFileSync(declaration, "utf8");
if (!content.startsWith(imports)) {
  writeFileSync(declaration, imports + content);
}
