/**
 * The caller's address, derived from `X-Forwarded-For` but trusted ONLY when the
 * immediate peer is a configured proxy.
 *
 * A WHATWG `Request` has no socket, so a Start host hands the peer address in on
 * an extra `ip` property. Behind an ingress that peer is the INGRESS, not the
 * caller: every real user shares one address, so a per-IP rate limit degrades to
 * a single global bucket and a stamped client IP names the proxy. The chain the
 * ingress writes carries the real caller, but reading it unconditionally is
 * worse than not reading it at all — any caller can send `X-Forwarded-For` and
 * would then choose their own rate-limit bucket and their own logged address.
 *
 * So the trusted-peer check is the load-bearing part, not the header read.
 * {@link resolveClientAddress} consults the chain only when the peer is inside
 * {@link TrustedProxies}, and otherwise answers with the peer itself. With
 * nothing configured — the default — no chain is ever consulted and the result
 * is exactly the peer address, which is the behaviour that existed before this
 * module and is the right default for a deployment with no proxy in front.
 *
 * The vocabulary is Caddy's on purpose. `docker/caddy/Caddyfile.example` spells
 * the same idea `trusted_proxies static 10.0.0.0/8 172.16.0.0/12`, and an
 * operator configuring the ingress and the app servers should not have to learn
 * two notations for one decision. The two lists are separate settings and must
 * agree: Caddy REPLACES the chain rather than appending to it unless its own
 * `trusted_proxies` is configured, so an outer ingress's entry is only there to
 * be read if Caddy was told to keep it.
 *
 * This module reads no environment of its own, and ships on the SDK's
 * dependency-free `./server/forwarded` subpath so an isomorphic Start entry can
 * import it. {@link createClientAddressResolver} takes the env record as a
 * parameter so the single `process.env` read stays at the call site.
 */

/** What a proxy chain is written to. Every hop appends the peer it saw. */
const FORWARDED_FOR_HEADER = "x-forwarded-for";

/** Environment variable naming the proxies whose `X-Forwarded-For` may be believed. */
export const TRUSTED_PROXIES_ENV_KEY: string = "WALLOW_TRUSTED_PROXIES";

/**
 * The inbound request as srvx hands it to a Start server route. A WHATWG
 * `Request` has no socket, so the peer address arrives on this extra `ip`
 * property (populated in `vite dev` and in the built Nitro server alike).
 */
export interface PeerRequest extends Request {
  readonly ip?: string | undefined;
}

/** Bit widths of the two address families, which double as their prefix maxima. */
const IPV4_WIDTH = 32;
const IPV6_WIDTH = 128;

/** Structural constants of the two textual forms. */
const IPV4_OCTETS = 4;
const IPV6_GROUPS = 8;
const IPV6_GROUP_DIGITS = 4;
const HEX_RADIX = 16;

/** Two's-complement all-ones, and one set bit — what every mask below is built from. */
const ALL_BITS = -1n;
const ONE_BIT = 1n;

/** The same widths as bigints, for the shift-and-mask arithmetic. */
const BITS_PER_OCTET = 8n;
const BITS_PER_GROUP = 16n;

/**
 * Masks, derived rather than written as hex literals. `oxfmt` lower-cases hex
 * digits and `unicorn/number-literal-case` wants them upper-cased, so a hex
 * literal cannot satisfy both halves of this repo's toolchain; deriving them
 * from the widths also states what each one IS — every bit of one octet, of one
 * group, of a whole v4 address.
 */
const OCTET_MASK = (ONE_BIT << BITS_PER_OCTET) - ONE_BIT;
const GROUP_MASK = (ONE_BIT << BITS_PER_GROUP) - ONE_BIT;
const IPV4_MASK = (ONE_BIT << BigInt(IPV4_WIDTH)) - ONE_BIT;

/** The same two bounds as numbers, for the per-part validation. */
const OCTET_MAX = Number(OCTET_MASK);
const GROUP_MAX = Number(GROUP_MASK);

