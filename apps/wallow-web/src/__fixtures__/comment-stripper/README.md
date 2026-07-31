# comment-stripper fixtures

Adversarial inputs for the comment stripper both typography sweeps
(`src/typography.test.ts` and `src/app/typography.test.ts`) read every source
file through. Each file names one construct that LOOKS like a comment delimiter
without being one, and pairs it with code that must survive stripping.

Two markers carry the contract, and every fixture has both:

- `data-testid="<fixture-name>-survives"` — real source. It must still be in the
  stripped text. Its absence means the stripper deleted source, which is how a
  sweep goes green over code it never saw.
- `GENUINE-COMMENT` — a real comment. It must be gone, so a stripper that
  "passes" by stripping nothing is caught too.

They are `.txt`, not `.tsx`, on purpose:

- Every disk walk in this app (`typography`, `zone-dag`, `feature-barrels`,
  `client-navigation`, `server-only-naming`, `query-facade`) filters on a
  `.ts`/`.tsx` extension, so these stay invisible to all of them and cannot be
  mistaken for app source.
- `oxfmt` and `oxlint` do not touch `.txt`. The constructs here are only
  adversarial while they stay byte-for-byte as written; a formatter reflowing a
  line would quietly disarm the fixture.
