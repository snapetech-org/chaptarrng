# ChaptarrNG PR #190 W561 third-party cross-review

The primary and independent-blind packets both pass their input verifiers and checksum checks at head `c01c3974a01dfcab6a1aa09401c66d412bc88e60` / tree `55ea7696f4981fc56a774f38be477f89e454a7a3`. The exact caller at `src/NzbDrone.Core/Download/Clients/Nzbget/Nzbget.cs:348` rejects `value > 25000`; therefore 25000 itself is allowed. The proposed English “NzbGet setting KeepHistory must not exceed 25000” corrects the existing strict wording.

The current key is `server.message.m453c42aa8de5`; the proposed English text hashes to a separate prospective key, `server.message.m3a79390fb21e`. The current caller still points at the old key. This packet recommends a future source/catalog key transition and 12 locale target corrections; it does not apply them.

Primary and blind target strings agree exactly in 5 locales and differ in 7. The cross ledger records both, the selected final target, and the rationale for every locale. All 12 final targets preserve `NzbGet`, `KeepHistory`, and `25000`. Language confidence is moderate; no native-speaker approval was performed.

No repository or catalog files were edited, and no tests, builds, scans, CI, or head advances occurred. Run `PYTHONDONTWRITEBYTECODE=1 python3 verify_cross_review.py` to verify the packet.
