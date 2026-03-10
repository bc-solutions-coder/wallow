# Phase 12: Storage Module

**Scope:** Complete Storage module - Domain, Application, Infrastructure, Api layers + all tests
**Status:** Not Started
**Files:** 74 source files, 34 test files

## How to Use This Document
- Work through layers bottom-up: Domain -> Application -> Infrastructure -> Api -> Tests
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Domain Layer

### Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | src/Modules/Storage/Foundry.Storage.Domain/Entities/StorageBucket.cs | Aggregate root for logical file groupings with shared settings (tenant-scoped) | Factory Create() method; wildcard content type matching (e.g., "image/*"); file size validation; JSONB-stored allowed content types; optional RetentionPolicy owned value object; versioning flag | Shared.Kernel (AggregateRoot, ITenantScoped, TenantId) | |
| 2 | [ ] | src/Modules/Storage/Foundry.Storage.Domain/Entities/StoredFile.cs | Aggregate root for stored file metadata (tenant-scoped, actual bytes in storage backend) | Two factory methods: Create (immediately available) and CreatePendingValidation (awaits scan); status transitions: MarkAsAvailable, MarkAsRejected, MarkAsDeleted; storageKey tracks backend location | Shared.Kernel (AggregateRoot, ITenantScoped, TenantId) | |

### Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 3 | [ ] | src/Modules/Storage/Foundry.Storage.Domain/Enums/AccessLevel.cs | Enum defining bucket access levels | Private (0), Public (1) | None | |
| 4 | [ ] | src/Modules/Storage/Foundry.Storage.Domain/Enums/FileStatus.cs | Enum defining file lifecycle states | PendingValidation (0), Available (1), Rejected (2) | None | |
| 5 | [ ] | src/Modules/Storage/Foundry.Storage.Domain/Enums/RetentionAction.cs | Enum defining retention policy actions | Delete (0), Archive (1) | None | |
| 6 | [ ] | src/Modules/Storage/Foundry.Storage.Domain/Enums/StorageProvider.cs | Enum defining storage backend types | Local (0), S3 (1) | None | |

### Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 7 | [ ] | src/Modules/Storage/Foundry.Storage.Domain/Events/StorageEvents.cs | All domain events for Storage module in single file | FileUploadedEvent, FileDeletedEvent, BucketCreatedEvent, BucketDeletedEvent -- all extend DomainEvent | Shared.Kernel (DomainEvent), Domain identity types | |

### Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 8 | [ ] | src/Modules/Storage/Foundry.Storage.Domain/Identity/StorageBucketId.cs | Strongly-typed ID for StorageBucket | Readonly record struct with Create/New factory methods | Shared.Kernel (IStronglyTypedId) | |
| 9 | [ ] | src/Modules/Storage/Foundry.Storage.Domain/Identity/StoredFileId.cs | Strongly-typed ID for StoredFile | Readonly record struct with Create/New factory methods | Shared.Kernel (IStronglyTypedId) | |

### Value Objects

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 10 | [ ] | src/Modules/Storage/Foundry.Storage.Domain/ValueObjects/RetentionPolicy.cs | Value object for file retention configuration | Record with Days (int) and Action (RetentionAction) | Domain enums (RetentionAction) | |

## Application Layer

### Commands / CreateBucket

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 11 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/CreateBucket/CreateBucketCommand.cs | Command record for creating a storage bucket | Name, Description, Access, MaxFileSizeBytes, AllowedContentTypes, RetentionDays, RetentionAction, Versioning | Domain enums | |
| 12 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/CreateBucket/CreateBucketHandler.cs | Handler for creating storage buckets | Checks duplicate name; constructs RetentionPolicy from days+action; creates via factory; returns Result<BucketDto> | IStorageBucketRepository, ITenantContext | |
| 13 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/CreateBucket/CreateBucketValidator.cs | FluentValidation for CreateBucketCommand | Bucket name pattern (lowercase alphanumeric + hyphens); MaxFileSizeBytes >= 0; RetentionDays > 0 when present; RetentionAction required with days | FluentValidation | |

