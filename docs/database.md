DriverTime Database Design

Overview

DriverTime uses PostgreSQL as the primary database.

The database is designed for:

* Multi-company support
* DDD file processing
* Driver activity storage
* Violation analysis
* Reporting
* Audit logging

⸻

companies

Stores transport companies.

Columns:

* id
* name
* vat_number
* address
* active
* created_at

⸻

company_settings

Stores company-specific settings.

Columns:

* id
* company_id
* timezone
* country
* language
* report_settings

⸻

users

Stores system users.

Columns:

* id
* company_id
* email
* password_hash
* role
* active
* created_at

⸻

drivers

Stores drivers.

Columns:

* id
* company_id
* first_name
* last_name
* driver_card_number
* driving_license_number
* active
* created_at

⸻

driver_cards

Stores driver card history.

Columns:

* id
* driver_id
* card_number
* issue_date
* expiry_date
* status

⸻

vehicles

Stores vehicles.

Columns:

* id
* company_id
* registration_number
* vin
* active
* created_at

⸻

tachographs

Stores tachograph devices.

Columns:

* id
* vehicle_id
* serial_number
* manufacturer
* calibration_date
* next_calibration_date

⸻

driver_documents

Stores driver documents.

Columns:

* id
* driver_id
* document_type
* document_number
* valid_from
* valid_to

⸻

import_files

Stores uploaded DDD files.

Columns:

* id
* company_id
* original_file_name
* stored_file_name
* file_size
* status
* uploaded_at

Status Values:

* Pending
* Processing
* Completed
* Failed

⸻

driver_activities

Stores normalized activities extracted from DDD files.

Columns:

* id
* driver_id
* vehicle_id
* start_time
* end_time
* activity_type
* source_file_id

Activity Types:

* Driving
* Break
* Rest
* Availability
* Work

⸻

violations

Stores detected violations.

Columns:

* id
* driver_id
* violation_type
* regulation_reference
* severity
* duration_minutes
* violation_start
* violation_end
* calculated_at

Severity Values:

* Low
* Medium
* High
* Critical

⸻

notifications

Stores system notifications.

Columns:

* id
* company_id
* title
* message
* is_read
* created_at

⸻

audit_log

Stores audit history.

Columns:

* id
* user_id
* action_type
* entity_name
* entity_id
* created_at

⸻

Relationships

Company

* Company → Users
* Company → Drivers
* Company → Vehicles
* Company → Import Files
* Company → Notifications

Driver

* Driver → Driver Cards
* Driver → Driver Documents
* Driver → Driver Activities
* Driver → Violations

Vehicle

* Vehicle → Tachographs
* Vehicle → Driver Activities

Import File

* Import File → Driver Activities

User

* User → Audit Log

⸻

Future Tables

Planned for future releases:

* gps_positions
* fleet_dashboard_metrics
* report_templates
* report_exports
* mobile_devices
* driver_portal_accounts