# Setup experience contract

Keep ChaptarrNG setup inside its existing **System → Quickstart** sections and
provider forms. This is an ebook and audiobook manager, so its setup asks about
those formats and the services needed to acquire or organize them. It does not
reuse another project's setup questions or add a separate wizard or settings
store.

## ChaptarrNG-specific decisions

- Format scope is selected for each new root folder: ebooks, audiobooks, or
  both. Existing roots keep their saved scope and path.
- An indexer and download client are suggested when their existing settings
  show they are missing. Both remain optional for people who organize existing
  files or import them manually.
- AudioBookShelf is offered for audiobook workflows. Grimmory and Hardcover
  remain available for ebook and audiobook workflows. Other optional
  integrations remain governed by their existing provider forms and support.
- Device-format questions only belong here when ChaptarrNG can apply the answer
  through an existing reader or profile setting.
- The setup summary reads current root, indexer, download-client, and provider
  state. It reports the saved configuration and current-session connection-test
  results without copying secrets into a second store.

## Implemented in this change

- [x] **C1 — P1: Start guided mode for missing core setup.** Readiness uses the
  loaded root-folder, enabled-indexer, enabled-download-client, and skip state.
  Quickstart opens on the first missing required-for-grabbing section and
  collapses sections already configured or deliberately skipped.
- [x] **C2 — P1: Make root format explicit.** The existing root-folder section
  asks which formats a new root serves and uses the current folder-type fields
  when opening the existing root editor. Separate roots can serve different
  formats without changing existing roots.
- [x] **C3 — P2: Branch supported optional connections and summarize setup.**
  AudioBookShelf appears for audiobook roots, while existing shared book
  connections remain available for either format. Connection forms continue
  to use their existing schemas, save actions, and tests. A final summary shows
  saved formats, roots, enabled indexers and clients, and relevant optional
  connection states. Optional connections can be skipped.

## Validation boundary

Guided setup is a convenience over ChaptarrNG's existing configuration model.
It must not guess formats from files, rewrite root paths, change matching or
profile settings without an explicit save, or claim an integration works before
its existing connection test succeeds. A provider error remains visible in its
existing form while the operator is in Quickstart.
