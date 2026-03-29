namespace BusLane.Services.Security;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[SupportedOSPlatform("macos")]
public sealed class MacOsBiometricAuthService : PlatformBiometricAuthService
{
    public MacOsBiometricAuthService()
        : this(new MacOsLocalAuthenticationAdapter())
    {
    }

    internal MacOsBiometricAuthService(IBiometricPromptAdapter adapter)
        : base(adapter)
    {
    }
}

[SupportedOSPlatform("macos")]
internal sealed class MacOsLocalAuthenticationAdapter : IBiometricPromptAdapter
{
    private const nint DeviceOwnerAuthenticationWithBiometricsPolicy = 1;
    private const nint ErrorUserCancel = -2;
    private const nint ErrorUserFallback = -3;
    private const nint ErrorSystemCancel = -4;
    private const nint ErrorPasscodeNotSet = -5;
    private const nint ErrorBiometryNotAvailable = -6;
    private const nint ErrorBiometryNotEnrolled = -7;
    private const nint ErrorBiometryLockout = -8;
    private const nint ErrorAppCancel = -9;
    private const nint ErrorNotInteractive = -1004;

    private static readonly IntPtr LaContextClass = objc_getClass("LAContext");
    private static readonly IntPtr NsStringClass = objc_getClass("NSString");
    private static readonly IntPtr AllocSelector = sel_registerName("alloc");
    private static readonly IntPtr InitSelector = sel_registerName("init");
    private static readonly IntPtr ReleaseSelector = sel_registerName("release");
    private static readonly IntPtr StringWithUtf8StringSelector = sel_registerName("stringWithUTF8String:");
    private static readonly IntPtr CanEvaluatePolicySelector = sel_registerName("canEvaluatePolicy:error:");
    private static readonly IntPtr EvaluatePolicySelector = sel_registerName("evaluatePolicy:localizedReason:reply:");
    private static readonly IntPtr InvalidateSelector = sel_registerName("invalidate");
    private static readonly IntPtr CodeSelector = sel_registerName("code");
    private static readonly IntPtr NsConcreteStackBlock = NativeLibrary.GetExport(NativeLibrary.Load("/usr/lib/libSystem.B.dylib"), "_NSConcreteStackBlock");
    private static readonly IntPtr BlockCopyPointer = NativeLibrary.GetExport(NativeLibrary.Load("/usr/lib/libSystem.B.dylib"), "_Block_copy");
    private static readonly IntPtr BlockReleasePointer = NativeLibrary.GetExport(NativeLibrary.Load("/usr/lib/libSystem.B.dylib"), "_Block_release");
    private static readonly BlockCopyDelegate BlockCopy = Marshal.GetDelegateForFunctionPointer<BlockCopyDelegate>(BlockCopyPointer);
    private static readonly BlockReleaseDelegate BlockRelease = Marshal.GetDelegateForFunctionPointer<BlockReleaseDelegate>(BlockReleasePointer);
    private static readonly ReplyCallbackDelegate ReplyCallbackHandler = ReplyCallback;
    private static readonly IntPtr BlockSignature = Marshal.StringToHGlobalAnsi("v@?c@");
    private static readonly IntPtr BlockDescriptorPointer = CreateBlockDescriptor();

