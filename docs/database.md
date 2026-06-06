DriverTime Database Design

Core Tables

companies

Stores transport companies.

Columns:

* id
* name
* vat_number
* address
* active

users

Stores system users.

Columns:

* id
* company_id
* email
* password_hash
* role
* active

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

vehicles

Stores vehicles.

Columns:

* id
* company_id
* registration_number
* vin
* tachograph_serial
* active

driver_documents

Stores driver documents.

Columns:

* id
* driver_id
* document_type
* document_number
* valid_from
* valid_to

import_files

Stores uploaded DDD files.

Columns:

* id
* company_id
* original_file_name
* stored_file_name
* file_size
* status

driver_activities

Stores normalized activities from DDD files.

Columns:

* id
* driver_id
* vehicle_id
* start_time
* end_time
* activity_type

violations

Stores detected violations.

Columns:

* id
* driver_id
* code
* severity
* violation_start
* violation_end

notifications

Stores user notifications.

Columns:

* id
* company_id
* title
* message
* created_at

audit_log

Stores system audit history.

Columns:

* id
* user_id
* action_type
* entity_name
* entity_id
* created_at