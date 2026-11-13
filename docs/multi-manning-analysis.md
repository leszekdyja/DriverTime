# Multi-manning analysis

The card parser preserves `is_crew` and `card_slot` from Annex IC
`ActivityChangeInfo`. Card slot 1 is the driver; slot 2 is the co-driver.
When the card is withdrawn, the c bit describes a known manual entry, not
crew status. Such entries and old imports therefore have nullable crew metadata.

The compliance engine grants the 30-hour daily-rest window only when the
observed duty confirms crew operation, including the first-hour exception.
A continuous daily rest of at least 9 hours must fit inside that window.
Availability does not constitute daily rest. A single crew marker does not
grant the exception to an entire duty. Missing or conflicting evidence must
not be presented as a confirmed crew-rest infringement.

A consecutive 45-minute period of crew co-driver availability is accepted as
a driving break using the enforcement practice described by the Commission.
This assumes the co-driver is not assisting the driver; the card alone cannot
prove that condition. Short availability periods are not automatically combined
as 15+30 breaks. Ordinary rest breaks can still be split 15+30.
The individual daily, weekly and fortnightly driving limits remain unchanged.

The driver-violation endpoint and compliance runs use the same driving and
daily-rest rules. The timeline shows crew/slot evidence and uses the same
availability distinction in its illustrative driving counter.

Migration `AddDriverActivityCrewStatus` only adds two nullable columns. It
does not reconstruct crew status or rewrite historical activities. Reuploading
identical DDD bytes can enrich missing crew metadata on exact time/type matches
and recalculate compliance, without duplicating the import. Different boundaries,
conflicting evidence or missing originals require separate review; never infer
crew status from availability alone.

References:

- [Regulation 561/2006, Articles 4(o), 7, 8(5)](https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:02006R0561-20241231)
- [Commission Q&A, section VI](https://transport.ec.europa.eu/transport-modes/road/mobility-package-i/driving-rest-times_en)
- [ActivityChangeInfo, Regulation 2016/799](https://dtc.jrc.ec.europa.eu/iot_doc/EU%202016-799-EN.pdf)
- [TRACE II enforcement guidance, Article 8](https://transport.ec.europa.eu/system/files/2023-12/TRACE_II_Driving-Resting_Times_Enforcement_Guidance.pdf)
