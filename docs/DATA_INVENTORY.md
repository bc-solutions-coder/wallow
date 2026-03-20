# Data Inventory Documentation

This document provides a comprehensive inventory of personal data collected and processed by Wallow. It serves as both internal documentation and transparency documentation for users exercising their rights under GDPR, CCPA, and similar data protection regulations.

## Overview

Wallow collects and processes personal data to provide its services to tenants and their users. All data processing is done with appropriate legal basis and in accordance with applicable data protection laws.

### Regulatory Framework

- **GDPR (General Data Protection Regulation)** - EU regulation covering EU residents
- **CCPA (California Consumer Privacy Act)** - California law covering California residents

### User Rights

Under GDPR and CCPA, users have the following rights:

| Right | GDPR Article | CCPA Equivalent | Implementation |
|-------|--------------|-----------------|----------------|
| Right to Access | Art. 15 | Right to Know | Administrative API |
| Right to Data Portability | Art. 20 | Right to Know | Administrative API |
| Right to Erasure | Art. 17 | Right to Delete | Administrative API |
| Right to Rectification | Art. 16 | - | Standard update APIs |
| Right to Opt-Out | - | Opt-out of Sale | Administrative API |

## Data Categories

Personal data is organized into the following categories based on the modules that collect it:

### Identity Data

Information used for account authentication and user identification.

| Data Element | Description | Storage Location | Purpose | Legal Basis | Sensitive | Retention |
|-------------|-------------|------------------|---------|-------------|-----------|-----------|
| User Email | Email address for authentication and communication | Keycloak | Account authentication and contact | Contractual necessity | No | 7 years (2555 days) |
| User Name | Full name of the user | Keycloak | User identification and personalization | Contractual necessity | No | 7 years (2555 days) |
| User Password | Hashed password | Keycloak | Account authentication | Contractual necessity | Yes | 7 years (2555 days) |
| User ID | Unique identifier | Keycloak | System reference | Contractual necessity | No | 7 years (2555 days) |
| Service Account Metadata | Service account configuration | `identity.service_account_metadata` table | API access management | Contractual necessity | No | Until deletion |
| SSO Configuration | Single sign-on settings | `identity.sso_configurations` table | Enterprise authentication | Contractual necessity | No | Until deletion |
| SCIM Configuration | User provisioning settings | `identity.scim_configurations` table | Enterprise user sync | Contractual necessity | No | Until deletion |
| SCIM Sync Logs | Provisioning sync history | `identity.scim_sync_logs` table | Audit and debugging | Legitimate interest | No | 90 days |
| API Scopes | API access permissions | `identity.api_scopes` table | Authorization | Contractual necessity | No | Until deletion |

**Processing Activities:**
- Account creation and management
- Authentication and authorization
- User profile display
- Communication with users

**Third-Party Sharing:** Keycloak (self-hosted identity provider)

**Note:** The Identity module stores no user/organization data locally. User accounts, organizations, roles, and permissions all reside in Keycloak. The `identity` schema contains only service account metadata, SSO/SCIM configurations, and API scopes.

### Billing Data

Information used for payment processing and invoicing.

| Data Element | Description | Storage Location | Purpose | Legal Basis | Sensitive | Retention |
|-------------|-------------|------------------|---------|-------------|-----------|-----------|
| Invoice Data | Invoice records with status, amounts, due dates | `billing.invoices` table | Billing and tax compliance | Legal obligation | No | 7 years (2555 days) |
| Invoice Line Items | Individual line items on invoices | `billing.invoice_line_items` table | Itemized billing | Legal obligation | No | 7 years (2555 days) |
| Payment Records | Payment transactions with method, status, amount | `billing.payments` table | Process payments for services | Contractual necessity | Yes | 7 years (2555 days) |
| Subscription Data | User subscriptions with plan, status, billing periods | `billing.subscriptions` table | Subscription management | Contractual necessity | No | 7 years (2555 days) |

**Processing Activities:**
- Payment processing
- Subscription management
- Invoice generation
- Tax reporting
- Refund processing

**Third-Party Sharing:** Payment processors (if integrated)

**Note:** Billing data has extended retention (7 years) due to tax and accounting legal requirements. There is no `billing.customers` table; billing addresses would be stored as custom fields on invoices. The module supports custom fields via the `IHasCustomFields` interface on Invoice, Payment, and Subscription entities.

### Audit Log Data

Security-focused audit logs for compliance and security monitoring.

| Data Element | Description | Storage Location | Purpose | Legal Basis | Sensitive | Retention |
|-------------|-------------|------------------|---------|-------------|-----------|-----------|
| Audit Entries | Security audit logs | `audit.audit_entries` table | Security monitoring and compliance | Legal obligation | No | 7 years (2555 days) |
| User Info | User ID, email | `audit.audit_entries` table | Attribution | Legal obligation | No | 7 years (2555 days) |
| Request Context | IP address, user agent, request ID, endpoint | `audit.audit_entries` table | Security forensics | Legitimate interest | No | 7 years (2555 days) |
| Session Data | Session tokens and metadata | In-memory cache, Valkey | Session management | Contractual necessity | No | Session duration |

