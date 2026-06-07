DriverTime Architecture

Project Goal

DriverTime is a platform for transport companies that manages drivers, vehicles, tachograph data and compliance with EU regulations.

The system allows:

* Driver management
* Vehicle management
* Driver document management
* DDD file import
* Activity analysis
* Violation detection
* PDF and Excel reporting
* Fleet monitoring

⸻

System Architecture

DriverTime follows a Clean Architecture approach.

Layers

API Layer

Responsibilities:

* REST API endpoints
* Authentication
* Authorization
* Request validation

Technology:

* ASP.NET Core Web API

⸻

Application Layer

Responsibilities:

* Business use cases
* Commands and Queries
* DTOs
* Validation rules

Examples:

* Create Driver
* Import DDD File
* Generate Report
* Detect Violations

⸻

Domain Layer

Responsibilities:

* Domain entities
* Business rules
* Domain services

Entities:

* Company
* User
* Driver
* Vehicle
* DriverDocument
* ImportFile
* DriverActivity
* Violation
* Notification

⸻

Infrastructure Layer

Responsibilities:

* Database access
* External integrations
* File storage
* Reporting services

Technology:

* Entity Framework Core
* PostgreSQL

⸻

Frontend Architecture

Technology:

* React
* TypeScript
* Material UI

Modules:

* Dashboard
* Drivers
* Vehicles
* Documents
* DDD Import
* Violations
* Reports
* Administration

⸻

Security

Authentication:

* JWT Tokens

Authorization:

* Role-based access control

Roles:

* System Administrator
* Company Administrator
* Dispatcher
* Viewer

Password Security:

* BCrypt password hashing

⸻

Multi-Tenant Architecture

DriverTime supports multiple transport companies.

Rules:

* Every business entity contains CompanyId
* Data is isolated between companies
* Users can only access data from their company

⸻

DDD Import Module

Responsibilities:

* Upload DDD files
* Validate file format
* Store original files
* Extract tachograph data
* Normalize activities

Output:

* Driver Activities
* Driving Times
* Rest Periods
* Availability
* Work Activities

⸻

Violation Engine

Responsibilities:

* Detect violations of Regulation (EC) 561/2006
* Detect violations of Regulation (EU) 165/2014

Examples:

* Driving over 4h30 without break
* Exceeding daily driving time
* Exceeding weekly driving time
* Reduced daily rest
* Missing weekly rest

⸻

Reporting Module

Supported Formats:

* PDF
* Excel

Reports:

* Driver Activity Report
* Violations Report
* Driver Summary
* Fleet Summary

⸻

Database

Technology:

* PostgreSQL

Main Tables:

* companies
* users
* drivers
* vehicles
* driver_documents
* import_files
* driver_activities
* violations
* notifications
* audit_log

⸻

Future Modules

* Fleet Dashboard
* Real-time GPS Integration
* Mobile Application
* Driver Portal
* Automated Notifications
* AI-based Compliance Assistant