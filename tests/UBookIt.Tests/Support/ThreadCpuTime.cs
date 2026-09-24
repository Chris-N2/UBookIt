using System.Runtime.InteropServices;

namespace UBookIt.Tests.Support;

/// <summary>
/// The CPU time the calling thread has consumed, in a platform-specific unit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not a stopwatch:</b> wall-clock time includes every moment the thread was not running —
/// a GC pause, a preemption, another process taking the core. Under induced load a wall-clock
/// cost ratio failed its threshold in 23–51 of 200 trials even with interleaved minimum
/// sampling; the same sampling on this clock failed none (`stable-cost-measurement`, tasks 1.3).
/// </para>
/// <para>
/// <b>The unit differs by platform</b> — CPU cycles on Windows, nanoseconds on Linux and macOS —
/// so a reading is only meaningful divided by another reading taken on the same platform, and
/// only between two reads on the <b>same thread</b>. Callers compare ratios, never raw values.
/// </para>
/// <para>
/// <b>An unknown platform throws.</b> A caller must not fall back to a clock that measures
/// something else and pass on it.
/// </para>
/// </remarks>
public static class ThreadCpuTime
{
    /// <summary>What <see cref="Now"/> counts on this platform, for printing beside a figure.</summary>
    public static string Unit =>
        OperatingSystem.IsWindows() ? "cycles"
        : OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? "ns"
        : "unsupported";

    public static ulong Now()
    {
        if (OperatingSystem.IsWindows())
        {
            return QueryThreadCycleTime(GetCurrentThread(), out var cycles)
                ? cycles
                : throw new InvalidOperationException("QueryThreadCycleTime failed.");
        }

        if (OperatingSystem.IsLinux())
        {
            return ClockGetTime(LinuxClockThreadCpuTime);
        }

        if (OperatingSystem.IsMacOS())
        {
            return ClockGetTime(MacClockThreadCpuTime);
        }

        throw new PlatformNotSupportedException(
            $"No per-thread CPU clock is wired for {RuntimeInformation.OSDescription}. "
            + "Add one to ThreadCpuTime rather than falling back to wall-clock time.");
    }

    private const int LinuxClockThreadCpuTime = 3;
    private const int MacClockThreadCpuTime = 16;

    private static ulong ClockGetTime(int clock)
    {
        if (clock_gettime(clock, out var time) != 0)
        {
            throw new InvalidOperationException(
                $"clock_gettime({clock}) failed with errno {Marshal.GetLastPInvokeError()}.");
        }

        return (ulong)time.Seconds * 1_000_000_000UL + (ulong)time.Nanoseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TimeSpec
    {
        public long Seconds;
        public long Nanoseconds;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryThreadCycleTime(IntPtr thread, out ulong cycles);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("libc", SetLastError = true)]
    private static extern int clock_gettime(int clock, out TimeSpec time);
}
