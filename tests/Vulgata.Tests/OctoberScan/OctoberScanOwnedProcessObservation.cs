using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Read-only, bounded observations of descendants of one exact test-owned native launch.</summary>
internal sealed class OctoberScanOwnedProcessObservation : IDisposable
{
    private readonly Process _root;
    private readonly int _rootId;
    private readonly DateTime _rootStarted;
    private readonly Action<string> _log;
    private readonly List<RetainedChild> _children = [];
    private bool _captured;
    private bool _disposed;

    internal OctoberScanOwnedProcessObservation(Process actualLaunch, Action<string> log)
    {
        _rootId = actualLaunch.Id;
        _rootStarted = actualLaunch.StartTime.ToUniversalTime();
        _root = Process.GetProcessById(_rootId);
        try { _ = _root.SafeHandle; } // Hold the exact process object; do not rely on a later PID lookup.
        catch { _root.Dispose(); throw; }
        _log = log;
    }

    internal void CaptureBeforeKill()
    {
        if (_captured || _disposed) return;
        _captured = true;
        var entries = new List<(int Id, int Parent, string Name)>();
        using var snapshot = CreateToolhelp32Snapshot(0x00000002, 0);
        if (snapshot.IsInvalid)
        {
            _log($"Owned descendant capture failed: win32={Marshal.GetLastWin32Error()}");
            return;
        }
        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
        if (!Process32First(snapshot, ref entry))
        {
            _log($"Owned descendant capture first-entry failed: win32={Marshal.GetLastWin32Error()}");
            return;
        }
        do
        {
            if (entries.Count >= 8192) { _log("Process ancestry snapshot truncated; capture is partial."); break; }
            entries.Add(((int)entry.ProcessId, (int)entry.ParentProcessId, entry.Executable ?? "unknown"));
        }
        while (Process32Next(snapshot, ref entry));

        var parents = new Queue<(int Id, DateTime Started)>();
        parents.Enqueue((_rootId, _rootStarted));
        var seen = new HashSet<int> { _rootId };
        while (parents.TryDequeue(out var parent))
        {
            foreach (var candidate in entries.Where(entry => entry.Parent == parent.Id))
            {
                if (!seen.Add(candidate.Id)) continue;
                if (_children.Count >= 32)
                {
                    _log("Owned descendant capture truncated at 32 retained children; capture is partial.");
                    Report("before-kill");
                    return;
                }
                Process? child = null;
                try
                {
                    child = Process.GetProcessById(candidate.Id);
                    _ = child.SafeHandle;
                    var started = child.StartTime.ToUniversalTime();
                    if (started < parent.Started)
                    {
                        _log($"Rejected stale ancestry PID={candidate.Id}, parent={parent.Id}");
                        child.Dispose();
                        continue;
                    }
                    _children.Add(new(child, candidate.Id, parent.Id, candidate.Name));
                    parents.Enqueue((candidate.Id, started));
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
                {
                    child?.Dispose();
                    _log($"Owned descendant capture partial: PID={candidate.Id}, parent={parent.Id}, type={exception.GetType().Name}, HResult=0x{exception.HResult:X8}");
                }
            }
        }
        Report("before-kill");
    }

    internal bool AllRetainedProcessesKernelSignaled =>
        !_disposed && _root.WaitForExit(0) && _children.All(child => child.Process.WaitForExit(0));

    internal void Report(string phase)
    {
        if (_disposed) return;
        _log($"Owned root phase={phase}; PID={_rootId}; exited={ObserveExit(_root)}; retainedChildren={_children.Count}; snapshotTaken={_captured}");
        foreach (var child in _children)
            _log($"Owned child phase={phase}; PID={child.Id}; parent={child.ParentId}; name={child.Name}; exited={ObserveExit(child.Process)}");
    }

    internal async Task ObserveFailureAsync()
    {
        if (_disposed) return;
        Report("cleanup-failed");
        var timer = Stopwatch.StartNew();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Task.WhenAll(_children.Select(async child =>
        {
            try
            {
                await child.Process.WaitForExitAsync(deadline.Token);
                _log($"Owned child exit observed after cleanup failure: PID={child.Id}; elapsedMs={timer.ElapsedMilliseconds}; exit={child.Process.ExitCode}");
            }
            catch (OperationCanceledException)
            {
                _log($"Owned child still unconfirmed at diagnostic deadline: PID={child.Id}; elapsedMs={timer.ElapsedMilliseconds}; exited={ObserveExit(child.Process)}");
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
            {
                _log($"Owned child observation failed: PID={child.Id}; type={exception.GetType().Name}; HResult=0x{exception.HResult:X8}");
            }
        }));
        Report("after-bounded-observation");
    }

    private static string ObserveExit(Process process)
    {
        try { return $"{process.HasExited}; kernelSignaled={process.WaitForExit(0)}"; }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception) { return "unknown:" + exception.GetType().Name; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var child in _children) child.Process.Dispose();
        _root.Dispose();
    }

    private sealed record RetainedChild(Process Process, int Id, int ParentId, string Name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(SafeFileHandle snapshot, ref ProcessEntry entry);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public UIntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string? Executable;
    }
}
