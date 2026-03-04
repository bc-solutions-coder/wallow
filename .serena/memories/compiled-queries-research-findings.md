# Compiled Queries Research - Foundry Codebase

## Executive Summary
Found **25+ simple EF Core queries across 5 modules** that are prime candidates for compiled queries. Feature flags are hottest path. Dapper queries already optimized in Billing module. Some caching exists (Configuration, Identity).

---

## 1. DbContext Classes Found

| Module | DbContext | Schema | DbSets |
|--------|-----------|--------|--------|
| **Shared** | `AuditDbContext` | `audit` | AuditEntries |
| **Shared** | `TenantAwareDbContext<T>` | Base class | - |
| **Configuration** | `ConfigurationDbContext` | `configuration` | CustomFieldDefinitions, FeatureFlags, FeatureFlagOverrides |
| **Identity** | `IdentityDbContext` | `identity` | ServiceAccountMetadata, ApiScopes, SsoConfigurations, ScimConfigurations, ScimSyncLogs |
| **Storage** | `StorageDbContext` | `storage` | StorageBuckets, StoredFiles |
| **Billing** | `BillingDbContext` | `billing` | Invoices, Payments, Subscriptions, InvoiceLineItems, MeterDefinitions, QuotaDefinitions, UsageRecords |
| **Communications** | `CommunicationsDbContext` | `communications` | EmailMessages, EmailPreferences, SmsMessages, SmsPreferences, Notifications, ChannelPreferences, Conversations, Messages, Participants, Announcements, AnnouncementDismissals, ChangelogEntries, ChangelogItems |

---

## 2. Simple Lookup Queries (High Priority for Compiled Queries)

### **CONFIGURATION MODULE** ✨ HOTTEST PATH
- **FeatureFlagRepository.GetByIdAsync()** — `FirstOrDefaultAsync(f => f.Id == id)`
- **FeatureFlagRepository.GetByKeyAsync()** — `FirstOrDefaultAsync(f => f.Key == key)` + Include
- **FeatureFlagOverrideRepository.GetByIdAsync()** — `FirstOrDefaultAsync(o => o.Id == id)`
- **FeatureFlagOverrideRepository.GetAllAsync()** — simple filter, no includes
- **CustomFieldDefinitionRepository.GetByIdAsync()** — `FirstOrDefaultAsync(x => x.Id == id)`
- **CachedFeatureFlagService** — wraps FeatureFlagService with IDistributedCache (already optimized)

**Analysis:**
- Feature flags called on nearly every request (global configuration hot path)
- GetByKeyAsync includes Overrides collection (small data volume)
- Already has distributed cache wrapper, but underlying EF query still runs on cache miss
- **Compiled queries would reduce parse/plan cost on every cache miss**

### **IDENTITY MODULE**
- **ServiceAccountRepository.GetByIdAsync()** — `FirstOrDefaultAsync(x => x.Id == id)` + Where filter
- **ServiceAccountRepository.GetByKeycloakClientIdAsync()** — `FirstOrDefaultAsync(x => x.KeycloakClientId == keycloakClientId)` + IgnoreQueryFilters() (middleware hotpath)
- **SsoConfigurationRepository.GetAsyncReadOnly()** — simple `FirstOrDefaultAsync()`
- **ScimConfigurationRepository.GetAsyncReadOnly()** — simple `FirstOrDefaultAsync()`
- **ApiScopeRepository** — (need to verify, likely simple lookups)

**Analysis:**
- ServiceAccount lookups are in middleware (every request if using service accounts)
- Simple single-row lookups, excellent compiled query candidates

### **STORAGE MODULE**
- **StorageBucketRepository.GetByNameAsync()** — `FirstOrDefaultAsync(b => b.Name == name)`
- **StoredFileRepository.GetByIdAsync()** — `FirstOrDefaultAsync(f => f.Id == id)`

**Analysis:**
- Simple lookups, likely called frequently in file operations
- No includes, single predicate conditions