/**
 * `::ffff:0:0/96` — the range an IPv4 address occupies when reported as IPv6.
 * Its one significant group is all-ones, which is exactly {@link GROUP_MASK}.
 */
const V4_MAPPED_PREFIX = GROUP_MASK;
const V4_MAPPED_SHIFT = BigInt(IPV4_WIDTH);

/** What `indexOf` and `lastIndexOf` answer when there is nothing to find. */
const NOT_FOUND = -1;

/** `::` may appear once, so splitting on it yields at most two halves. */
const MAX_ELISION_HALVES = 2;

/** `::` stands for at least one group; a full-length address carrying one is malformed. */
const MIN_ELIDED_GROUPS = 1;

/** A parsed address: its numeric value and which family it belongs to. */
interface Address {
  readonly value: bigint;
  readonly width: number;
  /** The canonical text this parsed from, which is what callers key on. */
  readonly text: string;
}

/** One entry of the trusted-proxy list, pre-masked so matching is two operations. */
interface CidrBlock {
  readonly network: bigint;
  readonly mask: bigint;
  readonly width: number;
}

/** The proxies whose forwarded chain may be believed. Empty means "trust none". */
export type TrustedProxies = readonly CidrBlock[];

/** Nothing is trusted — the default, and what an unset variable parses to. */
export const TRUST_NO_PROXIES: TrustedProxies = [];

/** Whether every character is an ASCII digit, with at least one present. */
function isDigits(text: string): boolean {
  if (text.length === 0) {
    return false;
  }
  for (const character of text) {
    if (character < "0" || character > "9") {
      return false;
    }
  }

  return true;
}

/** One dotted-quad, or `undefined` when `text` is not one. */
function parseIpv4(text: string): bigint | undefined {
  const parts: string[] = text.split(".");
  if (parts.length !== IPV4_OCTETS) {
    return undefined;
  }

  let value = 0n;
  for (const part of parts) {
    // Leading zeros are rejected rather than normalized: `010` is 8 to an
    // octal-minded parser and 10 to this one, and an address that means two
    // things is an address that can smuggle past an allowlist.
    if (!isDigits(part) || (part.length > 1 && part.startsWith("0"))) {
      return undefined;
    }
    const octet = Number(part);
    if (octet > OCTET_MAX) {
      return undefined;
    }
    value = (value << BITS_PER_OCTET) | BigInt(octet);
  }

  return value;
}

/** The colon-separated groups of one half of an address; an empty half has none. */
function splitGroups(half: string): string[] {
  return half === "" ? [] : half.split(":");
}

/** The eight groups of an IPv6 address, expanding at most one `::`. */
function expandGroups(text: string): string[] | undefined {
  const halves: string[] = text.split("::");
  if (halves.length > MAX_ELISION_HALVES) {
    return undefined;
  }

  if (halves.length < MAX_ELISION_HALVES) {
    const groups: string[] = splitGroups(text);

    return groups.length === IPV6_GROUPS ? groups : undefined;
  }

  const [leadingHalf = "", trailingHalf = ""] = halves;
  const leading: string[] = splitGroups(leadingHalf);
  const trailing: string[] = splitGroups(trailingHalf);
  const missing: number = IPV6_GROUPS - leading.length - trailing.length;
  if (missing < MIN_ELIDED_GROUPS) {
    return undefined;
  }

  return [...leading, ...Array.from({ length: missing }, () => "0"), ...trailing];
}