### Commands / DeleteBucket

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 14 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/DeleteBucket/DeleteBucketCommand.cs | Command record for deleting a bucket | TenantId, Name, Force (for non-empty buckets) | None | |
| 15 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/DeleteBucket/DeleteBucketHandler.cs | Handler for deleting buckets with force option | Tenant ownership check; if files exist and not force, returns validation error; force mode deletes all files from storage backend then removes records | IStorageBucketRepository, IStoredFileRepository, IStorageProvider | |
| 16 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/DeleteBucket/DeleteBucketValidator.cs | FluentValidation for DeleteBucketCommand | TenantId and Name required | FluentValidation | |

### Commands / DeleteFile

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 17 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/DeleteFile/DeleteFileCommand.cs | Command record for deleting a file | TenantId and FileId | None | |
| 18 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/DeleteFile/DeleteFileHandler.cs | Handler for deleting files | Tenant ownership check; deletes from storage backend; removes from repository | IStoredFileRepository, IStorageProvider | |
| 19 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/DeleteFile/DeleteFileValidator.cs | FluentValidation for DeleteFileCommand | TenantId and FileId required | FluentValidation | |

### Commands / ScanUploadedFile

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 20 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/ScanUploadedFile/ScanUploadedFileCommand.cs | Command record for scanning an uploaded file | Single StoredFileId property | Domain identity | |
| 21 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/ScanUploadedFile/ScanUploadedFileHandler.cs | Static handler for async file scanning after presigned upload | Downloads file from storage; scans via IFileScanner; marks Available or Rejected based on result; structured logging | IStoredFileRepository, IStorageProvider, IFileScanner, ILogger | |

### Commands / UploadFile

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 22 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/UploadFile/UploadFileHandler.cs | Handler for direct file upload | Validates bucket exists, content type allowed, file size within limit; scans file before storing; sanitizes filename; builds tenant-scoped storage key; creates StoredFile record | IStorageBucketRepository, IStoredFileRepository, IStorageProvider, IFileScanner | |
| 23 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Commands/UploadFile/UploadFileValidator.cs | FluentValidation for UploadFileCommand with security checks | Magic byte validation for known content types (JPEG, PNG, PDF, GIF, ZIP); blocks PE/DLL (MZ header), HTML, SVG signatures; path traversal detection; async stream-based validation | FluentValidation | |

### Configuration

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 24 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Configuration/PresignedUrlOptions.cs | Options class for presigned URL expiry limits | MaxDownloadExpiryMinutes (default 60), MaxUploadExpiryMinutes (default 15); bound from "Storage:PresignedUrls" config section | None | |

### DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 25 | [ ] | src/Modules/Storage/Foundry.Storage.Application/DTOs/BucketDto.cs | DTO records for bucket data transfer | BucketDto with all bucket properties; RetentionPolicyDto (Days, Action as string) | None | |
| 26 | [ ] | src/Modules/Storage/Foundry.Storage.Application/DTOs/PresignedUrlResult.cs | DTO records for presigned URL results | PresignedUrlResult (Url, ExpiresAt); PresignedUploadResult (FileId, UploadUrl, ExpiresAt) | None | |
| 27 | [ ] | src/Modules/Storage/Foundry.Storage.Application/DTOs/StoredFileDto.cs | DTO record for stored file metadata | Id, TenantId, BucketId, FileName, ContentType, SizeBytes, Path, IsPublic, UploadedBy, UploadedAt, Metadata | None | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 28 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Extensions/ApplicationExtensions.cs | DI registration for Storage application layer | Registers FluentValidation validators from assembly | FluentValidation, Microsoft.Extensions.DependencyInjection | |

### Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 29 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Interfaces/IFileScanner.cs | Interface for antivirus file scanning + FileScanResult record | ScanAsync returns FileScanResult; Clean/Infected factory methods | None | |
| 30 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Interfaces/IStorageBucketRepository.cs | Repository interface for StorageBucket persistence | GetByNameAsync, ExistsByNameAsync, Add, Remove, SaveChangesAsync | Domain entities | |
| 31 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Interfaces/IStoredFileRepository.cs | Repository interface for StoredFile persistence | GetByIdAsync, GetByBucketIdAsync (with pathPrefix), GetByBucketIdPagedAsync, Add, Remove, SaveChangesAsync | Domain entities, Shared.Kernel (PagedResult) | |

