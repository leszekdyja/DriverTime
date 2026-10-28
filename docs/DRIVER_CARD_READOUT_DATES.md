# Driver card readout dates

`DddFiles.CardReadAtUtc` stores the readout date separately from activity coverage
and `UploadedAtUtc`. `CardReadDateSource` records `CardDownload` or
`OriginalFileName`. The additive EF migration is `AddDriverCardReadDate`.

The parser reads EF 050E / LastCardDownload as a four-byte TimeReal from generation
00/02 data blocks, never signature blocks. See the [JRC interoperability file
table](https://dtc.jrc.ec.europa.eu/iot_doc/2022-04-08%20Smart%20Tachograph%20Interoperability%20Test%20Specification%20Version%201.10.pdf)
and the [LastCardDownload definition](https://www.legislation.gov.uk/eur/2016/799/pdfs/eur_20160799_2020-02-26_en.pdf).
This is a date recorded on the card, not cryptographic verification of a download.

When that field is absent, the resolver supports the original filename's
`yyyyMMddHHmmss` or `yyyyMMdd_HHmmss` convention. These filename times are interpreted
as Polish local time and displayed explicitly as filename-derived dates. Invalid,
future and DST-ambiguous/nonexistent dates are rejected. Upload timestamps and
filesystem modification times are not accepted as readout evidence. Reuploading
the same file therefore does not refresh its readout deadline.

Older imports with no persisted date can use their original filename without
rewriting history or reimporting. If neither source is available, the previous
activity-based estimate remains, clearly labelled as an estimate. A filename date
is not a verified card download: renaming a file cannot establish authenticity.

The download schedule selects the latest accepted readout for the current card
only, never another driver's/company's card or an old replacement card. Existing
confirmed-expired-replacement filtering is retained. Alerts and dashboard counters
use the same schedule service. This does not alter activity dates, violations,
historical imports or saved schedules.

Dashboard expiry alerts use the same backend-filtered active card IDs, so an
expired historical card cannot still produce a separate card-expiry warning.
If the active-card request fails, expiry alerts remain visible rather than being
silently suppressed.
