using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace AutoCopy
{
    internal sealed class SelectionResult
    {
        internal CopyRequest Request;
        internal string Text;
        internal bool Password, TextContext, SelectionAvailable, Failed;
    }
    internal sealed class SelectionReader : IDisposable
    {
        private readonly object gate = new object();
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private CopyRequest pending;
        private volatile bool stopping;
        private readonly Func<CopyRequest, bool> current;
        internal event Action<SelectionResult> Read;
        internal SelectionReader(Func<CopyRequest, bool> current)
        {
            this.current = current;
            var worker = new Thread(Work) { IsBackground = true, Name = "AutoCopy selection reader" };
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
        }
        internal void Queue(CopyRequest request) { lock (gate) pending = request; signal.Set(); }
        private void Work()
        {
            while (!stopping)
            {
                signal.WaitOne();
                if (stopping) break;
                Thread.Sleep(100);
                CopyRequest request;
                lock (gate) { request = pending; pending = null; }
                if (request == null || !current(request)) { Diagnostics.Write("reader skipped stale request"); continue; }
                var result = new SelectionResult { Request = request };
                try
                {
                    AutomationElement focused = AutomationElement.FocusedElement;
                    if (focused != null && focused.Current.IsPassword) result.Password = true;
                    else
                    {
                        if (request.Kind == SelectionKind.AutoSelectedField) ReadAutoSelectedField(focused, result);
                        else if (request.Kind == SelectionKind.Keyboard) ReadAncestors(focused, result);
                        else
                        {
                            ReadAncestors(AutomationElement.FromPoint(new System.Windows.Point(request.X, request.Y)), result);
                            if (!result.SelectionAvailable && !result.Password) ReadAncestors(focused, result);
                        }
                    }
                }
                catch (Exception ex) when (ex is ElementNotAvailableException || ex is InvalidOperationException || ex is COMException || ex is UnauthorizedAccessException || ex is NotSupportedException) { result.Failed = true; Diagnostics.Write("reader " + ex.GetType().Name); }
                var handler = Read;
                if (!stopping && current(request) && handler != null) handler(result);
            }
            signal.Dispose();
        }
        private static void ReadAncestors(AutomationElement element, SelectionResult result)
        {
            uint target;
            Native.GetWindowThreadProcessId(result.Request.Window, out target);
            for (int depth = 0; element != null && depth < 12; depth++)
            {
                var info = element.Current;
                if (info.IsPassword) { result.Password = true; return; }
                if (info.ProcessId != (int)target) return;
                if (info.ControlType == ControlType.Edit || info.ControlType == ControlType.Document) result.TextContext = true;
                object pattern;
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                {
                    result.TextContext = true;
                    var text = (TextPattern)pattern;
                    if (text.SupportedTextSelection != SupportedTextSelection.None)
                    {
                        result.SelectionAvailable = true;
                        var ranges = text.GetSelection();
                        if (ranges != null && ranges.Length <= 64)
                        {
                            var selected = new StringBuilder();
                            foreach (var range in ranges)
                            {
                                string part = range.GetText(1000001);
                                if (part != null) selected.Append(part);
                                if (selected.Length > 1000000) return;
                            }
                            if (selected.Length > 0) { result.Text = selected.ToString(); return; }
                        }
                        return;
                    }
                }
                element = TreeWalker.RawViewWalker.GetParent(element);
            }
        }
        private static void ReadAutoSelectedField(AutomationElement element, SelectionResult result)
        {
            if (element == null) return;
            var info = element.Current;
            uint target; Native.GetWindowThreadProcessId(result.Request.Window, out target);
            if (info.ProcessId != (int)target || info.ControlType != ControlType.Edit || !info.HasKeyboardFocus) return;
            if (info.IsPassword) { result.Password = true; return; }
            if (!result.Request.FocusShortcut && !info.BoundingRectangle.Contains(new System.Windows.Point(result.Request.X,result.Request.Y))) return;
            object pattern;
            if (!element.TryGetCurrentPattern(TextPattern.Pattern,out pattern)) return;
            var text=(TextPattern)pattern;
            if(text.SupportedTextSelection==SupportedTextSelection.None) return;
            if(object.Equals(text.DocumentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute),true)) return;
            result.SelectionAvailable=true; result.TextContext=true;
            var ranges=text.GetSelection();
            if(ranges==null || ranges.Length!=1) return;
            var range=ranges[0]; var document=text.DocumentRange;
            if(range.CompareEndpoints(TextPatternRangeEndpoint.Start,document,TextPatternRangeEndpoint.Start)!=0 || range.CompareEndpoints(TextPatternRangeEndpoint.End,document,TextPatternRangeEndpoint.End)!=0) return;
            string selected=range.GetText(1000001);
            if(!string.IsNullOrEmpty(selected) && selected.Length<=1000000) result.Text=selected;
        }
        public void Dispose() { stopping = true; signal.Set(); }
    }
}
