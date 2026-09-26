using System;
using System.Diagnostics;
using System.Threading;

namespace Worms.Server
{
    public sealed class ServerStats
    {
        readonly Stopwatch _uptime = Stopwatch.StartNew();
        int _connections;

        public int Connections => Volatile.Read(ref _connections);
        public TimeSpan Uptime => _uptime.Elapsed;

        public void Opened() { Interlocked.Increment(ref _connections); }
        public void Closed() { Interlocked.Decrement(ref _connections); }
    }
}
