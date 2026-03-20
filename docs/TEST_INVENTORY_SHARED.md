# Test Inventory: Shared Libraries

## Summary

| Project | Source Files | Test Files | Tests Passing | Coverage Estimate |
|---------|-------------|------------|---------------|-------------------|
| Wallow.Shared.Kernel | 33 | 25 | 287 | ~89.5% |
| Wallow.Shared.Infrastructure | 23 | 28 | 329 | ~58.3% |

## Wallow.Shared.Kernel

### Test Coverage Map

| Source File | Test File | Status |
|------------|-----------|--------|
| Domain/AggregateRoot.cs | Domain/AggregateRootTests.cs | Covered |
| Domain/AuditableEntity.cs | Domain/AuditableEntityTests.cs | Covered |
| Domain/DomainException.cs | Domain/DomainExceptionTests.cs | Covered |
| Domain/Entity.cs | Domain/EntityTests.cs | Covered |
| Domain/SystemClock.cs | Domain/SystemClockTests.cs | Covered |
| Domain/ValueObject.cs | Domain/ValueObjectTests.cs | Covered |
| Domain/IDomainEvent.cs | (interface only) | N/A |
| Domain/ISystemClock.cs | (interface only) | N/A |
| Identity/TenantId.cs | Identity/StronglyTypedIdTests.cs | Covered |
| Identity/UserId.cs | Identity/StronglyTypedIdTests.cs | Covered |
| Identity/StronglyTypedIdConverter.cs | Identity/StronglyTypedIdTests.cs | Covered |
| Identity/IStronglyTypedId.cs | (interface only) | N/A |
| MultiTenancy/TenantContext.cs | MultiTenancy/TenantContextTests.cs | Covered |
| MultiTenancy/TenantContextFactory.cs | MultiTenancy/TenantContextFactoryTests.cs | Covered |
| MultiTenancy/TenantQueryExtensions.cs | MultiTenancy/TenantQueryExtensionsTests.cs | Covered |
| MultiTenancy/TenantSaveChangesInterceptor.cs | MultiTenancy/TenantSaveChangesInterceptorTests.cs | Covered |
| MultiTenancy/RegionConfiguration.cs | MultiTenancy/RegionConfigurationTests.cs | Covered |
| MultiTenancy/ITenantContext.cs | (interface only) | N/A |
| MultiTenancy/ITenantContextFactory.cs | (interface only) | N/A |
| MultiTenancy/ITenantScoped.cs | (interface only) | N/A |
| Extensions/ServiceCollectionExtensions.cs | Extensions/ServiceCollectionExtensionsTests.cs | Covered |
| Extensions/StringExtensions.cs | Extensions/StringExtensionsTests.cs | Covered |
| Results/Result.cs | Results/ResultTests.cs | Covered |
| Results/Error.cs | Results/ErrorTests.cs | Covered |
| Pagination/PagedResult.cs | Results/PagedResultTests.cs | Covered |
| CustomFields/CustomFieldRegistry.cs | CustomFields/CustomFieldRegistryTests.cs | Covered |
| CustomFields/FieldValidationRules.cs | CustomFields/FieldValidationRulesTests.cs | Covered |
| CustomFields/CustomFieldType.cs | CustomFields/CustomFieldRegistryTests.cs | Covered (enum) |
| CustomFields/CustomFieldOption.cs | CustomFields/CustomFieldRegistryTests.cs | Covered (record) |
| CustomFields/ICustomFieldValidator.cs | (interface only) | N/A |
| CustomFields/IHasCustomFields.cs | (interface only) | N/A |
| Messaging/IntegrationEventHandler.cs | (no test) | GAP |
| Messaging/WolverineErrorHandlingExtensions.cs | Messaging/WolverineErrorHandlingExtensionsTests.cs | Covered |
| Diagnostics.cs | Diagnostics/DiagnosticsTests.cs | Covered |
| Observability/SloDefinitions.cs | Diagnostics/SloDefinitionsTests.cs | Covered |
| Plugins/PluginManifest.cs | Plugins/PluginModelTests.cs | Covered |
| Plugins/PluginPermission.cs | Plugins/PluginModelTests.cs | Covered |
| Plugins/PluginContext.cs | Plugins/PluginModelTests.cs | Covered |
| Plugins/PluginLifecycleState.cs | Plugins/PluginModelTests.cs | Covered |
| Plugins/IWallowPlugin.cs | (interface only) | N/A |
| Plugins/IPluginPermissionValidator.cs | (interface only) | N/A |
| BackgroundJobs/IJobScheduler.cs | (interface only) | N/A |

### Kernel Coverage Gaps

1. **Messaging/IntegrationEventHandler.cs** - No dedicated test file. This is the primary gap holding Kernel below 90%.

### Kernel: What's Needed for 90%+

- Add tests for `IntegrationEventHandler.cs` (abstract base class for handling integration events)
- Review existing test completeness for edge cases in covered files

