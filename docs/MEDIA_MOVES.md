# Moving media between author folders

ChaptarrNG can move registered ebook or audiobook files when an author’s
format-specific folder changes. Use the application workflow or the scripts
below. Both routes preview the operation first, check conflicts and copy space,
and require the server-issued preview token before queueing a move.

## Command-line scripts

The scripts require Bash, curl, and jq. Use the ChaptarrNG URL and an
administrator API key. They print the server preview and ask before queueing;
passing --yes skips only the prompt. The server still rejects an unsafe preview.

Move one author’s files:

    scripts/chaptarr-move-media.sh --url http://localhost:8787 \
      --api-key "$CHAPTARR_API_KEY" --author-id 42 \
      --format audiobook --destination /media/audiobooks/Author

Move a selected set of authors:

    scripts/chaptarr-bulk-move-media.sh --url http://localhost:8787 \
      --api-key "$CHAPTARR_API_KEY" --author-ids 42,57 \
      --format ebook --destination-root /media/ebooks

To include every author, replace --author-ids with --all. The script reports a
command ID after queueing; check its state through GET /api/v1/command/{id}.
The move worker handles registered media and linked sidecars. It leaves
untracked files in place.

## API flow

For one author, POST to /api/v1/author/media-move/preview and then
/api/v1/author/media-move/start. For a batch, use
/api/v1/author/media-move/bulk/preview and
/api/v1/author/media-move/bulk/start. Send the same request body to preview and
start, adding the returned previewToken to the start request. The server
recalculates the preview and refuses a stale token.

Keep BookshelfNG running until migration, rollback, and installed-channel
acceptance are complete. The media-move workflow does not replace those
retirement checks.
