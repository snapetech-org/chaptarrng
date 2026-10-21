# BookshelfNG Helm handoff chart

This chart keeps the existing BookshelfNG chart identity and Kubernetes
resource names while replacing its container with ChaptarrNG. It is a
**source-only retirement candidate**. There is no published ChaptarrNG Helm
chart or approved production cutover yet. ChaptarrNG releases and compatibility
bridges remain frozen until representative data, migration and rollback,
feature parity, installed channel handoff, and operator acceptance are complete.

The chart name stays `bookshelfng`. With the same Helm release name and
namespace, it keeps the StatefulSet, Service, PVC, selector labels, service
account behavior, `/config` mount, and port `8787`. The existing PVC remains
named `<release>-config` under the default naming rules. Keep the existing
release name, namespace, and values when a cutover is approved.

## Safety requirements

Rendering fails closed unless all of these are supplied:

- `bookshelfMigration.confirmed=true`, set only after the retirement gates are
  approved;
- `bookshelfMigration.providerMode=hardcover` or `softcover`, matching the
  source BookshelfNG provider mode; and
- `image.digest=sha256:<64 lowercase hexadecimal characters>`, pointing to
  the approved, provider-compatible ChaptarrNG image. A private image mirror
  also needs the explicit `image.allowCustomRepository=true` setting.

There is no mutable image tag default. The chart also rejects more than one
replica while the source SQLite database is migrated. Replica count zero is
allowed for a deliberate service stop.

Do not use this candidate against live BookshelfNG data. The published
ChaptarrNG `v0.9.977` image has known migration limits and is not an approved
cutover target. The chart does not make the migration or its rollback
operationally accepted.

## Future cutover shape

After the release and channel gates close, use the versioned chart published to
the existing `oci://ghcr.io/snapetech/bookshelfng/bookshelfng` identity. Keep
the existing release name and namespace.
First record the release and its values, stop BookshelfNG, take an independent
backup or storage snapshot, and complete the approved migration preview and
rollback rehearsal on a copy. Confirm the source provider mode and obtain the
matching immutable image digest from the approved release evidence.

The in-place upgrade will have this form; replace the placeholders with the
existing release details and the approved provider-specific digest:

```sh
helm upgrade bookshelfng oci://ghcr.io/snapetech/bookshelfng/bookshelfng \
  --version <approved-chaptarrng-version> \
  --namespace <existing-namespace> \
  --reuse-values \
  --set-string image.repository=ghcr.io/snapetech-org/chaptarrng \
  --set-string image.digest=sha256:<approved-64-character-digest> \
  --set bookshelfMigration.confirmed=true \
  --set bookshelfMigration.providerMode=hardcover
```

Use `softcover` only for a source configured for the Goodreads provider, and
select the matching image digest. With `--reuse-values`, explicitly override
both `image.repository` and `image.digest`; older chart values otherwise retain
the legacy image repository. Review the rendered manifests before applying.

This chart source lives in the ChaptarrNG repository. Its release workflow has
a publisher for the existing OCI identity, gated by both
`CHAPTARRNG_RELEASE_READY` and `BOOKSHELFNG_RELEASE_READY`, as well as
`CHAPTARRNG_MIGRATION_BRIDGE_ENABLED` and an explicit bridge request. No chart
artifact is published while the retirement freeze is active. Once the gates
close, existing installations can upgrade the same chart source and preserve
their release name, namespace, and PVC; pinned/offline chart archives still
need a manual chart and image update.

## Local validation

With Helm 3.20.0 and Ruby available, run:

```sh
helm lint charts/bookshelfng \
  --set bookshelfMigration.confirmed=true \
  --set bookshelfMigration.providerMode=hardcover \
  --set-string image.digest=sha256:0000000000000000000000000000000000000000000000000000000000000000
ruby scripts/test_bookshelfng_helm_handoff.rb
```

The test renders with the legacy release name and verifies the workload,
selector, service, PVC, image digest, and persistent mount identities. It also
checks that missing approval, provider mode, or a valid image digest fails.
