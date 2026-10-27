# Local planning is the master database

## Implemented: read-only preview (phase 1)

The SQLite database on the user's computer remains authoritative. The preview does
not import, delete, update, or store planning data on the VPS. Existing DDD imports,
accounts, company assignments, and locally edited planner code are unaffected.

Create an export with the standard Python installation:

```powershell
python tools/export_local_planning.py --output C:\Users\48602\Desktop\DriverTime-planning-preview.json
```

The default source is `%APPDATA%\PlanowanieSluzb\sluzby_v30.sqlite`.
`--database` selects another database. The exporter opens it with `mode=ro` and
`PRAGMA query_only=ON`, and reads all tables inside one SQLite read transaction.
The output must be a new file: an existing file, including the source database,
cannot be overwritten.

In **Planowanie → Synchronizacja z komputerem — podgląd**, select the exported JSON
and click **Sprawdź różnice bez zapisywania**. The API endpoint is
`POST /api/planning/local-sync/preview` (authenticated, maximum 16 MiB).

For the current VPS, include `deploy/nginx/drivertime-local-sync.conf` in the
DriverTime HTTPS server blocks and run `nginx -t` before reloading Nginx. This
raises the default outer-proxy body limit only for the exact preview route; the
container proxy already allows 50 MiB. Back up existing proxy configuration first.

The report includes counts, months, proposed driver/duty matches, possible overlaps
with server assignments, and counts of exported settings. Driver matches are only
suggestions, even for a unique surname. No driver is created or merged. Duty codes
with several server versions remain ambiguous. All candidate and overlap queries
are scoped by the authenticated tenant and operating company; mobile driver
accounts cannot use this feature.

The source ID is stable for the same computer and database path. Record it from the
export if the master database is moved; pass the same UUID using `--source-id` on
the new computer. Do not treat a relocated database as a new synchronization source.

## Preserved data

The export keeps all columns of drivers, duties, assignments, cars, monthly and
permanent pairs, RN pairs, duty blocks, exclusions, fixed duties, monthly shift
preferences, weekend overrides, and the original schedule. In particular it keeps
manual/automatic provenance, vehicle and pair references, notes, and absence codes.
It excludes backup tables, compliance calculations, accounts, and unrelated tables.
Exporting settings does **not** mean the web generator already supports every setting.

## Required before enabling writes (not implemented yet)

1. Persist source/row mappings, import batches and revisions through EF Core migrations.
   Confirm ambiguous drivers and choose the target tenant/operating company and schedule.
   Never reassign an existing driver's company implicitly or match identity only by name.
2. Add missing planning semantics, including separate Sunday exclusions, monthly
   start shifts, monthly exclusions, dated pairs, vehicle assignments, and vacation
   shift continuity. Do not silently discard unsupported settings.
3. Add transactional, idempotent snapshot application with a preview revision token,
   explicit conflict resolution and an audit trail. Never delete unrelated server
   records or silently overwrite manually edited cells. Retain a backup before writes.
4. Verify RN blocks across months, required staffing, manual cells, rest constraints,
   work-hour totals, pairs, and excluded drivers on a database copy first.
5. Start with computer-to-VPS publishing. The VPS shows the last synchronized version
   when the computer is offline. Web edits must remain proposals until accepted by
   the local master; two independent editable masters would create conflicts.

This phase intentionally provides no confirm/apply endpoint and no synchronization
scheduler. Publishing to production requires the mappings and conflict policy above.