### Mappings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 32 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Mappings/StorageMappings.cs | Extension methods mapping domain entities to DTOs | ToDto() for StoredFile and StorageBucket; deserializes AllowedContentTypes JSON; maps RetentionPolicy to RetentionPolicyDto | Domain entities, DTOs | |

### Queries / GetBucketByName

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 33 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetBucketByName/GetBucketByNameQuery.cs | Query record for retrieving a bucket by name | Single Name string property | None | |
| 34 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetBucketByName/GetBucketByNameHandler.cs | Handler that retrieves a bucket by name | Loads by name; returns NotFound if null; maps to BucketDto | IStorageBucketRepository | |

### Queries / GetFileById

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 35 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetFileById/GetFileByIdQuery.cs | Query record for retrieving a file by ID | TenantId and FileId | None | |
| 36 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetFileById/GetFileByIdHandler.cs | Handler that retrieves file metadata with tenant check | Loads by ID; tenant ownership verification; maps to StoredFileDto | IStoredFileRepository | |

### Queries / GetFilesByBucket

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 37 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetFilesByBucket/GetFilesByBucketQuery.cs | Query record for listing files in a bucket | TenantId, BucketName, PathPrefix, Page, PageSize | None | |
| 38 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetFilesByBucket/GetFilesByBucketHandler.cs | Handler that lists files in a bucket with pagination | Resolves bucket by name; paged query with tenant filter and optional path prefix; maps to PagedResult<StoredFileDto> | IStorageBucketRepository, IStoredFileRepository | |

### Queries / GetPresignedUrl

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 39 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetPresignedUrl/GetPresignedUrlQuery.cs | Query record for generating a download presigned URL | TenantId, FileId, optional Expiry | None | |
| 40 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetPresignedUrl/GetPresignedUrlHandler.cs | Handler that generates presigned download URLs | Tenant ownership check; FileStatus must be Available; expiry clamped to MaxDownloadExpiryMinutes; delegates to IStorageProvider.GetPresignedUrlAsync | IStoredFileRepository, IStorageProvider, IOptions<PresignedUrlOptions> | |

### Queries / GetUploadPresignedUrl

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 41 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetUploadPresignedUrl/GetUploadPresignedUrlQuery.cs | Query record for generating an upload presigned URL | TenantId, UserId, BucketName, FileName, ContentType, SizeBytes, Path, Expiry | None | |
| 42 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetUploadPresignedUrl/GetUploadPresignedUrlHandler.cs | Handler that generates presigned upload URLs with pending file record | Validates bucket constraints (content type, file size); creates StoredFile in PendingValidation status; publishes ScanUploadedFileCommand via Wolverine; expiry clamped to MaxUploadExpiryMinutes | IStorageBucketRepository, IStoredFileRepository, IStorageProvider, IMessageBus, IOptions<PresignedUrlOptions> | |
| 43 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Queries/GetUploadPresignedUrl/GetUploadPresignedUrlValidator.cs | FluentValidation for GetUploadPresignedUrlQuery | TenantId, UserId, BucketName, FileName required; path traversal detection; content type and size validation | FluentValidation | |

### Telemetry

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 44 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Telemetry/StorageModuleTelemetry.cs | OpenTelemetry instrumentation for Storage module | ActivitySource for "Storage"; Meter with OperationsTotal counter, BytesTransferred counter, OperationDuration histogram; Start*Activity helper methods | Shared.Kernel (Diagnostics) | |

### Utilities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 45 | [ ] | src/Modules/Storage/Foundry.Storage.Application/Utilities/FileNameSanitizer.cs | Sanitizes user-supplied filenames for safe storage | Strips path components; removes control chars/null bytes/quotes/semicolons via GeneratedRegex; replaces invalid filename chars; truncates to 255 chars preserving extension; fallback to "unnamed" | None | |

## Infrastructure Layer

