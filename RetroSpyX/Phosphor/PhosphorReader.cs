// RetroSpy, Phosphor edition: reads the controller a SuperStation running Phosphor says is active.
// Copyright (C) 2026 f3bandit. GPL-3.0, as the rest of RetroSpy (see LICENSE).
//
// The input itself comes the way RetroSpy's MiSTer source gets it (its MiSTer program, retrospy, on /dev/input/jsN,
// over SSH, read by MiSTerReader: buttons b0.., axes x, y, z, rx...). Every two seconds Phosphor is asked again
// which controller to show; when that changes (another player 1, a controller plugged in) the reader moves to it
// and StatusChanged says so, so the window can draw the new controller.

using Avalonia.Threading;
using RetroSpy.Readers;
using Renci.SshNet;
using System;
using System.Globalization;
using System.Threading;

namespace RetroSpy.Phosphor
{
    public sealed class PhosphorReader : IControllerReader, IDisposable
    {
        public event EventHandler<ControllerStateEventArgs>? ControllerStateChanged;
        public event EventHandler? ControllerDisconnected;
        /// <summary>On the UI thread: the SS1's answer changed (the controller shown, or its layout).</summary>
        public event EventHandler? StatusChanged;

        public const string ReaderCommand = "/media/fat/retrospy/retrospy /dev/input/js{0}";

        private readonly string _host, _user, _password;
        private readonly int _player;                 // 0: the active controller, 1-4: that player's
        private readonly object _lock = new();
        private SshClient? _info;
        private SSHControllerReader? _inner;
        private Timer? _poll;
        private bool _finished;

        public PhosphorStatus? Status { get; private set; }
        public PhosphorController? Controller { get; private set; }
        public PhosphorLayout? Layout => Controller != null && Status != null && Status.Layouts.TryGetValue(Controller.LayoutKey, out var l) ? l : null;

        public PhosphorReader(string host, string user, string password, int player)
        {
            _host = host; _user = user; _password = password; _player = player;
            _info = new SshClient(host, user, password);
            _info.ConnectionInfo.Timeout = TimeSpan.FromSeconds(6);
            _info.Connect();
            var st = PhosphorClient.Query(_info) ?? throw new InvalidOperationException(
                "This doesn't look like a SuperStation running Phosphor (phos wasn't found). Use the MiSTer source instead.");
            if (!st.RetroSpyInstalled)
                throw new InvalidOperationException("RetroSpy's MiSTer program isn't installed on the SuperStation yet. In Phosphor: Settings > Controllers > RetroSpy > Install.");
            var c = st.Pick(_player) ?? throw new InvalidOperationException("No controller is connected to the SuperStation.");
            Status = st;
            Controller = c;
            Watch(c.Js);
            _poll = new Timer(_ => Poll(), null, 2000, 2000);
        }

        private void Watch(int js)
        {
            var old = _inner;
            _inner = null;
            old?.Finish();
            var r = new SSHControllerReader(_host, ReaderCommand, MiSTerReader.ReadFromPacket, _user, _password,
                                            js.ToString(CultureInfo.InvariantCulture), 5000, true);
            r.ControllerStateChanged += (s, e) => ControllerStateChanged?.Invoke(this, e);
            r.ControllerDisconnected += (s, e) => { if (ReferenceEquals(s, _inner)) ControllerDisconnected?.Invoke(this, EventArgs.Empty); };
            _inner = r;
        }

        private void Poll()
        {
            if (!Monitor.TryEnter(_lock)) return;
            try
            {
                if (_finished || _info == null) return;
                if (!_info.IsConnected) _info.Connect();
                var st = PhosphorClient.Query(_info);
                if (st == null) return;
                var c = st.Pick(_player);
                if (c == null) return;
                bool moved = Controller == null || c.Js != Controller.Js;
                bool redraw = moved || c.LayoutKey != Controller!.LayoutKey || c.Name != Controller.Name || c.Player != Controller.Player;
                Status = st;
                Controller = c;
                if (moved)
                {
                    Dispatcher.UIThread.Post(() => { if (!_finished) Watch(c.Js); });
                }
                if (redraw)
                {
                    Dispatcher.UIThread.Post(() => { if (!_finished) StatusChanged?.Invoke(this, EventArgs.Empty); });
                }
            }
            catch (Exception)
            {
                // the SS1 restarting or the network dropping: asked again in two seconds
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }

        public void Finish()
        {
            _finished = true;
            _poll?.Dispose();
            _poll = null;
            _inner?.Finish();
            _inner = null;
            lock (_lock)
            {
                try { _info?.Disconnect(); } catch (Exception) { }
                _info?.Dispose();
                _info = null;
            }
        }

        public void Dispose() => Finish();
    }
}
