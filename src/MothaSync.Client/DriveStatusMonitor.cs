using System;
using System.IO;
using System.Threading;
using Avalonia.Threading;

namespace MothaSync.Client;

public static class DriveStatusMonitor
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    private static readonly object Gate = new();
    private static Timer? _timer;
    private static bool _sourceConnected;
    private static bool _destinationConnected;

    public static bool SourceConnected
    {
        get
        {
            lock (Gate)
            {
                return _sourceConnected;
            }
        }
    }

    public static bool DestinationConnected
    {
        get
        {
            lock (Gate)
            {
                return _destinationConnected;
            }
        }
    }

    public static event Action? Changed;

    public static void Start()
    {
        lock (Gate)
        {
            if (_timer is not null)
            {
                return;
            }

            _timer = new Timer(_ => Poll(), null, TimeSpan.Zero, Interval);
        }
    }

    private static void Poll()
    {
        var settings = SettingsStore.Load();
        var source = Directory.Exists(settings.SourcePath);
        var destination = Directory.Exists(settings.DestinationPath);

        bool changed;
        lock (Gate)
        {
            changed = source != _sourceConnected || destination != _destinationConnected;
            _sourceConnected = source;
            _destinationConnected = destination;
        }

        if (changed)
        {
            Dispatcher.UIThread.Post(() => Changed?.Invoke());
        }
    }
}
