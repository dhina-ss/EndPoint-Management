using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EMS.Agent.Helpers;

/// <summary>
/// Queries Windows Terminal Services sessions (active console/RDP domain users)
/// and launches the per-user tracker process directly into an active user's
/// desktop session from the LocalSystem Windows Service.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WtsSessionHelper
{
    private const int WTS_CURRENT_SERVER_HANDLE = 0;
    private const int WTSUserName = 5;
    private const int WTSDomainName = 7;

    private const uint TOKEN_ALL_ACCESS = 0xF01FF;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    public sealed record ActiveSession(int SessionId, string? Username);

    public static IReadOnlyList<ActiveSession> GetActiveUserSessions()
    {
        var result = new List<ActiveSession>();
        if (!WTSEnumerateSessionsW(IntPtr.Zero, 0, 1, out var pSessionInfo, out var count))
        {
            return result;
        }

        try
        {
            var structSize = Marshal.SizeOf<WTS_SESSION_INFO>();
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<WTS_SESSION_INFO>(pSessionInfo + i * structSize);
                if (info.SessionId > 0 && info.State == WTS_CONNECTSTATE_CLASS.WTSActive)
                {
                    result.Add(new ActiveSession(info.SessionId, GetSessionUsername(info.SessionId)));
                }
            }
        }
        finally
        {
            WTSFreeMemory(pSessionInfo);
        }

        return result;
    }

    public static string? GetSessionUsername(int sessionId)
    {
        if (sessionId <= 0) return null;

        var user = QuerySessionString(sessionId, WTSUserName);
        if (string.IsNullOrWhiteSpace(user)) return null;

        var domain = QuerySessionString(sessionId, WTSDomainName);
        return string.IsNullOrWhiteSpace(domain) ? user : $@"{domain}\{user}";
    }

    public static bool LaunchInUserSession(int sessionId, string exePath, string arguments, ILogger logger)
    {
        IntPtr userToken = IntPtr.Zero;
        IntPtr primaryToken = IntPtr.Zero;
        IntPtr envBlock = IntPtr.Zero;

        try
        {
            if (!WTSQueryUserToken(sessionId, out userToken))
            {
                var err = Marshal.GetLastWin32Error();
                logger.LogDebug("WTSQueryUserToken failed for session {SessionId} (error {Error}).", sessionId, err);
                return false;
            }

            var sa = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>() };
            if (!DuplicateTokenEx(userToken, TOKEN_ALL_ACCESS, ref sa, SecurityImpersonation, TokenPrimary, out primaryToken))
            {
                logger.LogWarning(new Win32Exception(Marshal.GetLastWin32Error()),
                    "DuplicateTokenEx failed for session {SessionId}.", sessionId);
                return false;
            }

            CreateEnvironmentBlock(out envBlock, primaryToken, false);

            var si = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFO>(),
                lpDesktop = @"winsta0\default"
            };

            var cmdLine = $"\"{exePath}\" {arguments}";
            var flags = CREATE_NO_WINDOW | (envBlock != IntPtr.Zero ? CREATE_UNICODE_ENVIRONMENT : 0);

            if (!CreateProcessAsUserW(
                primaryToken,
                exePath,
                cmdLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                flags,
                envBlock,
                Path.GetDirectoryName(exePath),
                ref si,
                out var pi))
            {
                logger.LogWarning(new Win32Exception(Marshal.GetLastWin32Error()),
                    "CreateProcessAsUser failed for session {SessionId}.", sessionId);
                return false;
            }

            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            logger.LogInformation("Launched per-user tracker in Windows session {SessionId}.", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not launch tracker into session {SessionId}.", sessionId);
            return false;
        }
        finally
        {
            if (envBlock != IntPtr.Zero) DestroyEnvironmentBlock(envBlock);
            if (primaryToken != IntPtr.Zero) CloseHandle(primaryToken);
            if (userToken != IntPtr.Zero) CloseHandle(userToken);
        }
    }

    private static string? QuerySessionString(int sessionId, int infoClass)
    {
        if (!WTSQuerySessionInformationW(IntPtr.Zero, sessionId, infoClass, out var buffer, out var bytes))
        {
            return null;
        }

        try
        {
            if (buffer == IntPtr.Zero || bytes <= 2) return null;
            return Marshal.PtrToStringUni(buffer)?.Trim();
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive,
        WTSConnected,
        WTSConnectQuery,
        WTSShadow,
        WTSDisconnected,
        WTSIdle,
        WTSListen,
        WTSReset,
        WTSDown,
        WTSInit
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WTS_SESSION_INFO
    {
        public int SessionId;
        public IntPtr pWinStationName;
        public WTS_CONNECTSTATE_CLASS State;
    }

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
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSEnumerateSessionsW(
        IntPtr hServer, int reserved, int version, out IntPtr ppSessionInfo, out int pCount);

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSQuerySessionInformationW(
        IntPtr hServer, int sessionId, int wtsInfoClass, out IntPtr ppBuffer, out int pBytesReturned);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(int sessionId, out IntPtr phToken);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken, uint dwDesiredAccess, ref SECURITY_ATTRIBUTES lpTokenAttributes,
        int impersonationLevel, int tokenType, out IntPtr phNewToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUserW(
        IntPtr hToken, string? lpApplicationName, string? lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles,
        uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
