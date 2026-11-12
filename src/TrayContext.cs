using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoCopy
{
    internal sealed class MessageWindow : NativeWindow, IDisposable
    {
        internal Action OpenSettings, Exit;
        internal MessageWindow() { CreateHandle(new CreateParams { Caption = "AutoCopy.MessageWindow" }); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == Native.OpenSettingsMessage) { if (OpenSettings != null) OpenSettings(); }
            else if (message.Msg == Native.ExitMessage) { if (Exit != null) Exit(); }
            else base.WndProc(ref message);
        }
        public void Dispose() { DestroyHandle(); }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly SettingsStore store;
        private readonly AppSettings settings;
        private readonly Control dispatcher = new Control();
        private readonly NotifyIcon tray;
        private readonly TrayMenu menu;
        private readonly CopyFeedbackSound feedbackSound;
        private readonly SelectionMonitor monitor;
        private readonly SelectionReader reader;
        private readonly MessageWindow window;
        private readonly Icon activeIcon, pausedIcon;
        private readonly int ownId = Process.GetCurrentProcess().Id;
        private readonly System.Collections.Generic.List<Timer> pendingTimers = new System.Collections.Generic.List<Timer>();
        private SettingsForm form;
        private CopyToast toast;
        private bool stopping;

        internal TrayContext(SettingsStore store)
        {
            this.store = store; settings = store.Load();
            IntPtr handle = dispatcher.Handle;
            activeIcon = AppIcon.Create(true); pausedIcon = AppIcon.Create(false);
            menu = new TrayMenu(Toggle, OpenSettings, ExitThread);
            feedbackSound = new CopyFeedbackSound();
            tray = new NotifyIcon { Text = "CtrlCopy", Icon = activeIcon, Visible = true };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Toggle(); else if(e.Button == MouseButtons.Right) menu.ShowAt(Cursor.Position, settings.Enabled); };
            window = new MessageWindow { OpenSettings = OpenSettings, Exit = ExitThread };
            monitor = new SelectionMonitor();
            reader = new SelectionReader(Current);
            monitor.Requested += request => Dispatch(() => Queue(request));
            reader.Read += result => Dispatch(() => ProcessResult(result));
            UpdateState();
            Diagnostics.Write("started hooks=" + monitor.Generation);
        }
        private void Dispatch(Action action)
        {
            if (stopping || dispatcher.IsDisposed) return;
            try { dispatcher.BeginInvoke(action); } catch (InvalidOperationException) { }
        }
        private bool Current(CopyRequest request)
        {
            return !stopping && SelectionPolicy.IsCurrent(request, monitor.Generation, Native.GetForegroundWindow(), Native.GetClipboardSequenceNumber());
        }
        private bool Allowed(CopyRequest request)
        {
            if (!settings.Enabled || !SelectionPolicy.ShouldRead(request, settings) || !Current(request)) return false;
            int id; string process = Native.ProcessName(request.Window, out id);
            return !SelectionPolicy.IsExcluded(process, id, ownId, settings, Native.WindowClass(request.Window));
        }
        private void Queue(CopyRequest request)
        {
            bool allowed = Allowed(request);
            Diagnostics.Write("request " + request.Kind + " allowed=" + allowed + " generation=" + request.Generation + "/" + monitor.Generation + " sequence=" + request.ClipboardSequence + "/" + Native.GetClipboardSequenceNumber());
            if (allowed) reader.Queue(request);
        }
        private void ProcessResult(SelectionResult result)
        {
            Diagnostics.Write("result textLength=" + (result.Text == null ? 0 : result.Text.Length) + " password=" + result.Password + " context=" + result.TextContext + " allowed=" + Allowed(result.Request));
            if (result.Password || !Allowed(result.Request)) return;
            if (!string.IsNullOrEmpty(result.Text)) WriteClipboard(result.Request, result.Text, 0);
            else if (SelectionPolicy.ShouldFallback(result.Request, result.SelectionAvailable, result.Failed, result.TextContext)) FallbackCopy(result.Request, 0);
        }
        private void Later(int milliseconds, Action action)
        {
            if (stopping) return;
            var timer = new Timer { Interval = milliseconds };
            pendingTimers.Add(timer);
            timer.Tick += (s, e) => { timer.Stop(); pendingTimers.Remove(timer); timer.Dispose(); if (!stopping) action(); };
            timer.Start();
        }
        private void WriteClipboard(CopyRequest request, string text, int attempt)
        {
            if (!Allowed(request) || Native.KeyDown(1)) return;
            try
            {
                Diagnostics.Write("clipboard inspect attempt=" + attempt);
                if (Clipboard.ContainsText(TextDataFormat.UnicodeText) && Clipboard.GetText(TextDataFormat.UnicodeText) == text) return;
                Diagnostics.Write("clipboard inspected current=" + Allowed(request));
                if (!Allowed(request)) return;
                var data = new DataObject(); data.SetText(text, TextDataFormat.UnicodeText);
                Diagnostics.Write("clipboard write start");
                Clipboard.SetDataObject(data, true, 1, 0);
                Diagnostics.Write("clipboard write complete");
                NotifyCopied();
            }
            catch (ExternalException ex) { Diagnostics.Write("clipboard error " + ex.ErrorCode.ToString("X")); if (attempt < 5) Later(45, () => WriteClipboard(request, text, attempt + 1)); }
        }
        private void FallbackCopy(CopyRequest request, int attempt)
        {
            if (!Allowed(request)) return;
            if (Native.InputBusy)
            {
                if (attempt < 10) Later(60, () => FallbackCopy(request, attempt + 1));
                return;
            }
            if (Native.SendCopy()) PollCopy(request, 0);
        }
        private void PollCopy(CopyRequest request, int attempt)
        {
            Later(40, () =>
            {
                if (!settings.Enabled || request.Generation != monitor.Generation || request.Window != Native.GetForegroundWindow()) return;
                try
                {
                    if (Native.GetClipboardSequenceNumber() != request.ClipboardSequence && Clipboard.ContainsText(TextDataFormat.UnicodeText) && Clipboard.GetText(TextDataFormat.UnicodeText).Length > 0) { NotifyCopied(); return; }
                }
                catch (ExternalException) { }
                if (attempt < 10) PollCopy(request, attempt + 1);
            });
        }
        private void NotifyCopied()
        {
            if (settings.Sound) feedbackSound.Play();
            if (settings.Popup) { if (toast == null) toast = new CopyToast(); toast.Display(); }
        }
        private void Toggle() { settings.Enabled = !settings.Enabled; ApplySettings(); }
        private void UpdateState()
        {
            tray.Icon = settings.Enabled ? activeIcon : pausedIcon;
            tray.Text = settings.Enabled ? "CtrlCopy — включено" : "CtrlCopy — пауза";
            menu.SetEnabled(settings.Enabled);
            if (form != null && !form.IsDisposed) form.RefreshState();
        }
        private void ApplySettings()
        {
            monitor.Cancel(); UpdateState();
            try { store.Save(settings); if (form != null) form.ShowSaveStatus(null); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                if (form != null && !form.IsDisposed) form.ShowSaveStatus(ex.Message);
                tray.ShowBalloonTip(4000, "CtrlCopy", "Не удалось сохранить настройки. Проверьте доступ к папке AutoCopy.", ToolTipIcon.Warning);
            }
        }
        private void OpenSettings()
        {
            if (stopping) return;
            menu.Hide();
            Diagnostics.Write("settings open requested");
            if (form == null || form.IsDisposed)
            {
                form = new SettingsForm(settings, ApplySettings);
                Diagnostics.Write("settings constructed handle=" + form.Handle);
                form.FormClosing += (s, e) => { if (!stopping && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; form.Hide(); } };
            }
            form.RefreshState(); form.RefreshExclusions();
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Show(); Native.ShowWindow(form.Handle, 5); form.Activate();
            Diagnostics.Write("settings shown visible=" + form.Visible + " handle=" + form.Handle);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !stopping)
            {
                stopping = true;
                foreach (var timer in pendingTimers.ToArray()) timer.Dispose(); pendingTimers.Clear();
                monitor.Dispose(); reader.Dispose();
                tray.Visible = false; tray.Dispose(); menu.Dispose(); feedbackSound.Dispose();
                if (form != null) form.Dispose(); if (toast != null) toast.Dispose();
                window.Dispose(); dispatcher.Dispose(); activeIcon.Dispose(); pausedIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
