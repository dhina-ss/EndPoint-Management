namespace EMS.API.Middleware;

/// <summary>
/// Marks a controller or action as requiring X-Device-Id / X-Device-Token
/// headers, enforced by <see cref="DeviceAuthenticationMiddleware"/>.
/// The registration endpoint must NOT carry this attribute — it is the
/// anonymous first contact that issues the token.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireDeviceAuthAttribute : Attribute
{
}