### Configuration

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 46 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Configuration/StorageOptions.cs | Options classes for storage provider configuration | StorageOptions (provider type, ClamAV host/port), LocalStorageOptions (BasePath, BaseUrl), S3StorageOptions (Endpoint, credentials, region, RegionBuckets with GetBucketForRegion fallback) | Domain enums (StorageProvider) | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 47 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Extensions/StorageInfrastructureExtensions.cs | Infrastructure DI registration with provider selection and health checks | Registers DbContext, repositories, IFileScanner; switches between Local/S3 provider based on config; configures S3 client (supports MinIO, Garage, R2); ClamAV TCP health check | AWS S3, EF Core, Configuration options | |
| 48 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Extensions/StorageModuleExtensions.cs | Top-level module DI registration and database initialization | AddStorageModule combines application + infrastructure; InitializeStorageModuleAsync runs migrations in development | Application/Infrastructure extensions | |

### Persistence / Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 49 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Persistence/Configurations/StorageBucketConfiguration.cs | EF Core entity configuration for StorageBucket | Table "buckets" in storage schema; StronglyTypedIdConverter; OwnsOne for RetentionPolicy (retention_days, retention_action); JSONB for allowed_content_types; unique index on (TenantId, Name) | EF Core, Domain entities/identity | |
| 50 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Persistence/Configurations/StoredFileConfiguration.cs | EF Core entity configuration for StoredFile | Table "files" in storage schema; JSONB for metadata; unique index on StorageKey; foreign key to StorageBucket with Restrict delete; indexes on TenantId, BucketId, (BucketId, Path) | EF Core, Domain entities/identity | |

### Persistence

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 51 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Persistence/StorageDbContext.cs | EF Core DbContext for Storage module | Extends TenantAwareDbContext; "storage" schema; DbSets for Buckets and Files; NoTracking default; applies tenant query filters | Shared.Infrastructure (TenantAwareDbContext) | |
| 52 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Persistence/StorageDbContextFactory.cs | Design-time factory for EF Core migrations | Creates DbContext with dummy connection string and DesignTimeTenantContext for migration tooling | EF Core Design | |
| 53 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Persistence/DesignTimeTenantContext.cs | Mock ITenantContext for design-time migrations | Returns placeholder TenantId (Guid.Empty), IsResolved=true | Shared.Kernel (ITenantContext, RegionConfiguration) | |

### Persistence / Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 54 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Persistence/Repositories/StorageBucketRepository.cs | EF Core repository for StorageBucket with compiled query | Compiled async query for GetByName with AsTracking; ExistsByNameAsync via AnyAsync; synchronous Add/Remove | StorageDbContext | |
| 55 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Persistence/Repositories/StoredFileRepository.cs | EF Core repository for StoredFile with pagination | Compiled async query for GetById; GetByBucketIdAsync with optional pathPrefix filter; GetByBucketIdPagedAsync with tenant filter, count, skip/take; ordered by UploadedAt descending | StorageDbContext, Shared.Kernel (PagedResult) | |

### Providers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 56 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Providers/LocalStorageProvider.cs | Local filesystem storage provider for development | Upload creates directories and writes file; Download opens FileStream; Delete removes file; path traversal protection via GetFullPath check; presigned URLs return API endpoint URLs | IStorageProvider, StorageOptions | |
| 57 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Providers/S3StorageProvider.cs | S3-compatible storage provider (AWS, MinIO, Garage, R2) | Upload via PutObjectAsync; Download via GetObjectAsync; Delete via DeleteObjectAsync; Exists via GetObjectMetadataAsync; presigned URL via GetPreSignedURLAsync (PUT for upload, GET for download); region-aware bucket resolution via GetBucketForRegion | IAmazonS3, StorageOptions, ITenantContext | |

### Scanning

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 58 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Scanning/ClamAvFileScanner.cs | ClamAV antivirus scanner via TCP INSTREAM protocol | Connects via TcpClient; sends zINSTREAM command; streams file in 8KB chunks with 4-byte big-endian length prefix; parses "OK" or "FOUND" response; structured logging for results | IFileScanner, StorageOptions, ILogger | |

### Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 59 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Migrations/20260213182315_InitialCreate.cs | Initial migration creating storage schema tables | Creates buckets and files tables with indexes | EF Core Migrations | |
| 60 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Migrations/20260213182315_InitialCreate.Designer.cs | Designer file for InitialCreate migration | Auto-generated model snapshot | EF Core Migrations | |
| 61 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Migrations/20260223073410_AddTenantIdToStorageBucket.cs | Migration adding TenantId to StorageBucket | Adds tenant_id column to buckets table | EF Core Migrations | |
| 62 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Migrations/20260223073410_AddTenantIdToStorageBucket.Designer.cs | Designer file for AddTenantIdToStorageBucket migration | Auto-generated model snapshot | EF Core Migrations | |
| 63 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Migrations/20260303060139_AddTenantScopedBucketNameIndex.cs | Migration adding tenant-scoped unique bucket name index | Adds unique index ix_storage_buckets_tenant_name on (TenantId, Name) | EF Core Migrations | |
| 64 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Migrations/20260303060139_AddTenantScopedBucketNameIndex.Designer.cs | Designer file for AddTenantScopedBucketNameIndex migration | Auto-generated model snapshot | EF Core Migrations | |
| 65 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Migrations/20260305024940_SyncModelChanges.cs | Migration syncing model changes | Syncs EF Core model state with database | EF Core Migrations | |
| 66 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Migrations/20260305024940_SyncModelChanges.Designer.cs | Designer file for SyncModelChanges migration | Auto-generated model snapshot | EF Core Migrations | |
| 67 | [ ] | src/Modules/Storage/Foundry.Storage.Infrastructure/Migrations/StorageDbContextModelSnapshot.cs | Current model snapshot for StorageDbContext | Auto-generated snapshot of all entity configurations | EF Core Migrations | |

## Api Layer

### Contracts / Requests

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 68 | [ ] | src/Modules/Storage/Foundry.Storage.Api/Contracts/Requests/CreateBucketRequest.cs | API request record for creating a bucket | Name, Description, Access (string), MaxFileSizeBytes, AllowedContentTypes, RetentionDays, RetentionAction (string), Versioning | None | |
| 69 | [ ] | src/Modules/Storage/Foundry.Storage.Api/Contracts/Requests/PresignedUploadRequest.cs | API request record for requesting a presigned upload URL | BucketName, FileName, ContentType, SizeBytes, Path, ExpiryMinutes | None | |

### Contracts / Responses

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 70 | [ ] | src/Modules/Storage/Foundry.Storage.Api/Contracts/Responses/BucketResponse.cs | API response records for bucket data | BucketResponse with all properties; RetentionPolicyResponse (Days, Action) | None | |
| 71 | [ ] | src/Modules/Storage/Foundry.Storage.Api/Contracts/Responses/FileMetadataResponse.cs | API response record for file metadata | Id, BucketId, FileName, ContentType, SizeBytes, Path, IsPublic, UploadedBy, UploadedAt | None | |
| 72 | [ ] | src/Modules/Storage/Foundry.Storage.Api/Contracts/Responses/PresignedUploadResponse.cs | API response records for presigned URLs | PresignedUploadResponse (FileId, UploadUrl, ExpiresAt); PresignedUrlResponse (Url, ExpiresAt) | None | |
| 73 | [ ] | src/Modules/Storage/Foundry.Storage.Api/Contracts/Responses/UploadResponse.cs | API response record for direct upload result | FileId, FileName, SizeBytes, ContentType, UploadedAt | None | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 74 | [ ] | src/Modules/Storage/Foundry.Storage.Api/Controllers/StorageController.cs | REST controller for all storage operations (buckets, files, presigned URLs) | Versioned route "api/v1/storage"; bucket CRUD (StorageWrite/StorageRead); file upload (multipart, 100MB limit, rate limited), download (redirect to presigned URL), delete, list (paginated); presigned URL endpoints for upload and download; string-to-enum parsing for Access/RetentionAction | Wolverine IMessageBus, ITenantContext, ICurrentUserService | |

## Test Files

### Domain Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 75 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Domain/Entities/StorageBucketTests.cs | Unit tests for StorageBucket entity | Create, content type matching (wildcard), file size validation, updates | |
| 76 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Domain/Entities/StoredFileTests.cs | Unit tests for StoredFile entity | Create, CreatePendingValidation, status transitions, metadata update | |
| 77 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Domain/Entities/RetentionPolicyTests.cs | Unit tests for RetentionPolicy value object | Construction, equality | |
| 78 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Domain/StorageBucketTests.cs | Additional domain tests for StorageBucket | Bucket behavior and validation | |