/** One IPv6 address, including the dotted-quad tail form, or `undefined`. */
function parseIpv6(text: string): bigint | undefined {
  // A trailing IPv4 literal (`::ffff:10.0.0.1`) occupies the last two groups.
  const lastColon: number = text.lastIndexOf(":");
  let head: string = text;
  let tail: bigint | undefined;
  if (lastColon !== NOT_FOUND && text.includes(".", lastColon)) {
    const embedded: bigint | undefined = parseIpv4(text.slice(lastColon + 1));
    if (embedded === undefined) {
      return undefined;
    }
    tail = embedded;
    // Keep the colon and append two placeholder groups the expander can count.
    head = `${text.slice(0, lastColon + 1)}0:0`;
  }

  const groups: string[] | undefined = expandGroups(head);
  if (groups === undefined) {
    return undefined;
  }

  let value = 0n;
  for (const group of groups) {
    if (group === "" || group.length > IPV6_GROUP_DIGITS || !/^[0-9a-f]+$/iu.test(group)) {
      return undefined;
    }
    const parsed = Number.parseInt(group, HEX_RADIX);
    if (parsed > GROUP_MAX) {
      return undefined;
    }
    value = (value << BITS_PER_GROUP) | BigInt(parsed);
  }

  if (tail !== undefined) {
    // Overwrite the two placeholder groups with the embedded IPv4 value.
    value = ((value >> V4_MAPPED_SHIFT) << V4_MAPPED_SHIFT) | tail;
  }

  return value;
}

/** Render a v4 value back to dotted-quad, so every caller keys on one spelling. */
function formatIpv4(value: bigint): string {
  const octets: string[] = Array.from({ length: IPV4_OCTETS }, (_unused, index) =>
    String((value >> (BigInt(IPV4_OCTETS - 1 - index) * BITS_PER_OCTET)) & OCTET_MASK),
  );

  return octets.join(".");
}

/** Render a v6 value to its lowercase groups. Not compressed; only ever compared. */
function formatIpv6(value: bigint): string {
  const groups: string[] = Array.from({ length: IPV6_GROUPS }, (_unused, index) =>
    ((value >> (BigInt(IPV6_GROUPS - 1 - index) * BITS_PER_GROUP)) & GROUP_MASK).toString(
      HEX_RADIX,
    ),
  );

  return groups.join(":");
}

/**
 * Strip the decorations a peer address or a chain entry can arrive wearing: a
 * `%eth0` zone index, `[…]` brackets, and a `:port` suffix.
 */
function undecorate(raw: string): string {
  let text: string = raw.trim();

  const bracketEnd: number = text.lastIndexOf("]");
  if (text.startsWith("[") && bracketEnd !== NOT_FOUND) {
    // `[::1]` and `[::1]:443` — anything after the bracket is a port.
    text = text.slice(1, bracketEnd);
  } else {
    const firstColon: number = text.indexOf(":");
    if (firstColon !== NOT_FOUND && firstColon === text.lastIndexOf(":") && text.includes(".")) {
      // Exactly one colon on something dotted: `10.0.0.1:5432`. An IPv6 address
      // always has at least two, so this cannot strip half of one.
      text = text.slice(0, firstColon);
    }
  }

  const zone: number = text.indexOf("%");

  return zone === NOT_FOUND ? text : text.slice(0, zone);
}

/**
 * One address, in whichever family it is written.
 *
 * An IPv4-mapped IPv6 address collapses to its IPv4 form. Node reports the peer
 * of a v4 client as `::ffff:10.0.0.1` on a dual-stack listener and as `10.0.0.1`
 * on a v4 one, and the same client must not land in two rate-limit buckets — nor
 * miss a `10.0.0.0/8` entry because of how the socket happened to bind.
 */
function parseAddress(raw: string): Address | undefined {
  const text: string = undecorate(raw);
  if (text.length === 0) {
    return undefined;
  }

  if (!text.includes(":")) {
    const value: bigint | undefined = parseIpv4(text);

    return value === undefined ? undefined : { value, width: IPV4_WIDTH, text: formatIpv4(value) };
  }

  const value: bigint | undefined = parseIpv6(text);
  if (value === undefined) {
    return undefined;
  }
  if (value >> V4_MAPPED_SHIFT === V4_MAPPED_PREFIX) {
    const mapped: bigint = value & IPV4_MASK;

    return { value: mapped, width: IPV4_WIDTH, text: formatIpv4(mapped) };
  }

  return { value, width: IPV6_WIDTH, text: formatIpv6(value) };
}

