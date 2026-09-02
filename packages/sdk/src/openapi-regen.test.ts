import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import * as generated from "./generated";

// Guards the codegen contract as a set of RULES, not as a dated snapshot diff. Legitimate
// backend additions must keep passing without touching this file; a regression in any of the
// invariants below must fail it. Each detector is exercised against a synthetic violating
// document in the last describe block, so the invariants are proven to have teeth even while
// the real snapshot is clean.

const SCHEMA_REF_PREFIX: string = "#/components/schemas/";

const TEST_SUPPORT_TAG: string = "Test Support";

const HTTP_METHODS: ReadonlySet<string> = new Set([
  "get",
  "put",
  "post",
  "delete",
  "options",
  "head",
  "patch",
  "trace",
]);

interface MediaTypeObject {
  schema?: unknown;
}

interface ResponseObject {
  content?: Record<string, MediaTypeObject>;
}

interface OperationObject {
  operationId?: string;
  tags?: string[];
  responses?: Record<string, ResponseObject>;
}

interface OpenApiSpec {
  paths: Record<string, Record<string, OperationObject>>;
  components?: Record<string, unknown> & { schemas?: Record<string, unknown> };
  tags?: { name?: string }[];
}

interface SchemaObject {
  type?: unknown;
  enum?: unknown;
  properties?: Record<string, unknown>;
  "x-enum-descriptions"?: unknown;
  "x-enum-varnames"?: unknown;
}

interface OperationEntry {
  path: string;
  method: string;
  operation: OperationObject;
}

function loadSnapshot(): OpenApiSpec {
  const snapshotUrl: URL = new URL("../openapi/v1.json", import.meta.url);
  return JSON.parse(readFileSync(fileURLToPath(snapshotUrl), "utf8")) as OpenApiSpec;
}

function collectOperations(spec: OpenApiSpec): OperationEntry[] {
  const entries: OperationEntry[] = [];

  for (const [path, pathItem] of Object.entries(spec.paths)) {
    for (const [method, operation] of Object.entries(pathItem)) {
      if (HTTP_METHODS.has(method)) {
        entries.push({ path, method, operation });
      }
    }
  }

  return entries;
}

function describeOperation(entry: OperationEntry): string {
  return `${entry.method.toUpperCase()} ${entry.path}`;
}

function findOperationsMissingAnOperationId(spec: OpenApiSpec): string[] {
  return collectOperations(spec)
    .filter((entry: OperationEntry) => !entry.operation.operationId)
    .map((entry: OperationEntry) => describeOperation(entry));
}

function findDuplicateOperationIds(spec: OpenApiSpec): string[] {
  const seen: Set<string> = new Set();
  const duplicates: Set<string> = new Set();

  for (const entry of collectOperations(spec)) {
    const operationId: string | undefined = entry.operation.operationId;
    if (operationId) {
      if (seen.has(operationId)) {
        duplicates.add(operationId);
      }

      seen.add(operationId);
    }
  }

  return [...duplicates];
}

// An operation violates the typed-success rule when it declares a body-bearing 2xx but no body
// schema for it. EVERY non-204 success code counts, not just 200 — a 201 that returns a created
// resource generates `unknown` just as readily as an untyped 200 does. 204 is the sole exemption:
// it carries no body by definition.
// BOTH untyped shapes count: a bare response with no `content` at all, and — the case a naive
// check misses — one whose `content` key exists (every [Produces("application/json")] action emits
// one) but carries no `schema` for any media type. hey-api generates `unknown` for both alike.
function isBodyBearingSuccessCode(statusCode: string): boolean {
  return /^2\d\d$/.test(statusCode) && statusCode !== "204";
}

