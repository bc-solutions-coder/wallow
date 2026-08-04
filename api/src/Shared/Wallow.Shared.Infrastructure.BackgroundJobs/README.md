# Wallow.Shared.Infrastructure.BackgroundJobs

Hangfire integration for scheduled and recurring work.

- `HangfireJobScheduler` — the `IJobScheduler` implementation
- `BackgroundJobsExtensions` — DI registration

The API host registers the recurring jobs themselves and exposes the Hangfire dashboard; see
[`../../Wallow.Api/README.md`](../../Wallow.Api/README.md).
