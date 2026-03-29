namespace BusLane.Services.Security;

using System.Reflection;

public sealed class WindowsHelloBiometricAuthService : PlatformBiometricAuthService
{
    public WindowsHelloBiometricAuthService()
        : this(new WindowsHelloPromptAdapter())
    {
    }

    internal WindowsHelloBiometricAuthService(IBiometricPromptAdapter adapter)
        : base(adapter)
    {
    }
}

internal sealed class WindowsHelloPromptAdapter : IBiometricPromptAdapter
{
    private const string UserConsentVerifierTypeName = "Windows.Security.Credentials.UI.UserConsentVerifier, Windows, ContentType=WindowsRuntime";
    private const string WindowsRuntimeExtensionsTypeName = "System.WindowsRuntimeSystemExtensions, System.Runtime.WindowsRuntime";

    public async Task<NativeBiometricAvailability> GetAvailabilityAsync(CancellationToken ct = default)
    {
        var verifierType = GetVerifierType();
        if (verifierType == null)
        {
            return NativeBiometricAvailability.Unavailable;
        }

        var operation = verifierType.GetMethod("CheckAvailabilityAsync", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        var result = await AwaitOperationAsync(operation, ct);

        return string.Equals(result?.ToString(), "Available", StringComparison.Ordinal)
            ? NativeBiometricAvailability.Available
            : NativeBiometricAvailability.Unavailable;
    }

    public async Task<NativeBiometricPromptResult> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var verifierType = GetVerifierType();
        if (verifierType == null)
        {
            return NativeBiometricPromptResult.Unavailable;
        }

        var operation = verifierType.GetMethod("RequestVerificationAsync", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, [reason]);
        var result = await AwaitOperationAsync(operation, ct);
        var status = result?.ToString();

        return status switch
        {
            "Verified" => NativeBiometricPromptResult.Success,
            "Canceled" => NativeBiometricPromptResult.Cancelled,
            "DeviceBusy" or "DisabledByPolicy" or "DeviceNotPresent" or "NotConfigured" => NativeBiometricPromptResult.Unavailable,
            "RetriesExhausted" => NativeBiometricPromptResult.Failed,
            _ => NativeBiometricPromptResult.Unavailable
        };
    }

    private static Type? GetVerifierType()
    {
        return OperatingSystem.IsWindows()
            ? Type.GetType(UserConsentVerifierTypeName, throwOnError: false)
            : null;
    }

    private static async Task<object?> AwaitOperationAsync(object? operation, CancellationToken ct)
    {
        if (operation == null)
        {
            return null;
        }

        if (operation is Task task)
        {
            await task.WaitAsync(ct);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        var operationType = operation.GetType();
        var asyncInterface = operationType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.Name.StartsWith("IAsyncOperation`1", StringComparison.Ordinal));

        if (asyncInterface == null)
        {
            return null;
        }

        var resultType = asyncInterface.GetGenericArguments()[0];
        var extensionsType = Type.GetType(WindowsRuntimeExtensionsTypeName, throwOnError: false);
        if (extensionsType != null)
        {
            var asTaskMethod = extensionsType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "AsTask" || !method.IsGenericMethodDefinition)
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != 1)
                    {
                        return false;
                    }

                    var parameterType = parameters[0].ParameterType;
                    return parameterType.IsGenericType
                        && string.Equals(
                            parameterType.GetGenericTypeDefinition().FullName,
                            "Windows.Foundation.IAsyncOperation`1",
                            StringComparison.Ordinal);
                });

            if (asTaskMethod != null)
            {
                var taskObject = (Task?)asTaskMethod.MakeGenericMethod(resultType).Invoke(null, [operation]);
                if (taskObject != null)
                {
                    await taskObject.WaitAsync(ct);
                    return taskObject.GetType().GetProperty("Result")?.GetValue(taskObject);
                }
            }
        }

        while (!ct.IsCancellationRequested)
        {
            var status = operationType.GetProperty("Status")?.GetValue(operation)?.ToString();
            if (string.Equals(status, "Completed", StringComparison.Ordinal))
            {
                return operationType.GetMethod("GetResults")?.Invoke(operation, null);
            }

            if (string.Equals(status, "Canceled", StringComparison.Ordinal)
                || string.Equals(status, "Error", StringComparison.Ordinal))
            {
                return null;
            }

            await Task.Delay(50, ct);
        }

        return null;
    }
}