function findOperationsWithUntypedSuccess(spec: OpenApiSpec): string[] {
  return collectOperations(spec)
    .filter((entry: OperationEntry) =>
      Object.entries(entry.operation.responses ?? {}).some(
        ([statusCode, success]: [string, ResponseObject]) => {
          if (!isBodyBearingSuccessCode(statusCode)) {
            return false;
          }

          const declaresBareResponse: boolean = success.content === undefined;
          const schemas: unknown[] = Object.values(success.content ?? {})
            .map((mediaType: MediaTypeObject) => mediaType?.schema)
            .filter((schema: unknown) => schema !== undefined);

          return declaresBareResponse || schemas.length === 0;
        },
      ),
    )
    .map((entry: OperationEntry) => describeOperation(entry));
}

function collectSchemaRefs(node: unknown, into: Set<string>): void {
  if (Array.isArray(node)) {
    for (const item of node) {
      collectSchemaRefs(item, into);
    }
    return;
  }

  if (node === null || typeof node !== "object") {
    return;
  }

  for (const [key, value] of Object.entries(node as Record<string, unknown>)) {
    if (key === "$ref" && typeof value === "string" && value.startsWith(SCHEMA_REF_PREFIX)) {
      into.add(value.slice(SCHEMA_REF_PREFIX.length));
    } else {
      collectSchemaRefs(value, into);
    }
  }
}

// Schemas ASP.NET Core harvested for an action whose path a document transformer later removed
// stay behind in components and generate dead TS types — a silent leak of internal surface into
// the published SDK. Reachability from the paths (plus the non-schema component sections) is the
// only way to see them.
function findOrphanedSchemas(spec: OpenApiSpec): string[] {
  const schemas: Record<string, unknown> = spec.components?.schemas ?? {};

  const roots: Set<string> = new Set();
  collectSchemaRefs(spec.paths, roots);
  for (const [section, value] of Object.entries(spec.components ?? {})) {
    if (section !== "schemas") {
      collectSchemaRefs(value, roots);
    }
  }

  const reachable: Set<string> = new Set();
  const pending: string[] = [...roots];

  while (pending.length > 0) {
    const name: string = pending.pop()!;
    if (!reachable.has(name) && name in schemas) {
      reachable.add(name);

      const nested: Set<string> = new Set();
      collectSchemaRefs(schemas[name], nested);
      pending.push(...nested);
    }
  }

  return Object.keys(schemas).filter((name: string) => !reachable.has(name));
}

function findTestSupportSurface(spec: OpenApiSpec): string[] {
  const taggedOperations: string[] = collectOperations(spec)
    .filter((entry: OperationEntry) => entry.operation.tags?.includes(TEST_SUPPORT_TAG) === true)
    .map((entry: OperationEntry) => describeOperation(entry));

  const documentTags: string[] = (spec.tags ?? [])
    .filter((tag: { name?: string }) => tag.name === TEST_SUPPORT_TAG)
    .map(() => `document tag "${TEST_SUPPORT_TAG}"`);

  return [...taggedOperations, ...documentTags];
}

const ERROR_CODE_SCHEMA: string = "ErrorCode";

const PROBLEM_DETAILS_SCHEMAS: readonly string[] = [
  "ProblemDetails",
  "HttpValidationProblemDetails",
  "ValidationProblemDetails",
];

