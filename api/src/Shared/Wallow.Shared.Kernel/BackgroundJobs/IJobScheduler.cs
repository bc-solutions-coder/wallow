using System.Linq.Expressions;

namespace Wallow.Shared.Kernel.BackgroundJobs;

public interface IJobScheduler
{
    string Enqueue(Expression<Func<Task>> job);
    string Enqueue<T>(Expression<Func<T, Task>> job);
    void AddRecurring(string id, string cron, Expression<Func<Task>> job);
    void RemoveRecurring(string id);
}
