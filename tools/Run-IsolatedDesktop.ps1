param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [string]$Arguments = '',
    [int]$TimeoutSeconds = 600
)

# Create a desktop in the current window station without switching to it.
# A GUI child launched there cannot become foreground on the user's desktop.
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public static class IsolatedDesktopProcess
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfo
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
        public int dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ProcessInformation
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateDesktop(string name, string device, IntPtr devMode, int flags, uint desiredAccess, IntPtr securityAttributes);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcess(string applicationName, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, int creationFlags, IntPtr environment,
        string currentDirectory, ref StartupInfo startupInfo, out ProcessInformation processInformation);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool TerminateProcess(IntPtr process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    public static int Run(string executable, string arguments, int timeoutSeconds)
    {
        string desktopName = "WormsBuild_" + Guid.NewGuid().ToString("N");
        IntPtr desktop = CreateDesktop(desktopName, null, IntPtr.Zero, 0, 0x01FF, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDesktop failed");
        try
        {
            var startup = new StartupInfo { cb = Marshal.SizeOf(typeof(StartupInfo)), lpDesktop = desktopName };
            string command = "\"" + executable + "\"" + (String.IsNullOrEmpty(arguments) ? "" : " " + arguments);
            var line = new StringBuilder(command);
            ProcessInformation process;
            if (!CreateProcess(executable, line, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero,
                Environment.CurrentDirectory, ref startup, out process))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess failed");
            try
            {
                uint wait = WaitForSingleObject(process.hProcess, checked((uint)timeoutSeconds * 1000u));
                if (wait == 0x102)
                {
                    TerminateProcess(process.hProcess, 124);
                    throw new TimeoutException("Isolated process timed out");
                }
                if (wait != 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "WaitForSingleObject failed");
                uint code;
                if (!GetExitCodeProcess(process.hProcess, out code))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "GetExitCodeProcess failed");
                return unchecked((int)code);
            }
            finally { CloseHandle(process.hThread); CloseHandle(process.hProcess); }
        }
        finally { CloseDesktop(desktop); }
    }
}
'@

$code = [IsolatedDesktopProcess]::Run($Executable, $Arguments, $TimeoutSeconds)
Write-Output "Isolated process exit code: $code"
exit $code
