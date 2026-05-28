using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// Windows Job Object を使って子プロセス (Python ヘルパー等) を親プロセスに紐付ける。
    /// 親が異常終了/強制 Kill されても、Job が閉じられた瞬間に子も自動 Kill される。
    /// これによりゾンビ Python プロセス問題を根絶する。
    ///
    /// 使い方:
    ///   1. アプリ起動時 (Program.Main) に <see cref="EnsureInitialized"/> を呼ぶ
    ///   2. Process.Start 後すぐに <see cref="AssignProcess"/> を呼ぶ
    /// </summary>
    public static class ChildProcessJob
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType,
            ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo, int cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9,
        }

        [Flags]
        private enum JOBOBJECTLIMIT : uint
        {
            KillOnJobClose = 0x00002000,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public JOBOBJECTLIMIT LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        private static IntPtr _hJob = IntPtr.Zero;
        private static readonly object _lock = new();
        private static bool _initialized = false;
        private static bool _enabled = true;

        /// <summary>Job Object を 1 度だけ作成して KILL_ON_JOB_CLOSE 属性を設定</summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;
                _initialized = true;
                try
                {
                    _hJob = CreateJobObject(IntPtr.Zero, null);
                    if (_hJob == IntPtr.Zero)
                    {
                        _enabled = false;
                        Debug.WriteLine($"CreateJobObject failed: err={Marshal.GetLastWin32Error()}");
                        return;
                    }

                    var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                    info.BasicLimitInformation.LimitFlags = JOBOBJECTLIMIT.KillOnJobClose;

                    int len = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                    if (!SetInformationJobObject(_hJob, JobObjectInfoType.ExtendedLimitInformation, ref info, len))
                    {
                        _enabled = false;
                        Debug.WriteLine($"SetInformationJobObject failed: err={Marshal.GetLastWin32Error()}");
                        CloseHandle(_hJob);
                        _hJob = IntPtr.Zero;
                    }
                }
                catch (Exception ex)
                {
                    _enabled = false;
                    Debug.WriteLine($"JobObject init failed: {ex.Message}");
                }
            }
        }

        /// <summary>子プロセスを Job に紐付ける。親プロセスが死ぬと自動で kill される</summary>
        public static bool AssignProcess(Process child)
        {
            if (!_enabled || _hJob == IntPtr.Zero) return false;
            try
            {
                if (child.HasExited) return false;
                return AssignProcessToJobObject(_hJob, child.Handle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AssignProcessToJobObject failed: {ex.Message}");
                return false;
            }
        }
    }
}
