using System.Linq.Expressions;
using Wallow.Shared.Kernel.BackgroundJobs;
using Hangfire;

namespace Wallow.Shared.Infrastructure.BackgroundJobs;

public sealed class HangfireJobScheduler : IJobScheduler
{
    public string Enqueue(Expression<Func<Task>> job) =>
        BackgroundJob.Enqueue(job);

    public string Enqueue<T>(Expression<Func<T, Task>> job) =>
        BackgroundJob.Enqueue(job);

    public void AddRecurring(string id, string cron, Expression<Func<Task>> job) =>
        RecurringJob.AddOrUpdate(id, job, cron);

    public void RemoveRecurring(string id) =>
        RecurringJob.RemoveIfExists(id);
}
