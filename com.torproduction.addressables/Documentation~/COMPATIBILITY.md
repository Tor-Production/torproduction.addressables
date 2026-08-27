# Compatibility

## Required release lanes

| Unity editor | Addressables | Status | Evidence |
| --- | --- | --- | --- |
| `6000.0.78f1` | `2.7.6` | Verified Phase 6 minimum lane | Hosted run `32689916114`, 133/133 EditMode tests |
| `6000.0.78f1` | `2.9.1` | Verified Phase 6 compatibility lane | Hosted run `32689916114`, 133/133 EditMode tests |

The Phase 6 implementation evidence is `bf147de69b1bb9f2afb4ca76450027056e4682b4`, recorded by `phase-6-verified^{}`. The exact `0.1.0-preview.3` release candidate must pass a fresh manual hosted verification before its signed tag is created; the repository `ImplementationPlan.md` records that final evidence.

The declared production dependency remains exact `2.7.6`, making `2.7.6` the supported minimum rather than excluding it. The verified claim is deliberately limited to the two rows above; versions between or beyond them are not claimed merely because Unity can resolve a consumer override. The owner requested a future `4.0.1` compatibility investigation when that version is intentionally prepared and tested; it is not a current support claim or workflow lane.

## Prepared latest lane

The manual experimental workflow tracks the current Unity 6.0 LTS patch `6000.0.82f1` with Addressables `2.11.2`. It is prepared but not yet verified. It is intentionally manual-only, non-recurring, and non-publishing. A passing experimental lane does not replace either required lane without an explicit plan change.

## Version-sensitive behavior

Group and scene synchronization use supported public APIs across the required lanes. Duplicate-dependency **Fix** is deliberately enabled only for exact tested adapters (`2.7.6` and `2.9.1`); other versions remain analyze-only with a blocking capability diagnostic. Content-build preflight records the exact Unity and Addressables versions and rejects incompatible recovery or existing-build receipts.

Unity Package Validation Suite `0.86.0-preview` is a release-readiness tool dependency used only in a disposable validation project. It is not a production package dependency.
