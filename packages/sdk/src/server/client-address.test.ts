import { describe, expect, it } from "vitest";

import {
  createClientAddressResolver,
  isTrustedPeer,
  type PeerRequest,
  parseTrustedProxies,
  resolveClientAddress,
  TRUST_NO_PROXIES,
  TRUSTED_PROXIES_ENV_KEY,
} from "./client-address";

/**
 * The address a rate limiter buckets on and a log line records.
 *
 * Two failures sit on opposite sides of one decision. Ignore `X-Forwarded-For`
 * and every user behind an ingress shares the ingress's address — one global
 * rate-limit bucket, and a log field naming the proxy. Believe it unconditionally
 * and any caller picks its own bucket and its own recorded address by sending a
 * header. The trusted-peer check is what separates the two, so it is what these
 * specs are mostly about.
 */

const LOCAL_PROXIES = parseTrustedProxies("10.0.0.0/8, 172.16.0.0/12");

function requestWith(chain?: string): Request {
  const headers: Headers = new Headers();
  if (chain !== undefined) {
    headers.set("x-forwarded-for", chain);
  }

  return new Request("http://app.test/x", { headers });
}

function peerRequestWith(peer: string | undefined, chain?: string): PeerRequest {
  // srvx hands the peer address in on an extra property of a real `Request`,
  // which is what this reproduces — the object is not a stand-in.
  return Object.assign(requestWith(chain), { ip: peer }) as PeerRequest;
}

