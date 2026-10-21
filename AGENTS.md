# ChaptarrNG build notes

## Six Labors license — check these paths first

Before diagnosing, retrying, or asking Keith about a ChaptarrNG build that needs the Six Labors license, check these confirmed local paths first:

- The license key is recorded in the gitignored repository-root file `.env.sixlabors`. Check that the file exists without printing or copying its contents.
- The license file exists at `/home/keith/Documents/code/chaptarrng/sixlabors.lic` and `/home/keith/Downloads/sixlabors.lic`.
- For local builds, pass `-p:SixLaborsLicenseFile=/home/keith/Documents/code/chaptarrng/sixlabors.lic`; use the Downloads copy if the repository copy is unavailable.

Do not print or expose the key, add `.env.sixlabors` or either `.lic` file to Git, or ask Keith to locate these files unless both license-file paths have been checked and are actually unavailable.
