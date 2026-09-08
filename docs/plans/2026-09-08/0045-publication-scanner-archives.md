**status: active**

# Scan exact prepared image bytes

Issue #283. Serialize one inspected image into a regular-file Docker-save archive for
Trivy, then verify its configuration and uncompressed layers against the original CI
export. Preserve repeated layer references while writing each blob once. Never execute
an image, rebuild it, or loosen the image inspector's archive rules.

The pinned Skopeo Docker-archive conversion adds legacy V1 symlinks, which the strict
inspector correctly rejects. The new serializer instead reads freshly verified prepared
blobs and emits only the exact configuration, layers, and one tagged manifest. It refuses
an existing output and removes its own incomplete output after failure.

All 108 helper tests pass. The actual Garage AMD64 and ARM64 exports passed through the
new helper with the same configuration and six layer identities each. Trivy 0.74.0
scanned both resulting archives after a fresh database download, reporting 25 Alpine
packages per image and zero findings; both policy gates passed. Report image IDs,
architectures, and diff IDs matched the original inspected exports. The scans ran without
network access after database download and did not execute the application images.

The local scanner image was pinned to
`aquasec/trivy@sha256:62b1e65e8869bc4b4c6aa4fa2b21595256c7c2f6018a9d9ad61caf87187c1969`.
Evidence: `/tmp/wallow-publication-image-evidence/scanner-inputs/scanning-proof.json`.
Hosted preparation integration and all other catalog images remain pending.