describe("resolveClientAddress", () => {
  it("ignores a forwarded chain sent by a peer that is not a trusted proxy", () => {
    // The load-bearing case. A caller reaching the app directly can write any
    // header it likes, so its own address is the only fact in the request.
    expect(resolveClientAddress(requestWith("1.2.3.4"), "203.0.113.9", LOCAL_PROXIES)).toBe(
      "203.0.113.9",
    );
  });

  it("ignores a chain naming a trusted proxy, which would otherwise borrow its trust", () => {
    // Claiming to be the proxy is the obvious next attempt after claiming to be
    // someone else: it must not matter what the chain says, only who sent it.
    expect(resolveClientAddress(requestWith("10.0.0.1"), "203.0.113.9", LOCAL_PROXIES)).toBe(
      "203.0.113.9",
    );
  });

  it("reads the chain when the peer is a configured proxy", () => {
    expect(resolveClientAddress(requestWith("203.0.113.9"), "10.0.0.1", LOCAL_PROXIES)).toBe(
      "203.0.113.9",
    );
  });

  it("consults no chain at all when nothing is configured", () => {
    // The default, and the behaviour that existed before this module: a
    // deployment with no proxy in front must not become forgeable by adding it.
    expect(resolveClientAddress(requestWith("1.2.3.4"), "203.0.113.9", TRUST_NO_PROXIES)).toBe(
      "203.0.113.9",
    );
  });

  it("puts two clients behind one proxy at two different addresses", () => {
    // Restated from the rate limiter's point of view: these are its bucket keys,
    // and the whole point of the change is that they differ.
    const first: string | undefined = resolveClientAddress(
      requestWith("203.0.113.9"),
      "10.0.0.1",
      LOCAL_PROXIES,
    );
    const second: string | undefined = resolveClientAddress(
      requestWith("198.51.100.7"),
      "10.0.0.1",
      LOCAL_PROXIES,
    );

    expect(first).not.toBe(second);
    expect([first, second]).toStrictEqual(["203.0.113.9", "198.51.100.7"]);
  });

  it("walks stacked proxies from the right and stops at the first outsider", () => {
    // Two trusted hops appended their peers; everything left of the outsider was
    // written by a hop that could have been lying.
    expect(
      resolveClientAddress(
        requestWith("192.0.2.1, 203.0.113.9, 10.0.0.5"),
        "172.16.0.2",
        LOCAL_PROXIES,
      ),
    ).toBe("203.0.113.9");
  });

  it("takes the leftmost hop when every entry in the chain is a trusted proxy", () => {
    // The caller is itself inside the trusted network. The leftmost entry is the
    // furthest out anything appended, so it is the closest thing to a caller.
    expect(resolveClientAddress(requestWith("10.0.0.5, 10.0.0.6"), "10.0.0.1", LOCAL_PROXIES)).toBe(
      "10.0.0.5",
    );
  });

  it("falls back to the peer when no entry in the chain parses at all", () => {
    expect(resolveClientAddress(requestWith("unknown"), "10.0.0.1", LOCAL_PROXIES)).toBe(
      "10.0.0.1",
    );
  });

  it("ignores a prefix the caller prepended ahead of the address a proxy appended", () => {
    // The chain is client-controlled on the LEFT: a caller can write anything it
    // likes there, and each trusted hop then appends what it actually saw. The
    // rightward walk is what makes that prefix inert.
    expect(resolveClientAddress(requestWith("evil, 203.0.113.9"), "10.0.0.1", LOCAL_PROXIES)).toBe(
      "203.0.113.9",
    );
    expect(
      resolveClientAddress(requestWith("192.0.2.66, 203.0.113.9"), "10.0.0.1", LOCAL_PROXIES),
    ).toBe("203.0.113.9");
  });

  it("falls back to the peer when a trusted proxy sent no chain", () => {
    expect(resolveClientAddress(requestWith(), "10.0.0.1", LOCAL_PROXIES)).toBe("10.0.0.1");
  });

  it("skips an unparseable chain entry rather than answering with it", () => {
    // A valid address may still follow, and "unknown" is a real value some
    // proxies emit for a hop they could not determine.
    expect(
      resolveClientAddress(requestWith("203.0.113.9, unknown"), "10.0.0.1", LOCAL_PROXIES),
    ).toBe("203.0.113.9");
  });

  it("answers with an unparseable peer verbatim rather than dropping it", () => {
    // Dropping it would collapse every such caller into one shared bucket, which
    // is the failure this module exists to remove.
    expect(resolveClientAddress(requestWith(), "not-an-address", LOCAL_PROXIES)).toBe(
      "not-an-address",
    );
  });

  it("has no answer when there is no peer", () => {
    expect(resolveClientAddress(requestWith(), undefined, LOCAL_PROXIES)).toBeUndefined();
    expect(resolveClientAddress(requestWith(), "   ", LOCAL_PROXIES)).toBeUndefined();
  });

  it("matches a v4 peer that a dual-stack listener reported in mapped form", () => {
    // Node reports a v4 client as `::ffff:10.0.0.1` on a dual-stack socket. The
    // proxy is inside 10.0.0.0/8 either way, and must be trusted either way.
    expect(resolveClientAddress(requestWith("203.0.113.9"), "::ffff:10.0.0.1", LOCAL_PROXIES)).toBe(
      "203.0.113.9",
    );
  });

  it("collapses a mapped client address to one spelling", () => {
    // Otherwise the same client keys to two buckets depending on how the socket
    // happened to bind.
    expect(resolveClientAddress(requestWith("::ffff:203.0.113.9"), "10.0.0.1", LOCAL_PROXIES)).toBe(
      "203.0.113.9",
    );
    expect(resolveClientAddress(requestWith(), "::ffff:203.0.113.9", LOCAL_PROXIES)).toBe(
      "203.0.113.9",
    );
  });

  it("handles the bracketed-with-port shape srvx can report a v6 peer as", () => {
    const proxies = parseTrustedProxies("loopback");

    expect(resolveClientAddress(requestWith("203.0.113.9"), "[::1]:54321", proxies)).toBe(
      "203.0.113.9",
    );
  });

  it("strips the port, brackets and zone index an address can arrive wearing", () => {
    expect(resolveClientAddress(requestWith(), "203.0.113.9:54321", TRUST_NO_PROXIES)).toBe(
      "203.0.113.9",
    );
    expect(resolveClientAddress(requestWith(), "[2001:db8::1]:443", TRUST_NO_PROXIES)).toBe(
      "2001:db8:0:0:0:0:0:1",
    );
    expect(resolveClientAddress(requestWith(), "fe80::1%eth0", TRUST_NO_PROXIES)).toBe(
      "fe80:0:0:0:0:0:0:1",
    );
  });

  it("trusts an IPv6 proxy by prefix", () => {
    const proxies = parseTrustedProxies("2001:db8::/32");

    expect(resolveClientAddress(requestWith("203.0.113.9"), "2001:db8::5", proxies)).toBe(
      "203.0.113.9",
    );
    expect(resolveClientAddress(requestWith("203.0.113.9"), "2001:dead::5", proxies)).toBe(
      "2001:dead:0:0:0:0:0:5",
    );
  });

  it("does not let one family's prefix match the other's numbers", () => {
    // 10.0.0.0/8 is a small integer; so is ::a00:0/104. They are not the same
    // range, and a mask comparison alone would say they were.
    expect(resolveClientAddress(requestWith("203.0.113.9"), "::a00:1", LOCAL_PROXIES)).toBe(
      "0:0:0:0:0:0:a00:1",
    );
  });
});

describe("isTrustedPeer", () => {
  // The gate `resolveRequestOrigin` shares: the same peer-in-trusted-set
  // decision, without the forwarded-chain walk.
  it("answers whether the peer falls inside a trusted block", () => {
    expect(isTrustedPeer("10.1.2.3", LOCAL_PROXIES)).toBe(true);
    expect(isTrustedPeer("203.0.113.9", LOCAL_PROXIES)).toBe(false);
  });

  it("trusts nothing when nothing is configured, which is the default", () => {
    expect(isTrustedPeer("10.1.2.3", TRUST_NO_PROXIES)).toBe(false);
  });

  it("trusts no peer it cannot judge", () => {
    expect(isTrustedPeer(undefined, LOCAL_PROXIES)).toBe(false);
    expect(isTrustedPeer("", LOCAL_PROXIES)).toBe(false);
    expect(isTrustedPeer("not-an-address", LOCAL_PROXIES)).toBe(false);
  });

  it("matches a v4 peer that a dual-stack listener reported in mapped form", () => {
    expect(isTrustedPeer("::ffff:10.1.2.3", LOCAL_PROXIES)).toBe(true);
  });
});

