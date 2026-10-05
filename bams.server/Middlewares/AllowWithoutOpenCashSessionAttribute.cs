namespace bams.server.Middlewares;

/// <summary>Marks the operation that creates an officer's required cash session as exempt from the session gate.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AllowWithoutOpenCashSessionAttribute : Attribute { }
