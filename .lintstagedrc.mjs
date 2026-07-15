/**
 * Lint-staged configuration with protection for generated/drift-checked files
 * and beads integration bridge
 */

// Protected paths that should never be reformatted
const PROTECTED_PATTERNS = [
  "packages/sdk/openapi/**",
  "**/generated/**",
  "packages/sdk/src/generated/**",
  "**/dist/**",
];

/**
 * Filter out protected paths from a file list
 */
function filterProtected(files) {
  const protectedRegexes = PROTECTED_PATTERNS.map((pattern) => {
    const escaped = pattern.replaceAll(/\./g, String.raw`\.`).replaceAll(/\*/g, ".*").replaceAll(/\?/g, ".");
    return new RegExp(`^${escaped}$`);
  });

  return files.filter((file) => !protectedRegexes.some((re) => re.test(file)));
}

export default {
  // C# files: format with dotnet
  "*.cs": (files) => {
    const filtered = filterProtected(files);
    if (filtered.length === 0) {return [];}
    return [`dotnet format api/Wallow.slnx --include ${filtered.join(" ")} --no-restore`];
  },

  // TypeScript/JavaScript: format with oxfmt, lint with oxlint
  "*.{ts,tsx,js,jsx,mjs,cjs}": (files) => {
    const filtered = filterProtected(files);
    if (filtered.length === 0) {return [];}
    return [`oxfmt --write ${filtered.join(" ")}`, `oxlint --fix ${filtered.join(" ")}`];
  },

  // JSON/YAML: format with oxfmt only
  "*.{json,yml,yaml}": (files) => {
    const filtered = filterProtected(files);
    if (filtered.length === 0) {return [];}
    return [`oxfmt --write ${filtered.join(" ")}`];
  },

  // TypeScript typecheck: run once per commit (no file args)
  // This guard ensures it only runs if any TS/TSX files are staged
  "*.{ts,tsx}": () => 
    ["pnpm typecheck"]
  ,
};