/** One `10.0.0.0/8`, `2001:db8::/32`, or bare-address entry of the trusted list. */
function parseCidr(entry: string): CidrBlock | undefined {
  const slash: number = entry.lastIndexOf("/");
  const addressText: string = slash === NOT_FOUND ? entry : entry.slice(0, slash);
  const address: Address | undefined = parseAddress(addressText);
  if (address === undefined) {
    return undefined;
  }

  let prefix: number = address.width;
  if (slash !== NOT_FOUND) {
    const prefixText: string = entry.slice(slash + 1).trim();
    if (!isDigits(prefixText)) {
      return undefined;
    }
    prefix = Number(prefixText);
    if (prefix > address.width) {
      return undefined;
    }
  }

  const hostBits: bigint = BigInt(address.width - prefix);
  const mask: bigint = (ALL_BITS << hostBits) & ((ONE_BIT << BigInt(address.width)) - ONE_BIT);

  return { network: address.value & mask, mask, width: address.width };
}

/** The loopback ranges, as `loopback`. */
const LOOPBACK: readonly string[] = ["127.0.0.0/8", "::1/128"];

/** The link-local ranges, as `linklocal`. */
const LINK_LOCAL: readonly string[] = ["169.254.0.0/16", "fe80::/10"];

/** RFC 1918 plus IPv6 unique-local, as `uniquelocal`. */
const UNIQUE_LOCAL: readonly string[] = [
  "10.0.0.0/8",
  "172.16.0.0/12",
  "192.168.0.0/16",
  "fc00::/7",
];

/**
 * Named shorthands for the ranges an operator would otherwise retype.
 *
 * The names are Express's `trust proxy` vocabulary on purpose: anyone who has
 * configured a Node app behind a proxy has met them, and a value that reads the
 * same in both places is one less thing to translate. `private` is the union —
 * everything unroutable — and is what a container network wants, since the
 * ingress reaches the app from whichever bridge subnet the runtime handed out.
 */
const PRESETS: ReadonlyMap<string, readonly string[]> = new Map([
  ["loopback", LOOPBACK],
  ["linklocal", LINK_LOCAL],
  ["uniquelocal", UNIQUE_LOCAL],
  ["private", [...LOOPBACK, ...LINK_LOCAL, ...UNIQUE_LOCAL]],
]);

/**
 * Parse a `WALLOW_TRUSTED_PROXIES` value: a comma- or whitespace-separated list of CIDR
 * blocks, bare addresses and {@link PRESETS} names.
 *
 * Unparseable entries are DROPPED rather than thrown on. This value is read
 * during server start-up on a deployment that is already serving, and a typo in
 * one range should narrow what is trusted — the safe direction — instead of
 * refusing to boot. An entry that silently means nothing is visible in the
 * behaviour it fails to produce; a server that will not start is an outage.
 */
export function parseTrustedProxies(spec: string | undefined): TrustedProxies {
  if (spec === undefined) {
    return TRUST_NO_PROXIES;
  }

  const blocks: CidrBlock[] = [];
  for (const entry of spec.split(/[\s,]+/u)) {
    // A preset expands to its ranges; anything else stands for itself. An empty
    // entry — which a trailing separator produces — names no preset and parses as
    // no address, so it falls out here rather than needing its own guard.
    const preset: readonly string[] | undefined = PRESETS.get(entry.toLowerCase());
    for (const range of preset ?? [entry]) {
      const block: CidrBlock | undefined = parseCidr(range);
      if (block !== undefined) {
        blocks.push(block);
      }
    }
  }

  return blocks;
}

/** Whether `address` falls inside any trusted block of its own family. */
function isTrusted(address: Address, trusted: TrustedProxies): boolean {
  return trusted.some(
    (block) => block.width === address.width && (address.value & block.mask) === block.network,
  );
}

/**
 * Whether `peer` is one of the proxies a forwarded header may be believed from.
 *
 * This is the trust gate {@link resolveClientAddress} applies to
 * `x-forwarded-for`, exported so `resolveRequestOrigin` can put the SAME gate on
 * `x-forwarded-proto` — one trust policy for both forwarded headers. A peer that
 * is absent, blank, or unparseable is never trusted, and an empty trusted set
 * (the default) trusts nothing.
 */