### **BILLING MODULE**
- **SubscriptionRepository.GetByIdAsync()** — `FirstOrDefaultAsync(s => s.Id == id)`
- **SubscriptionRepository.GetActiveByUserIdAsync()** — `FirstOrDefaultAsync()` + Where(status, userId) + OrderByDescending
- **InvoiceRepository.GetByIdAsync()** — appears twice, `FirstOrDefaultAsync(i => i.Id == id)`
- **PaymentRepository.GetByIdAsync()** — `FirstOrDefaultAsync(p => p.Id == id)`
- **MeterDefinitionRepository.GetByIdAsync()** — `FirstOrDefaultAsync(m => m.Id == id)`
- **MeterDefinitionRepository.GetByCodeAsync()** — `FirstOrDefaultAsync(m => m.Code == code)`
- **QuotaDefinitionRepository.GetByIdAsync()** — `FirstOrDefaultAsync(q => q.Id == id)`
- **QuotaDefinitionRepository.GetTenantOverrideAsync()** — `FirstOrDefaultAsync()` + multi-predicate
- **QuotaDefinitionRepository.GetEffectiveQuotaAsync()** — complex (see below)
- **UsageRecordRepository.GetByIdAsync()** — `FirstOrDefaultAsync(u => u.Id == id)`
- **UsageRecordRepository.GetForPeriodAsync()** — `FirstOrDefaultAsync()` + 4-part predicate

**Analysis:**
- Billing has mix of simple and moderately complex queries
- GetActiveByUserIdAsync and GetForPeriodAsync have multi-condition WHERE + ORDER BY
- QuotaDefinition has tenant-override fallback logic (dynamic, harder to compile)

### **COMMUNICATIONS MODULE**
- **AnnouncementRepository.GetByIdAsync()** — `FirstOrDefaultAsync(a => a.Id == id)`
- **ConversationRepository.GetByIdAsync()** — `FirstOrDefaultAsync(c => c.Id == id)`
- **NotificationRepository.GetByIdAsync()** — `FirstOrDefaultAsync(n => n.Id == id)`
- **ChangelogRepository.GetByIdAsync()** — `FirstOrDefaultAsync(e => e.Id == id)`
- **ChangelogRepository.GetByVersionAsync()** — `FirstOrDefaultAsync(e => e.Version == version)`
- **ChangelogRepository.GetLatestAsync()** — simple `FirstOrDefaultAsync()`
- **EmailPreferenceRepository.GetByUserAndTypeAsync()** — `FirstOrDefaultAsync(ep => ep.UserId == userId && ep.NotificationType == notificationType)`
- **ChannelPreferenceRepository** — (needs verification)

**Analysis:**
- Many simple ID-based lookups (excellent candidates)
- GetByUserAndTypeAsync is 2-column composite predicate (frequently called for preference checks)

---

## 3. Categorized Query Candidates

### **HIGH PRIORITY** (Simple shape, frequent calls, no caching)
| Repository | Method | Pattern | Frequency |
|------------|--------|---------|-----------|
| FeatureFlagRepository | GetByIdAsync | ID lookup | Per request (cache miss) |
| FeatureFlagRepository | GetByKeyAsync | String key lookup | Per request (cache miss) |
| ServiceAccountRepository | GetByKeycloakClientIdAsync | String lookup + IgnoreQueryFilters | Every request (middleware) |
| StorageBucketRepository | GetByNameAsync | String lookup | High (file operations) |
| AnnouncementRepository | GetByIdAsync | ID lookup | Moderate |
| InvoiceRepository | GetByIdAsync | ID lookup | High (CRUD operations) |
| SubscriptionRepository | GetByIdAsync | ID lookup | High (CRUD operations) |
| EmailPreferenceRepository | GetByUserAndTypeAsync | Composite key (userId + type) | High (preference checks) |

**Rationale:** All are single-row lookups, simple WHERE predicates, no aggregations or includes (except GetByKeyAsync which includes small Overrides).

### **MEDIUM PRIORITY** (Simple but already cached, or multi-predicate)
| Repository | Method | Reason |
|------------|--------|--------|
| FeatureFlagOverrideRepository | GetByIdAsync | Already wrapped in CachedFeatureFlagService |
| SubscriptionRepository | GetActiveByUserIdAsync | Has ORDER BY, slightly more complex |
| UsageRecordRepository | GetForPeriodAsync | 4-condition WHERE + ORDER BY |
| QuotaDefinitionRepository | GetTenantOverrideAsync | Multi-predicate but simple |
| MeterDefinitionRepository | GetByCodeAsync | Simple, but lower call frequency |

