# Local planning is the master database

## Manual synchronization only

The SQLite database on the user's computer remains authoritative. There is no
automatic save, timer, background upload, or scheduled synchronization. A preview
never changes data; applying a month requires a separate explicit user action.
DDD imports, accounts and company assignments are not changed by the importer.

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
and click **Sprawdź różnice bez zapisywania**.

### Save a month after reviewing collisions

1. In **Ręczny zapis na VPS**, choose the destination company, month and schedule.
   The default source schedule is reused for later imports of the same source/month,
   or created when none exists. Main-only schedules are not shared with company accounts.
2. Fill driver and duty mappings. The proposal button is optional; even unique
   surnames must be reviewed. Missing records are not created automatically.
   Create them separately or explicitly acknowledge a partial import with skipped cells.
3. Acknowledge that this imports cells using the selected VPS duty definitions
   (including their times and work minutes), not the local generator's settings.
4. Click **Przelicz kolizje i przygotuj zapis**. Choose **Keep VPS** or **Use local**
   for each collision. The bulk local selection only covers generated VPS cells;
   overwriting manual VPS entries always requires individual decisions.
5. After changing any mappings or decisions, prepare again. Tick the final review
   checkbox and click **Zapisz na VPS — ręcznie**. Nothing is saved before that click.

There are authenticated `options`, `prepare`, `confirm`, `batches` and
`batches/{id}/revert` routes under `/api/planning/local-sync` in addition to `preview`.
Snapshot POST requests are limited to 16 MiB. `prepare` is read-only. `confirm` uses
a revision token covering the request, target schedule, assignments, drivers and
duty definitions and rechecks it inside a serializable transaction. A changed plan
must be reviewed again. Retrying the same operation ID returns its saved result;
importing the same content again does not duplicate cells or source schedules.

For the current VPS, include `deploy/nginx/drivertime-local-sync.conf` in the
DriverTime HTTPS server blocks and run `nginx -t` before reloading Nginx. This
raises the default outer-proxy body limit only for the three snapshot POST routes; the
container proxy already allows 50 MiB. Back up existing proxy configuration first.

All candidates, target schedules, history, writes and undo operations are scoped
by the authenticated tenant and destination operating company. Company accounts
cannot select another company or import its drivers, unassigned drivers or duties.
Mobile driver accounts cannot use this feature. A driver cannot be mapped twice.

### Audit and undo

An EF Core migration adds `LocalPlanningSyncBatches`. Before-images, after-images,
source request, operation ID, acting user and result are saved atomically with the
assignment changes. No unmentioned cells are deleted. In **Historia ręcznych importów
i cofanie**, a separate manual confirmation restores the previous cells and removes
only cells created by that import. Undo refuses any later change or tenant/scope
change. The schedule and audit history are retained, even when undo leaves it empty.
There is no automatic rollback or background application of a snapshot.

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

## Remaining limitations

1. Missing drivers and duties must currently be created separately, then mapped
   by the user. Import does not merge identities or reassign company membership.
2. Add missing planning semantics before claiming full settings synchronization: separate Sunday exclusions, monthly
   start shifts, monthly exclusions, dated pairs, vehicle assignments, and vacation
   shift continuity. Do not silently discard unsupported settings.
3. Import copies the reviewed plan, including historical assignments; it does not
   rerun the generator or certify legal compliance. Use existing planning validation
   to review rest constraints, staffing and work-hour totals after importing.
4. Publishing is computer-to-VPS, manually. Web edits are not written back to SQLite.
   They must be reconciled manually with the local master. When the computer is offline,
   the VPS keeps its last manually saved version.