export function isTrustedPeer(peer: string | undefined, trusted: TrustedProxies): boolean {
  const raw: string = (peer ?? "").trim();
  if (raw === "" || trusted.length === 0) {
    return false;
  }

  const address: Address | undefined = parseAddress(raw);
  return address !== undefined && isTrusted(address, trusted);
}

/**
 * The caller's address for `request`, given the immediate `peer` and the proxies
 * whose chain may be believed.
 *
 * Returns the peer whenever the chain cannot be trusted, so an untrusted caller
 * cannot choose its own answer by sending a header. When the peer IS trusted the
 * chain is walked from the RIGHT — the end a proxy appends to — and the first
 * entry that is not itself a trusted proxy is the caller. Walking leftwards is
 * what makes the result independent of how many proxies are stacked in front:
 * anything to the left of the last untrusted entry was written by a hop that
 * could have been lying, and is ignored.
 *
 * The answer is canonicalized (an IPv4-mapped form collapses to dotted-quad), so
 * one client keys to one bucket regardless of how the socket bound. An address
 * that parses as neither family is returned trimmed but otherwise verbatim when
 * it is the peer — dropping it would collapse every such caller into one bucket —
 * and skipped when it is a chain entry, where a valid alternative may follow.
 */
export function resolveClientAddress(
  request: Request,
  peer: string | undefined,
  trusted: TrustedProxies,
): string | undefined {
  const rawPeer: string = (peer ?? "").trim();
  if (rawPeer === "") {
    return undefined;
  }

  const peerAddress: Address | undefined = parseAddress(rawPeer);
  const peerText: string = peerAddress?.text ?? rawPeer;
  if (peerAddress === undefined || trusted.length === 0 || !isTrusted(peerAddress, trusted)) {
    return peerText;
  }

  const chain: string | null = request.headers.get(FORWARDED_FOR_HEADER);
  if (chain === null) {
    return peerText;
  }

  const entries: string[] = chain.split(",");
  let leftmost: string | undefined;
  for (let index = entries.length - 1; index >= 0; index -= 1) {
    const candidate: Address | undefined = parseAddress(entries[index] ?? "");
    if (candidate !== undefined) {
      if (!isTrusted(candidate, trusted)) {
        return candidate.text;
      }
      leftmost = candidate.text;
    }
  }

  // Every hop in the chain is a proxy we configured, which happens when the
  // caller is itself inside the trusted network. The leftmost entry is the
  // furthest out anything appended, so it is the closest thing to a caller that
  // exists; with no parseable entry at all, the peer is.
  return leftmost ?? peerText;
}

/**
 * The trusted-proxy list a proxy preset runs with: an explicit `spec` wins
 * (an empty string deliberately trusts nothing), else `WALLOW_TRUSTED_PROXIES`
 * from `env`, else {@link TRUST_NO_PROXIES}.
 */
export function resolveTrustedProxies(
  spec: string | undefined,
  env: Readonly<Record<string, string | undefined>>,
): TrustedProxies {
  return parseTrustedProxies(spec ?? env[TRUSTED_PROXIES_ENV_KEY]);
}

/**
 * Bind {@link resolveClientAddress} to a deployment's trusted-proxy list.
 *
 * The env record is a PARAMETER because this package must not read the
 * environment itself: every app's `start.ts` is aliased into the client module
 * graph as well as the server one, so a `process.env` read at module scope here
 * would either break the client build or leak a server value into it. Call this
 * once at module scope in a server-only file — the parse is not per-request work.
 */
export function createClientAddressResolver(
  env: Readonly<Record<string, string | undefined>>,
): (request: PeerRequest) => string | undefined {
  const trusted: TrustedProxies = parseTrustedProxies(env[TRUSTED_PROXIES_ENV_KEY]);

  return (request: PeerRequest): string | undefined =>
    resolveClientAddress(request, request.ip, trusted);
}
