**status: active**

# Legacy nightly migration

Issue #283; evidence: https://github.com/bc-solutions-coder/wallow/issues/283#issuecomment-5577824516.

The seven existing production nightly indexes predate the new provenance format. Their exact digests match successful main Deploy run 34066236541 attempt 1, workflow 255082277, source 5c6aada60c85321b6d97e66fa76d62d12932e699. That source is an ancestor of current main. Docs has no existing nightly tag. Configuration labels do not provide source revisions; retained deployment logs and the reviewed digest snapshot establish the migration input.

The protected controller contains an exact repository/image/digest snapshot that expires within 30 days. Only that observed legacy state may use the migration path. Recheck the exact successful main deployment identity through GitHub, require its short-SHA tag to still return identical manifest bytes, and retain normal main-ancestry, forward-only movement and expected-old-state registry checks. The new candidate must independently pass the complete producer/preparation/writer authorization. Record the previous digest/source and evidence link in publication progress. No old image bytes become newly authorized for publication.

Unknown digests, different repositories, missing/changed deployment metadata, changed old tags, expired snapshots, or incomparable history must fail. Existing modern indexes continue through the ordinary immutable-full-SHA provenance check. Forks do not inherit this repository-specific adoption. The global publication queue remains required; no claim of CAS against external registry writers is added.

Verification uses behavioral helper tests plus the actual old deployment metadata/logs and registry bytes. Before real enablement, independently review the snapshot and exercise the bounded adoption against a disposable legacy index, including changed-digest and backward-history rejection. After all seven actual aliases have moved to verified modern indexes, delete the temporary snapshot and migration helper/hook in a follow-up checked commit. The migration is not complete until actual target readback and that retirement are done.
