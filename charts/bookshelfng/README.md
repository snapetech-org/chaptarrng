# BookshelfNG Helm handoff chart

This chart keeps the existing BookshelfNG chart identity and Kubernetes
resource names while replacing its container with ChaptarrNG. It is a
**source-only retirement candidate**. There is no published ChaptarrNG Helm
chart, and a production cutover is not ready yet. ChaptarrNG releases and
compatibility bridges remain frozen until representative data, migration and
rollback, feature parity, and installed channel handoff pass technical checks.
Maintainer/operator sign-off is recorded separately and does not close these
readiness gates.

The chart name stays `bookshelfng`. With the same Helm release name and
namespace, it keeps the StatefulSet, Service, PVC, selector labels, service
account behavior, `/config` mount, and port `8787`. The existing PVC remains
named `<release>-config` under the default naming rules. Keep the existing
release name, namespace, and values for an in-place cutover.

## Safety requirements

Rendering fails closed unless all of these are supplied:

- `bookshelfMigration.confirmed=true`, set after migration and channel checks
  pass for the target deployment;
- `bookshelfMigration.providerMode=hardcover` or `softcover`, matching the
  source BookshelfNG provider mode; and
- `image.digest=sha256:<64 lowercase hexadecimal characters>`, pointing to
  a validated, provider-compatible ChaptarrNG image. A private image mirror
  also needs the explicit `image.allowCustomRepository=true` setting.

There is no mutable image tag default. The chart also rejects more than one
replica while the source SQLite database is migrated. Replica count zero is
allowed for a deliberate service stop.

Do not use this candidate against live BookshelfNG data. The published
ChaptarrNG `v0.9.977` image has known migration limits and is not ready for
this cutover. The chart does not run migration or validate rollback against
the target data; complete those checks separately.

## Future cutover shape

After the release and channel gates close, use the versioned chart published to
the existing `oci://ghcr.io/snapetech/bookshelfng/bookshelfng` identity. Keep
the existing release name and namespace.
First record the release and its values, stop BookshelfNG, take an independent
backup or storage snapshot, and complete the migration preview and
rollback rehearsal on a copy. Confirm the source provider mode and obtain the
matching immutable image digest from release evidence.

The in-place upgrade will have this form; replace the placeholders with the
existing release details and the validated provider-specific digest:

```sh
helm upgrade bookshelfng oci://ghcr.io/snapetech/bookshelfng/bookshelfng \
  --version <chaptarrng-version> \
  --namespace <existing-namespace> \
  --reuse-values \
  --set-string image.repository=ghcr.io/snapetech-org/chaptarrng \
  --set-string image.digest=sha256:<validated-64-character-digest> \
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
mkdir -p /tmp/bookshelfng-reference
git -C /path/to/bookshelfng archive adea130aeea2b6ea29c28a6ea8bf4706169d15e4 charts/bookshelfng \
  | tar -x -C /tmp/bookshelfng-reference
BOOKSHELFNG_REFERENCE_CHART=/tmp/bookshelfng-reference/charts/bookshelfng \
  ruby scripts/test_bookshelfng_helm_handoff.rb
helm lint charts/bookshelfng \
  --set bookshelfMigration.confirmed=true \
  --set bookshelfMigration.providerMode=hardcover \
  --set-string image.digest=sha256:0000000000000000000000000000000000000000000000000000000000000000
```

The test requires the pinned BookshelfNG reference chart, renders both charts
with the same legacy release values, and compares every rendered resource
after normalizing only the app image and chart-version labels. It covers the
default release and retained ingress, service-account, persistence, resource,
node-selection, pod-label, and secret-backed environment settings. It also
checks that missing operator confirmation, provider mode, or a valid image digest fails.
