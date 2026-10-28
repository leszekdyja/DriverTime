# Global settings in the local planner

Version v511 uses the existing November 2026 configuration as the shared settings
source. The `global_planning_settings_v511` app metadata entry records `2026-11`.
All reads and writes of excluded drivers, excluded duties and fixed I/II duties
use that source month, regardless of the month selected in the interface. Older
monthly configuration rows remain in the database, but do not override the shared
settings. Existing global vehicle/driver pairs, RN pairs, blocks, driver day
restrictions, no-RN flags and duty definitions remain global and unchanged.

Shift preferences use November as an anchor and retain the existing monthly
alternation. Editing a shift while viewing another month converts the selected
shift back to the shared anchor. Selecting Auto removes that shared preference;
older monthly records cannot bring it back.

On first activation, the program saves a consistent SQLite backup under the
database's `backups` directory (`before_global_settings_*.sqlite`). Legacy global
duty exclusions are copied into November once. Clearing one afterwards is
permanent, including after restart. No other monthly settings or saved schedule
cells are deleted by activation. Dated manual entries, sickness, leave and weekend
overrides are not copied into other months. Already created schedules are not
regenerated: the new shared configuration is used when the user plans again.

The normal export includes an explicit `settingsPolicy` with the source month;
it still preserves all source setting rows and remembered mappings. Exporting and
VPS synchronization remain manual. This change does not imply that the web
generator implements every local setting.
