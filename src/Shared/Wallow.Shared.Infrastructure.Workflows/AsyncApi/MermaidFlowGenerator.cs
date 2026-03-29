using System.Text;

namespace Wallow.Shared.Infrastructure.Workflows.AsyncApi;

public static class MermaidFlowGenerator
{
    public static string Generate(IReadOnlyList<EventFlowInfo> flows)
    {
        if (flows.Count == 0)
        {
            return "flowchart LR";
        }

        StringBuilder sb = new();
        sb.AppendLine("flowchart LR");
        sb.AppendLine();
        sb.AppendLine("    exchange{{Message Bus}}");

        IOrderedEnumerable<IGrouping<string, EventFlowInfo>> grouped = flows
            .GroupBy(f => f.SourceModule)
            .OrderBy(g => g.Key);

        foreach (IGrouping<string, EventFlowInfo> group in grouped)
        {
            string module = group.Key;
            string producerId = Sanitize(module);

            sb.AppendLine();
            sb.AppendLine($"    subgraph {producerId}_sub[\"{module}\"]");
            sb.AppendLine($"        {producerId}([\"{module}\"])");
            sb.AppendLine("    end");

            foreach (EventFlowInfo flow in group.OrderBy(f => f.EventTypeName))
            {
                string eventLabel = flow.SagaTrigger
                    ? $"{flow.EventTypeName} ⚡"
                    : flow.EventTypeName;

                sb.AppendLine($"    {producerId} -->|\"{eventLabel}\"| exchange");

                foreach (string consumerModule in flow.ConsumerModules.OrderBy(m => m))
                {
                    string consumerId = Sanitize(consumerModule) + "_cons";
                    sb.AppendLine($"    exchange -->|\"{eventLabel}\"| {consumerId}[\"{consumerModule}\"]");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string Sanitize(string name) =>
        new(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