    public Task<NativeBiometricAvailability> GetAvailabilityAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return Task.FromResult(NativeBiometricAvailability.Unavailable);
        }

        var context = CreateContext();
        try
        {
            var canEvaluate = objc_msgSend_bool_nint_out_IntPtr(
                context,
                CanEvaluatePolicySelector,
                DeviceOwnerAuthenticationWithBiometricsPolicy,
                out var errorPointer);

            if (canEvaluate != 0)
            {
                return Task.FromResult(NativeBiometricAvailability.Available);
            }

            var code = GetErrorCode(errorPointer);
            return Task.FromResult(code == ErrorBiometryNotEnrolled
                ? NativeBiometricAvailability.NotEnrolled
                : NativeBiometricAvailability.Unavailable);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public async Task<NativeBiometricPromptResult> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return NativeBiometricPromptResult.Unavailable;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var context = CreateContext();
        var tcs = new TaskCompletionSource<NativeBiometricPromptResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reasonPointer = objc_msgSend_string(NsStringClass, StringWithUtf8StringSelector, reason);
        var blockPointer = CreateReplyBlock(tcs);

        try
        {
            using var registration = ct.Register(() =>
            {
                try
                {
                    objc_msgSend_void(context, InvalidateSelector);
                }
                catch
                {
                    // Ignore cancellation cleanup failures.
                }
            });

            objc_msgSend_void_nint_IntPtr_IntPtr(
                context,
                EvaluatePolicySelector,
                DeviceOwnerAuthenticationWithBiometricsPolicy,
                reasonPointer,
                blockPointer);

            return await tcs.Task.WaitAsync(ct);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    private static IntPtr CreateContext()
    {
        var allocated = objc_msgSend(LaContextClass, AllocSelector);
        return objc_msgSend(allocated, InitSelector);
    }

    private static void ReleaseContext(IntPtr context)
    {
        if (context == IntPtr.Zero)
        {
            return;
        }

        try
        {
            objc_msgSend_void(context, InvalidateSelector);
        }
        catch
        {
            // Ignore invalidation errors during cleanup.
        }

        objc_msgSend_void(context, ReleaseSelector);
    }

    private static nint GetErrorCode(IntPtr errorPointer)
    {
        return errorPointer == IntPtr.Zero
            ? 0
            : objc_msgSend_nint(errorPointer, CodeSelector);
    }

    private static IntPtr CreateBlockDescriptor()
    {
        var descriptor = Marshal.AllocHGlobal(Marshal.SizeOf<BlockDescriptor>());
        Marshal.StructureToPtr(new BlockDescriptor
        {
            Reserved = UIntPtr.Zero,
            Size = (UIntPtr)Marshal.SizeOf<BlockLiteral>(),
            Signature = BlockSignature
        }, descriptor, fDeleteOld: false);
        return descriptor;
    }

    private static IntPtr CreateReplyBlock(TaskCompletionSource<NativeBiometricPromptResult> tcs)
    {
        var handle = GCHandle.Alloc(tcs);
        var sourceBlockPointer = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
        Marshal.StructureToPtr(new BlockLiteral
        {
            Isa = NsConcreteStackBlock,
            Flags = 1 << 30,
            Reserved = 0,
            Invoke = Marshal.GetFunctionPointerForDelegate(ReplyCallbackHandler),
            Descriptor = BlockDescriptorPointer,
            State = GCHandle.ToIntPtr(handle)
        }, sourceBlockPointer, fDeleteOld: false);

        var copiedBlock = BlockCopy(sourceBlockPointer);
        Marshal.FreeHGlobal(sourceBlockPointer);
        return copiedBlock;
    }

    private static void ReplyCallback(IntPtr blockPointer, byte success, IntPtr errorPointer)
    {
        try
        {
            var block = Marshal.PtrToStructure<BlockLiteral>(blockPointer);
            var handle = GCHandle.FromIntPtr(block.State);
            var tcs = (TaskCompletionSource<NativeBiometricPromptResult>)handle.Target!;
            handle.Free();

            var result = success != 0
                ? NativeBiometricPromptResult.Success
                : MapPromptResult(GetErrorCode(errorPointer));

            tcs.TrySetResult(result);
        }
        finally
        {
            BlockRelease(blockPointer);
        }
    }

    private static NativeBiometricPromptResult MapPromptResult(nint errorCode)
    {
        return errorCode switch
        {
            ErrorUserCancel or ErrorUserFallback or ErrorSystemCancel or ErrorAppCancel => NativeBiometricPromptResult.Cancelled,
            ErrorPasscodeNotSet or ErrorBiometryNotAvailable or ErrorBiometryNotEnrolled or ErrorBiometryLockout or ErrorNotInteractive => NativeBiometricPromptResult.Unavailable,
            _ => NativeBiometricPromptResult.Failed
        };
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr BlockCopyDelegate(IntPtr block);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void BlockReleaseDelegate(IntPtr block);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReplyCallbackDelegate(IntPtr blockPointer, byte success, IntPtr errorPointer);

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockDescriptor
    {
        public UIntPtr Reserved;
        public UIntPtr Size;
        public IntPtr Signature;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        public IntPtr Isa;
        public int Flags;
        public int Reserved;
        public IntPtr Invoke;
        public IntPtr Descriptor;
        public IntPtr State;
    }

    [SupportedOSPlatform("macos")]
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string className);

    [SupportedOSPlatform("macos")]
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string selectorName);

    [SupportedOSPlatform("macos")]
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [SupportedOSPlatform("macos")]
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_string(IntPtr receiver, IntPtr selector, string value);

    [SupportedOSPlatform("macos")]
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern byte objc_msgSend_bool_nint_out_IntPtr(IntPtr receiver, IntPtr selector, nint arg1, out IntPtr arg2);

    [SupportedOSPlatform("macos")]
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_nint_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, nint arg1, IntPtr arg2, IntPtr arg3);

    [SupportedOSPlatform("macos")]
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_nint(IntPtr receiver, IntPtr selector);

    [SupportedOSPlatform("macos")]
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void(IntPtr receiver, IntPtr selector);
}