**Rationale:** Either already have caching or involve additional sorting/filtering that slightly increases plan complexity. Still good candidates but lower ROI than HIGH priority.

### **LOW PRIORITY** (Complex queries, aggregations, or already optimized)
| Repository/Service | Method | Reason |
|-----------------|--------|--------|
| QuotaDefinitionRepository | GetEffectiveQuotaAsync | Conditional logic, two separate queries, dynamic |
| InvoiceQueryService | GetTotalRevenueAsync | **Already using Dapper** (SUM aggregation) |
| InvoiceQueryService | GetCountAsync | **Already using Dapper** |
| InvoiceQueryService | GetPendingCountAsync | **Already using Dapper** |
| InvoiceQueryService | GetOutstandingAmountAsync | **Already using Dapper** |
| MessagingQueryService | GetMessages() | **Using Dapper** (complex multi-join reads) |
| RevenueReportService | GetAsync | **Using Dapper** (complex reporting query) |
| InvoiceReportService | GetAsync | **Using Dapper** |
| PaymentReportService | GetAsync | **Using Dapper** |

**Rationale:** Complex aggregations/reports already using Dapper (correct choice). GetEffectiveQuotaAsync has runtime branching that makes compilation harder.

---

## 4. Dapper Usage (Already Optimized)

**Billing Module (Reports & Query Services):**
- InvoiceQueryService: 4 queries (GetTotalRevenueAsync, GetCountAsync, GetPendingCountAsync, GetOutstandingAmountAsync)
- RevenueReportService: Complex grouping/aggregation
- InvoiceReportService: Complex reporting
- PaymentReportService: Complex reporting

**Communications Module:**
- MessagingQueryService: GetMessages, GetConversations, GetParticipants (complex multi-joins)

**Summary:** Dapper is correctly used for aggregations, reports, and multi-join reads. These should NOT be compiled queries.

---

## 5. Caching Patterns Found

**Configuration Module:**
- `CachedFeatureFlagService` wraps `IFeatureFlagService` with `IDistributedCache`
- Cache invalidation on Create/Update/Delete operations
- Compiled queries on cache miss would still help

**Identity Module:**
- `UserQueryService` uses `IMemoryCache` for user lookups
- ServiceAccountRepository not cached (middleware hotpath)

**Billing Module:**
- `MeteringMiddleware` uses `IMemoryCache` for rate limiting/metering state

---

## 6. Recommendations

### Tier 1 (Implement First)
1. **FeatureFlagRepository.GetByIdAsync()** - Global config hotpath
2. **FeatureFlagRepository.GetByKeyAsync()** - Global config hotpath
3. **ServiceAccountRepository.GetByKeycloakClientIdAsync()** - Middleware hotpath
4. **StorageBucketRepository.GetByNameAsync()** - High-frequency file operations
5. **InvoiceRepository.GetByIdAsync()** - High CRUD volume

### Tier 2 (Medium ROI)
6. **SubscriptionRepository.GetByIdAsync()** - Billing CRUD
7. **EmailPreferenceRepository.GetByUserAndTypeAsync()** - Preference checks
8. **AnnouncementRepository.GetByIdAsync()** - Configuration read
9. **FeatureFlagOverrideRepository.GetByIdAsync()** - Config detail
10. **QuotaDefinitionRepository.GetTenantOverrideAsync()** - Quota lookups

### Tier 3 (Lower Priority, but viable)
11-20. Additional ID/lookup queries in other modules (MeterDefinition, Payment, Conversation, etc.)

---

## 7. Technical Notes

- **Query Filters:** TenantAwareDbContext uses query filters (IgnoreQueryFilters may require special compiled query handling)
- **AsTracking() calls:** All single-row lookups use AsTracking() (correct for mutations)
- **Includes:** Most simple lookups don't include; GetByKeyAsync includes Overrides (small collection)
- **Async:** All use async pattern (FirstOrDefaultAsync, ToListAsync)
- **Unit Tests:** Several test DbContext classes exist, can be used for validation