describe("parseTrustedProxies", () => {
  it("is empty for an unset variable, so nothing is trusted by default", () => {
    expect(parseTrustedProxies(undefined)).toStrictEqual([]);
    expect(parseTrustedProxies("")).toStrictEqual([]);
  });

  it("accepts a bare address as a host route", () => {
    const proxies = parseTrustedProxies("10.1.2.3");

    expect(resolveClientAddress(requestWith("203.0.113.9"), "10.1.2.3", proxies)).toBe(
      "203.0.113.9",
    );
    expect(resolveClientAddress(requestWith("203.0.113.9"), "10.1.2.4", proxies)).toBe("10.1.2.4");
  });

  it("splits on commas or whitespace, matching how the same list is written for Caddy", () => {
    // `trusted_proxies static 10.0.0.0/8 172.16.0.0/12` is space-separated; an
    // operator copying that value across must not have to re-punctuate it.
    expect(parseTrustedProxies("10.0.0.0/8 172.16.0.0/12")).toStrictEqual(
      parseTrustedProxies("10.0.0.0/8,172.16.0.0/12"),
    );
  });

  it("drops an unparseable entry instead of refusing to start", () => {
    // A typo narrows what is trusted, which is the safe direction. Throwing here
    // would turn it into an outage on a server that is already serving.
    const proxies = parseTrustedProxies("nonsense, 10.0.0.0/8, 10.0.0.0/99, 1.2.3.4/-1");

    expect(proxies).toHaveLength(1);
    expect(resolveClientAddress(requestWith("203.0.113.9"), "10.0.0.1", proxies)).toBe(
      "203.0.113.9",
    );
  });

  it("rejects a leading-zero octet rather than guessing which base it is in", () => {
    // `010` is 8 to an octal-minded parser and 10 to this one. An address that
    // means two things is an address that can be smuggled past an allowlist.
    expect(parseTrustedProxies("010.0.0.1")).toStrictEqual([]);
  });

  it("masks host bits off, so a block written from any member address is the same block", () => {
    expect(parseTrustedProxies("10.9.8.7/8")).toStrictEqual(parseTrustedProxies("10.0.0.0/8"));
  });

  it("expands the Express-style preset names", () => {
    expect(parseTrustedProxies("loopback")).toStrictEqual(
      parseTrustedProxies("127.0.0.0/8 ::1/128"),
    );
    expect(parseTrustedProxies("uniquelocal")).toStrictEqual(
      parseTrustedProxies("10.0.0.0/8 172.16.0.0/12 192.168.0.0/16 fc00::/7"),
    );
  });

  it("covers every unroutable range under `private`, which is what the compose stack sets", () => {
    // Caddy reaches the app from whichever bridge subnet the container runtime
    // handed out, so the value has to cover all of them rather than name one.
    const proxies = parseTrustedProxies("private");

    for (const peer of ["172.18.0.4", "10.1.2.3", "192.168.5.6", "127.0.0.1", "fd00::1"]) {
      expect(resolveClientAddress(requestWith("203.0.113.9"), peer, proxies)).toBe("203.0.113.9");
    }
    // A public address is not, which is the half that matters.
    expect(resolveClientAddress(requestWith("203.0.113.9"), "8.8.8.8", proxies)).toBe("8.8.8.8");
  });

  it("mixes presets with explicit ranges in one value", () => {
    const proxies = parseTrustedProxies("loopback, 203.0.113.0/24");

    expect(resolveClientAddress(requestWith("198.51.100.7"), "203.0.113.5", proxies)).toBe(
      "198.51.100.7",
    );
  });
});

describe("createClientAddressResolver", () => {
  it("reads the trusted list from the env record it is handed", () => {
    expect(TRUSTED_PROXIES_ENV_KEY).toBe("WALLOW_TRUSTED_PROXIES");
    const resolve = createClientAddressResolver({ [TRUSTED_PROXIES_ENV_KEY]: "10.0.0.0/8" });

    expect(resolve(peerRequestWith("10.0.0.1", "203.0.113.9"))).toBe("203.0.113.9");
    expect(resolve(peerRequestWith("203.0.113.9", "1.2.3.4"))).toBe("203.0.113.9");
  });

  it("trusts nothing when the variable is absent from the record", () => {
    const resolve = createClientAddressResolver({});

    expect(resolve(peerRequestWith("10.0.0.1", "203.0.113.9"))).toBe("10.0.0.1");
  });

  it("has no answer when the host reported no peer", () => {
    const resolve = createClientAddressResolver({ [TRUSTED_PROXIES_ENV_KEY]: "10.0.0.0/8" });

    expect(resolve(peerRequestWith(undefined, "203.0.113.9"))).toBeUndefined();
  });
});
