---
category: changed
audience: users, operators
area: migration
action: none
breaking: false
---
BookshelfNG's numeric `BookImportMinimumMatchPercent` is retained during conversion and mapped to the closest ChaptarrNG matching mode when no explicit mode exists: 50–66 to Aggressive, 67–82 to Balanced, and 83–99 to Strict. The migration report names the mapping, and invalid legacy values stop conversion. This is a lossy policy translation because the two applications use different matching algorithms.
