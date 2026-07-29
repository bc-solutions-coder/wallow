namespace Wallow.Api.Tests.Extensions;

/// <summary>
/// Stand-in action methods whose <see cref="System.Reflection.MethodInfo"/> feeds the
/// operationId transformer tests. Only the method NAME matters to the transformer.
/// </summary>
internal static class FakeOperationIdActions
{
    /// <summary>A method name that repeats across several real controllers.</summary>
    internal static void GetById()
    {
    }

    /// <summary>Another method name that repeats across several real controllers.</summary>
    internal static void Create()
    {
    }
}