---

## Wallow.Shared.Infrastructure

### Test Coverage Map

| Source File | Test File | Status |
|------------|-----------|--------|
| AsyncApi/AsyncApiDocumentGenerator.cs | AsyncApi/AsyncApiDocumentGeneratorTests.cs | Covered |
| AsyncApi/EventFlowDiscovery.cs | AsyncApi/EventFlowDiscoveryTests.cs | Covered |
| AsyncApi/JsonSchemaGenerator.cs | AsyncApi/JsonSchemaGeneratorTests.cs | Covered |
| AsyncApi/MermaidFlowGenerator.cs | AsyncApi/MermaidFlowGeneratorTests.cs | Covered |
| AsyncApi/ (integration) | AsyncApi/AsyncApiIntegrationTests.cs | Covered |
| Auditing/AuditDbContext.cs | Auditing/AuditTenantIsolationTests.cs | Partial |
| Auditing/AuditEntry.cs | Auditing/AuditEntryTests.cs | Covered |
| Auditing/AuditInterceptor.cs | Auditing/AuditInterceptorTests.cs | Covered |
| Auditing/AuditingExtensions.cs | Auditing/AuditingExtensionsTests.cs | Covered |
| BackgroundJobs/BackgroundJobsExtensions.cs | BackgroundJobs/BackgroundJobsExtensionsTests.cs | Covered |
| BackgroundJobs/HangfireJobScheduler.cs | BackgroundJobs/HangfireJobSchedulerTests.cs | Covered |
| Filters/DataResidencyFilter.cs | Persistence/DataResidencyFilterTests.cs | Covered |
| Middleware/WolverineModuleTaggingMiddleware.cs | Middleware/WolverineModuleTaggingMiddlewareTests.cs | Covered |
| Persistence/DictionaryValueComparer.cs | Persistence/DictionaryValueComparerTests.cs | Covered |
| Persistence/RegionAwareConnectionFactory.cs | Persistence/RegionAwareConnectionFactoryTests.cs | Covered |
| Plugins/PluginAssemblyLoadContext.cs | Plugins/PluginAssemblyLoadContextTests.cs | Covered |
| Plugins/PluginLifecycleManager.cs | Plugins/PluginLifecycleManagerTests.cs | Covered |
| Plugins/PluginLoadException.cs | Plugins/PluginLoadExceptionTests.cs | Covered |
| Plugins/PluginLoader.cs | Plugins/PluginLoaderTests.cs | Covered |
| Plugins/PluginManifestLoader.cs | Plugins/PluginManifestLoaderTests.cs | Covered |
| Plugins/PluginOptions.cs | Plugins/PluginConfigurationTests.cs | Covered |
| Plugins/PluginPermissionValidator.cs | Plugins/PluginPermissionValidatorTests.cs | Covered |
| Plugins/PluginRegistry.cs | Plugins/PluginRegistryTests.cs | Covered |
| Plugins/PluginRegistryEntry.cs | Plugins/PluginRegistryTests.cs | Covered |
| Plugins/PluginServiceExtensions.cs | Plugins/PluginServiceExtensionsTests.cs | Covered |
| ServiceClients/HttpNotificationServiceClient.cs | ServiceClients/HttpNotificationServiceClientTests.cs | Covered |
| Services/HtmlSanitizationService.cs | Services/HtmlSanitizationServiceTests.cs | Covered |
| Workflows/ElsaExtensions.cs | Workflows/ElsaExtensionsTests.cs | Covered |
| Workflows/WorkflowActivityBase.cs | Workflows/WorkflowActivityBaseTests.cs | Covered |

### Infrastructure Coverage Gaps

All source files have corresponding test files. The 58.3% coverage is due to **insufficient test depth** within existing test files, not missing test files. Key areas needing more tests:

1. **Auditing/AuditDbContext.cs** - Only partially covered via AuditTenantIsolationTests; needs dedicated tests for query filters, model configuration
2. **Plugins/** - Complex classes like PluginLoader, PluginLifecycleManager, and PluginServiceExtensions likely have many untested code paths
3. **AsyncApi/** - Generator classes may have untested edge cases
4. **Persistence/RegionAwareConnectionFactory.cs** - May need more region-specific scenario tests

### Infrastructure: What's Needed for 90%+

- Expand test depth across existing test files, focusing on:
  - All code branches in Plugin loading/lifecycle
  - AuditDbContext model configuration and tenant query filters
  - Edge cases in AsyncApi generators
  - Error handling paths in all services
- EF Core migration files (Migrations/) are excluded from coverage as they are auto-generated

---

## Test Execution

```bash
# Run Kernel tests (287 tests, ~364ms)
dotnet test tests/Wallow.Shared.Kernel.Tests/

# Run Infrastructure tests (329 tests, ~22s)
dotnet test tests/Wallow.Shared.Infrastructure.Tests/
```

All tests currently pass with 0 failures and 0 skipped.
