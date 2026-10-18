---
category: added
audience: users, operators
area: metadata
action: Enable supplemental catalogs and choose a source for selected metadata fields in Settings > Metadata.
breaking: false
---

ChaptarrNG can search eight optional catalogs alongside its primary source. Provider IDs remain catalog-qualified, API keys are write-only, and field preferences use matching evidence. This feature remains unpublished during the BookshelfNG retirement freeze.

Retrying unmapped files can also search the configured catalogs after local and V5 matching fail. The file must provide an exact ISBN or ASIN, or an exact normalized title and author; one unique provider work identity is queued for review through the existing pending-author import flow. Catalog results never attach files directly, and ambiguous results stay unmatched.