### Application Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 79 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/ApplicationExtensionsTests.cs | Tests for application DI registration | Service registration verification | |
| 80 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/CreateBucketHandlerTests.cs | Handler tests for CreateBucket | Happy path, duplicate name conflict | |
| 81 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/CreateBucketValidatorTests.cs | Validator tests for CreateBucketCommand | Name pattern, field requirements | |
| 82 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/DeleteBucketHandlerTests.cs | Handler tests for DeleteBucket | Deletion, non-empty bucket, force mode | |
| 83 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/DeleteBucketValidatorTests.cs | Validator tests for DeleteBucketCommand | Field requirements | |
| 84 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/DeleteFileHandlerTests.cs | Handler tests for DeleteFile | Deletion, not found, tenant mismatch | |
| 85 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/DeleteFileValidatorTests.cs | Validator tests for DeleteFileCommand | Field requirements | |
| 86 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/FileNameSanitizerTests.cs | Tests for FileNameSanitizer utility | Path stripping, dangerous chars, truncation, edge cases | |
| 87 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/GetBucketByNameHandlerTests.cs | Handler tests for GetBucketByName | Found, not found | |
| 88 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/GetFileByIdHandlerTests.cs | Handler tests for GetFileById | Found, not found, tenant check | |
| 89 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/GetFilesByBucketHandlerTests.cs | Handler tests for GetFilesByBucket | Pagination, path prefix filter | |
| 90 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/GetPresignedUrlHandlerTests.cs | Handler tests for GetPresignedUrl | URL generation, expiry clamping, file status check | |
| 91 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/GetUploadPresignedUrlHandlerTests.cs | Handler tests for GetUploadPresignedUrl | URL generation, pending file creation, scan scheduling | |
| 92 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/StorageMappingsTests.cs | Tests for StorageMappings extension methods | ToDto for files and buckets | |
| 93 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/UploadFileHandlerTests.cs | Handler tests for UploadFile | Upload flow, content type check, size check, scan | |
| 94 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/UploadFileValidatorTests.cs | Validator tests for UploadFileCommand | Magic bytes, blocked signatures, path traversal | |

### Application / Handlers Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 95 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/Handlers/CreateBucketHandlerTests.cs | Additional handler tests for CreateBucket | Bucket creation scenarios | |
| 96 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/Handlers/PresignedUrlHandlerTests.cs | Handler tests for presigned URL operations | Download and upload presigned URL generation | |

### Application / Services Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 97 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Application/Services/FileNameSanitizerTests.cs | Additional FileNameSanitizer tests | Sanitization edge cases | |

### Api Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 98 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Api/Contracts/RequestContractTests.cs | Contract tests for API request records | Request record construction and properties | |
| 99 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Api/Contracts/ResponseContractTests.cs | Contract tests for API response records | Response record construction and properties | |
| 100 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Api/Controllers/StorageControllerTests.cs | Controller tests for StorageController | Bucket CRUD, file operations, presigned URL endpoints | |
| 101 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Api/Extensions/ResultExtensionsTests.cs | Tests for Result-to-ActionResult extension methods | Success/failure mapping to HTTP responses | |

### Infrastructure Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 102 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Infrastructure/ClamAvFileScannerTests.cs | Tests for ClamAV file scanner | TCP protocol, scan results parsing, error handling | |
| 103 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Infrastructure/EfCoreConfigurationTests.cs | Tests for EF Core entity configurations | Schema, table names, indexes, relationships | |
| 104 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Infrastructure/LocalStorageProviderTests.cs | Tests for local filesystem storage provider | Upload, download, delete, path traversal protection | |
| 105 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Infrastructure/S3StorageProviderTests.cs | Tests for S3 storage provider | S3 operations, region bucket resolution | |

### Integration Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 106 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Integration/StorageBucketRepositoryTests.cs | Integration tests for StorageBucketRepository | CRUD operations against real database | |
| 107 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/Integration/StoredFileRepositoryTests.cs | Integration tests for StoredFileRepository | CRUD, pagination, path filtering against real database | |

### Other Test Files

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 108 | [ ] | tests/Modules/Storage/Foundry.Storage.Tests/GlobalUsings.cs | Global using directives for test project | Common test imports | |
