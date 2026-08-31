using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace EMS.Agent.Helpers;

/// <summary>
/// Runs a process inside the active interactive user's session from the SYSTEM
/// service. Per-user application uninstallers write to the signed-in user's
/// HKCU and %AppData%; run as SYSTEM they hit SYSTEM's own hive instead and
/// silently do nothing (while still returning exit 0). This uses the standard
/// WTS + CreateProcessAsUser sequence to launch them as the real user, and
/// waits for completion so the caller can verify the result.
/// </summary>
[SupportedOSPlatform("windows")]
public static class UserProcessHelper
{
    public sealed record RunAsUserResult(bool Launched, int ExitCode, bool TimedOut, string? Error);

    public static Task<RunAsUserResult> RunAsync(
        string fileName, string arguments, string? workingDirectory,
        TimeSpan timeout, ILogger logger, CancellationToken cancellationToken = default)
        => Task.Run(() => Run(fileName, arguments, workingDirectory, timeout, logger), cancellationToken);

    private static RunAsUserResult Run(
        string fileName, string arguments, string? workingDirectory, TimeSpan timeout, ILogger logger)
    {
        var sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF)
        {
            return new RunAsUserResult(false, -1, false, "No active console session (no one is signed in).");
        }

        if (!WTSQueryUserToken(sessionId, out var userToken))
        {
            return new RunAsUserResult(false, -1, false,
                $"WTSQueryUserToken failed ({Marshal.GetLastWin32Error()}).");
        }

        var dupToken = IntPtr.Zero;
        var envBlock = IntPtr.Zero;
        try
        {
            var sa = new SECURITY_ATTRIBUTES();
            sa.nLength = Marshal.SizeOf(sa);

            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED, ref sa,
                    SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out dupToken))
            {
                return new RunAsUserResult(false, -1, false,
                    $"DuplicateTokenEx failed ({Marshal.GetLastWin32Error()}).");
            }

            // Best-effort: give the child the user's environment (PATH, %AppData%…).
            var hasEnv = CreateEnvironmentBlock(out envBlock, dupToken, false);

            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = @"winsta0\default";

            var commandLine = new StringBuilder();
            commandLine.Append('"').Append(fileName).Append('"');
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                commandLine.Append(' ').Append(arguments);
            }

            // CREATE_UNICODE_ENVIRONMENT is only valid with a non-null Unicode
            // environment block; setting it with a null block makes
            // CreateProcessAsUser fail. Only pass it when the block was built.
            uint flags = CREATE_NO_WINDOW;
            if (hasEnv && envBlock != IntPtr.Zero)
            {
                flags |= CREATE_UNICODE_ENVIRONMENT;
            }

            // Only pass a working directory that actually exists; a missing/odd
            // one (trailing slash, removed folder) makes CreateProcessAsUser fail.
            var workDir = workingDirectory?.Trim().TrimEnd('\\');
            if (string.IsNullOrWhiteSpace(workDir) || !Directory.Exists(workDir))
            {
                workDir = null;
            }

            var launched = CreateProcessAsUser(
                dupToken, null, commandLine, IntPtr.Zero, IntPtr.Zero, false, flags,
                envBlock == IntPtr.Zero ? IntPtr.Zero : envBlock, workDir, ref si, out var pi);

            if (!launched)
            {
                return new RunAsUserResult(false, -1, false,
                    $"CreateProcessAsUser failed ({Marshal.GetLastWin32Error()}).");
            }

            try
            {
                var waitMs = timeout.TotalMilliseconds >= int.MaxValue
                    ? INFINITE
                    : (uint)Math.Max(0, timeout.TotalMilliseconds);
                var waited = WaitForSingleObject(pi.hProcess, waitMs);
                if (waited == WAIT_TIMEOUT)
                {
                    return new RunAsUserResult(true, -1, true, null);
                }

                GetExitCodeProcess(pi.hProcess, out var exit);
                return new RunAsUserResult(true, unchecked((int)exit), false, null);
            }
            finally
            {
                if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "RunAsActiveUser threw.");
            return new RunAsUserResult(false, -1, false, ex.Message);
        }
        finally
        {
            if (envBlock != IntPtr.Zero) DestroyEnvironmentBlock(envBlock);
            if (dupToken != IntPtr.Zero) CloseHandle(dupToken);
            CloseHandle(userToken);
        }
    }

    private const uint MAXIMUM_ALLOWED = 0x02000000;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint INFINITE = 0xFFFFFFFF;
    private const uint WAIT_TIMEOUT = 0x00000102;

    private enum SECURITY_IMPERSONATION_LEVEL { SecurityAnonymous, SecurityIdentification, SecurityImpersonation, SecurityDelegation }
    private enum TOKEN_TYPE { TokenPrimary = 1, TokenImpersonation }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken, uint dwDesiredAccess, ref SECURITY_ATTRIBUTES lpTokenAttributes,
        SECURITY_IMPERSONATION_LEVEL impersonationLevel, TOKEN_TYPE tokenType, out IntPtr phNewToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        IntPtr hToken, string? lpApplicationName, StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles,
        uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