// The backend exports its aggregated error catalog as one string enum, described per code and
// referenced from every problem-details schema's `code` member. hey-api turns that into the
// `ErrorCode` union the apps narrow on; a snapshot that loses any part of the shape would still
// generate, but as `string`, and every `code ===` comparison would silently stop being checked.
function findErrorCodeContractViolations(spec: OpenApiSpec): string[] {
  const schemas: Record<string, unknown> = spec.components?.schemas ?? {};
  const violations: string[] = [];

  const errorCode: SchemaObject | undefined = schemas[ERROR_CODE_SCHEMA] as
    | SchemaObject
    | undefined;
  if (errorCode === undefined) {
    return [`components.schemas.${ERROR_CODE_SCHEMA} is missing`];
  }

  if (errorCode.type !== "string") {
    violations.push(`${ERROR_CODE_SCHEMA} is not a string schema`);
  }

  const codes: unknown = errorCode.enum;
  if (!Array.isArray(codes) || codes.length === 0) {
    violations.push(`${ERROR_CODE_SCHEMA} declares no enum values`);
  } else {
    const descriptions: unknown = errorCode["x-enum-descriptions"];
    if (!Array.isArray(descriptions) || descriptions.length !== codes.length) {
      violations.push(`${ERROR_CODE_SCHEMA} x-enum-descriptions does not describe every code`);
    }
  }

  if (errorCode["x-enum-varnames"] !== undefined) {
    violations.push(`${ERROR_CODE_SCHEMA} carries x-enum-varnames`);
  }

  for (const name of PROBLEM_DETAILS_SCHEMAS) {
    const problemDetails: SchemaObject | undefined = schemas[name] as SchemaObject | undefined;
    const code: unknown = problemDetails?.properties?.["code"];
    const ref: unknown = (code as { $ref?: unknown } | undefined)?.$ref;
    if (problemDetails !== undefined && ref !== `${SCHEMA_REF_PREFIX}${ERROR_CODE_SCHEMA}`) {
      violations.push(`${name}.code does not reference ${ERROR_CODE_SCHEMA}`);
    }
  }

  return violations;
}

// hey-api derives each exported operation function from the operationId, lower-camelised.
function expectedExportName(operationId: string): string {
  return operationId.charAt(0).toLowerCase() + operationId.slice(1);
}

describe("committed OpenAPI snapshot holds the codegen invariants", () => {
  it("gives every operation an operationId", () => {
    expect(findOperationsMissingAnOperationId(loadSnapshot())).toEqual([]);
  });

  it("keeps every operationId unique across the document", () => {
    expect(findDuplicateOperationIds(loadSnapshot())).toEqual([]);
  });

  it("declares a body schema for every non-204 2xx response", () => {
    expect(findOperationsWithUntypedSuccess(loadSnapshot())).toEqual([]);
  });

  it("ships no component schema that is unreachable from the paths", () => {
    expect(findOrphanedSchemas(loadSnapshot())).toEqual([]);
  });

  it("leaks no test-support surface into the public document", () => {
    expect(findTestSupportSurface(loadSnapshot())).toEqual([]);
  });

  it("exports the error catalog as a described string enum that problem details reference", () => {
    expect(findErrorCodeContractViolations(loadSnapshot())).toEqual([]);
  });
});

describe("generated client stays in step with the committed snapshot", () => {
  it("exports exactly one operation per snapshot operationId", () => {
    const spec: OpenApiSpec = loadSnapshot();
    const expectedExports: Set<string> = new Set(
      collectOperations(spec).map((entry: OperationEntry) =>
        expectedExportName(entry.operation.operationId!),
      ),
    );

    expect(new Set(Object.keys(generated))).toEqual(expectedExports);
  });
});