**Processing Activities:**
- Security monitoring
- Fraud detection
- Compliance audits
- Debugging and troubleshooting

### Notifications Data

In-app notification delivery.

| Data Element | Description | Storage Location | Purpose | Legal Basis | Sensitive | Retention |
|-------------|-------------|------------------|---------|-------------|-----------|-----------|
| Notifications | In-app notification records | `notifications.notifications` table | Deliver notifications to users | Legitimate interest | No | 30 days |
| Notification Content | Title, message, type | `notifications.notifications` table | User communication | Legitimate interest | No | 30 days |
| Read Status | IsRead flag and ReadAt timestamp | `notifications.notifications` table | Track notification state | Legitimate interest | No | 30 days |
| Email Messages | Records of emails with status and content | `notifications.email_messages` table | Audit trail and delivery confirmation | Legitimate interest | No | 90 days |
| Email Preferences | Per-user email opt-in/opt-out by notification type | `notifications.email_preferences` table | Respect user communication preferences | Consent | No | Until user deletion |

**Processing Activities:**
- Sending in-app notifications and real-time delivery via SignalR
- Transactional emails (password reset, account confirmation)
- Notification emails (based on user preferences)
- Retry handling with exponential backoff

**Third-Party Sharing:** SMTP provider (Mailpit in development, configurable SMTP in production)

**Note:** Email preferences are checked before every send to respect user opt-out settings.

### Announcements Data

System announcements and changelog.

| Data Element | Description | Storage Location | Purpose | Legal Basis | Sensitive | Retention |
|-------------|-------------|------------------|---------|-------------|-----------|-----------|
| Announcements | System announcements with targeting | `announcements.announcements` table | System announcements | Legitimate interest | No | Until deletion |
| Announcement Dismissals | Per-user dismissal records | `announcements.announcement_dismissals` table | Track user dismissals | Legitimate interest | No | Until deletion |
| Changelog Entries | Version changelog with items | `announcements.changelog_entries` table | Product changelog | Legitimate interest | No | Until deletion |

**Processing Activities:**
- System announcements and changelog management

### Storage Data

File storage management.

| Data Element | Description | Storage Location | Purpose | Legal Basis | Sensitive | Retention |
|-------------|-------------|------------------|---------|-------------|-----------|-----------|
| Storage Buckets | Bucket configurations | `storage.storage_buckets` table | File organization | Contractual necessity | No | Until deletion |
| Stored Files | File metadata (name, type, size, path) | `storage.stored_files` table | File management | Contractual necessity | No | Until deletion |
| Uploader Info | UploadedBy user ID | `storage.stored_files` table | Attribution | Contractual necessity | No | Until deletion |

**Processing Activities:**
- File upload and storage
- Access control (public/private files)
- Presigned URL generation

## Data Processing Principles

### Data Minimization

Wallow collects only the data necessary to provide its services. Users are not required to provide optional information.

### Purpose Limitation

Data is only used for the purposes specified in this inventory and communicated to users at the time of collection.

### Storage Limitation

Data is retained only as long as necessary for its stated purpose or as required by law. Automated retention policies enforce deletion or anonymization.

### Security Measures

All personal data is protected with:
- Encryption in transit (TLS 1.3)
- Encryption at rest (database-level encryption)
- Access controls (role-based permissions)
- Audit logging (all access to personal data is logged)
- Regular security updates and patches

## Retention Policies

Wallow implements automated retention policies to ensure data is not kept longer than necessary.

| Data Category | Retention Period | Action After Retention | Legal Basis |
|---------------|------------------|------------------------|-------------|
| Security Audit Logs (`audit` schema) | 7 years | Archive | Compliance requirements |
| Invoices & Billing Records | 7 years | Archive | Tax and accounting requirements |
| In-App Notifications (`notifications` schema) | 30 days | Delete | No legal requirement; 30 days sufficient for user reference |
| Email Messages (`notifications` schema) | 90 days | Delete | Sufficient for delivery tracking |
| Session & Login Logs | 1 year | Anonymize | Security monitoring; anonymize after 1 year |
| SCIM Sync Logs | 90 days | Delete | Sufficient for debugging |
| User Data (general) | 7 years after account closure | Delete | Contractual necessity ends at account closure |

**Retention Enforcement:** Automated background job runs daily to enforce retention policies.

## Data Exports

Users can request a complete export of their personal data. Exports include:

- All personal data elements listed in this inventory
- Metadata about data collection (when, how, why)
- This data inventory document

**Export Format:** ZIP archive containing JSON files organized by category, plus a metadata file.

**Availability:** Export links are available for 7 days and then expire for security reasons.

## Data Erasure

Users can request erasure of their personal data. Wallow implements the following erasure strategy:

| Data Category | Erasure Method | Exceptions |
|---------------|----------------|------------|
| Identity | Delete from Keycloak and application | Legal hold or active legal proceedings |
| Billing | Anonymize (retain for tax compliance) | Cannot delete for 7 years due to legal requirements |
| Notifications | Delete notifications and email records | None |
| Announcements | Delete announcements and dismissals | None |
| Storage | Delete files and metadata | None |
| Audit Logs | Retain for compliance period | Cannot delete for 7 years due to legal requirements |

**Anonymization:** Where deletion is not possible due to legal requirements, data is anonymized by removing all identifiable information while preserving aggregate statistics.

**Verification:** Identity verification is required before processing erasure requests to prevent unauthorized deletion.

## International Data Transfers

Wallow is designed to be deployed in any region. Data residency is controlled by:

- **Database Location:** PostgreSQL instance location determines data-at-rest location
- **Keycloak Location:** Identity data resides where Keycloak is deployed
- **Storage Location:** Files are stored in the configured storage provider (S3, local, etc.)

**Recommendation:** Deploy all infrastructure in the same region as your users to minimize cross-border data transfers.

**EU-US Transfers:** If transferring data from EU to US, ensure appropriate safeguards (Standard Contractual Clauses, Data Privacy Framework participation, etc.).

## Third-Party Data Processors

Wallow may share data with the following categories of third-party processors:

| Processor Category | Purpose | Data Shared | Safeguards |
|-------------------|---------|-------------|------------|
| Email Service Provider | Send transactional and notification emails | Email address, name, email content | DPA, TLS encryption |
| Payment Processor | Process payments (if integrated) | Payment information, billing address | PCI-DSS compliance, DPA |
| Cloud Infrastructure | Host application and database | All data | Encryption, access controls, DPA |
| Analytics Provider | Usage analytics (if enabled and consented) | Anonymized usage data | Anonymization, consent required |

**Data Processing Agreements (DPA):** All third-party processors are required to sign DPAs ensuring GDPR compliance.

## Data Subject Rights Requests

Users can exercise their rights through the following mechanisms:

### Self-Service (Recommended)

- **Data Export:** Via administrative API - Automated, available in 24-48 hours
- **Data Erasure:** Via administrative API - Requires identity verification, processed within 30 days
- **Data Correction:** Standard profile update APIs - Immediate effect

### Email Requests

For requests that cannot be handled through self-service:

1. Email: privacy@[tenant-domain]
2. Include: Full name, email address, request type
3. Response time: Within 30 days
4. Identity verification: May be required for security

### Request Processing SLA

| Request Type | Processing Time | Verification Required |
|--------------|-----------------|----------------------|
| Data Export | 24-48 hours | No (authenticated user) |
| Data Access | 24-48 hours | No (authenticated user) |
| Data Erasure | Up to 30 days | Yes |
| Data Rectification | Immediate | No (authenticated user) |
| Consent Withdrawal | Immediate | No (authenticated user) |

## Contact Information

For questions about this data inventory or data protection practices:

- **Data Protection Officer (if applicable):** dpo@[tenant-domain]
- **Privacy Inquiries:** privacy@[tenant-domain]
- **General Support:** support@[tenant-domain]

## Document Version

- **Version:** 1.1
- **Last Updated:** 2026-02-15
- **Next Review:** 2026-08-15 (6 months)

## Appendix: Data Inventory Technical Details

### Database Schemas

Personal data is stored across multiple PostgreSQL schemas:

**Module Schemas:**
- `identity` - Service account metadata, SSO/SCIM configs, API scopes (user data in Keycloak)
- `billing` - Invoices, payments, subscriptions, metering (meter definitions, quotas, usage records)
- `notifications` - In-app notifications, email messages, email preferences
- `announcements` - Announcements, announcement dismissals, changelog entries
- `messaging` - Messaging-related data (direct messages, conversations)
- `storage` - File metadata and buckets
- `inquiries` - Inquiry/contact form submissions


**Shared Infrastructure Schemas:**
- `audit` - Security audit logs (via Audit.NET interceptor)
- `wolverine` - Wolverine message outbox/inbox tables

### Cache Data

Temporary data stored in Valkey (Redis):

- Session tokens (expires with session)
- Rate limiting data (no PII, IP address only)
- SignalR backplane messages (transient, no long-term storage)

**Retention:** All cache data expires automatically and is not considered a persistent store.

### Backup Data

Database backups may contain personal data. Backup retention:

- **Daily Backups:** 30 days
- **Weekly Backups:** 90 days
- **Monthly Backups:** 1 year

**Note:** Data erasure requests do not retroactively erase data in backups. Backups are retained for disaster recovery only and are not used for operational purposes.

## Changes to This Inventory

When new data collection is added to Wallow:

1. Update this documentation
2. Notify users of material changes to data processing
3. Review retention policies to ensure compliance

---

**Last Updated:** 2026-02-15
**Maintained By:** Wallow Compliance Team
**Review Schedule:** Quarterly