describe("the invariants reject a violating document", () => {
  function specWith(operation: OperationObject): OpenApiSpec {
    return { paths: { "/v1/things": { get: operation } } };
  }

  const typedSuccess: OperationObject = {
    operationId: "ThingsGetAll",
    responses: { "200": { content: { "application/json": { schema: { type: "object" } } } } },
  };

  it("catches a missing operationId", () => {
    const spec: OpenApiSpec = specWith({ ...typedSuccess, operationId: undefined });

    expect(findOperationsMissingAnOperationId(spec)).toEqual(["GET /v1/things"]);
    expect(findOperationsMissingAnOperationId(specWith(typedSuccess))).toEqual([]);
  });

  it("catches two operations sharing one operationId", () => {
    const spec: OpenApiSpec = {
      paths: {
        "/v1/things": { get: typedSuccess },
        "/v1/other-things": { get: { ...typedSuccess } },
      },
    };

    expect(findDuplicateOperationIds(spec)).toEqual(["ThingsGetAll"]);
  });

  it("catches a bare 200 that declares no content at all", () => {
    const spec: OpenApiSpec = specWith({ ...typedSuccess, responses: { "200": {} } });

    expect(findOperationsWithUntypedSuccess(spec)).toEqual(["GET /v1/things"]);
  });

  it("catches a 200 whose content declares no schema", () => {
    const spec: OpenApiSpec = specWith({
      ...typedSuccess,
      responses: { "200": { content: { "application/json": {} } } },
    });

    expect(findOperationsWithUntypedSuccess(spec)).toEqual(["GET /v1/things"]);
    expect(findOperationsWithUntypedSuccess(specWith(typedSuccess))).toEqual([]);
  });

  it("catches an untyped 201 alongside a typed 200", () => {
    const spec: OpenApiSpec = specWith({
      ...typedSuccess,
      responses: {
        ...typedSuccess.responses,
        "201": { content: { "application/json": {} } },
      },
    });

    expect(findOperationsWithUntypedSuccess(spec)).toEqual(["GET /v1/things"]);
  });

  it("passes an operation that answers only 204, which carries no body by definition", () => {
    const spec: OpenApiSpec = specWith({ ...typedSuccess, responses: { "204": {} } });

    expect(findOperationsWithUntypedSuccess(spec)).toEqual([]);
  });

  it("ignores a non-2xx response that declares no schema", () => {
    const spec: OpenApiSpec = specWith({
      ...typedSuccess,
      responses: { ...typedSuccess.responses, "404": {} },
    });

    expect(findOperationsWithUntypedSuccess(spec)).toEqual([]);
  });

  it("catches a schema no path can reach, and follows nested refs to spare a reachable one", () => {
    const spec: OpenApiSpec = {
      paths: {
        "/v1/things": {
          get: {
            operationId: "ThingsGetAll",
            responses: {
              "200": {
                content: { "application/json": { schema: { $ref: `${SCHEMA_REF_PREFIX}Thing` } } },
              },
            },
          },
        },
      },
      components: {
        schemas: {
          Thing: { properties: { part: { $ref: `${SCHEMA_REF_PREFIX}ThingPart` } } },
          ThingPart: { type: "string" },
          CreateIsolatedOrgRequest: { type: "object" },
        },
      },
    };

    expect(findOrphanedSchemas(spec)).toEqual(["CreateIsolatedOrgRequest"]);
  });

  it("catches a test-support tagged operation and a leftover document tag", () => {
    const spec: OpenApiSpec = {
      ...specWith({ ...typedSuccess, tags: [TEST_SUPPORT_TAG] }),
      tags: [{ name: TEST_SUPPORT_TAG }],
    };

    expect(findTestSupportSurface(spec)).toEqual([
      "GET /v1/things",
      `document tag "${TEST_SUPPORT_TAG}"`,
    ]);
  });

  it("catches an error-code enum that lost its shape", () => {
    const sound: OpenApiSpec = {
      ...specWith(typedSuccess),
      components: {
        schemas: {
          ErrorCode: {
            type: "string",
            enum: ["Http.NotFound"],
            "x-enum-descriptions": ["The requested resource was not found."],
          },
          ProblemDetails: { properties: { code: { $ref: `${SCHEMA_REF_PREFIX}ErrorCode` } } },
        },
      },
    };
    const broken: OpenApiSpec = {
      ...sound,
      components: {
        schemas: {
          ErrorCode: { type: "string", enum: ["Http.NotFound"], "x-enum-varnames": ["NotFound"] },
          ProblemDetails: { properties: { code: { type: "string" } } },
        },
      },
    };

    expect(findErrorCodeContractViolations(sound)).toEqual([]);
    expect(findErrorCodeContractViolations({ ...sound, components: { schemas: {} } })).toEqual([
      "components.schemas.ErrorCode is missing",
    ]);
    expect(findErrorCodeContractViolations(broken)).toEqual([
      "ErrorCode x-enum-descriptions does not describe every code",
      "ErrorCode carries x-enum-varnames",
      "ProblemDetails.code does not reference ErrorCode",
    ]);
  });
});
