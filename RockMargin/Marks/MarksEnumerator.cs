using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;


namespace RockMargin
{
    class TextMark
    {
        public enum MarkType
        {
            Unknown,
            Bookmark,
            Breakpoint,
            Tracepoint
        }

        public int line;
        public MarkType type;

        private static MarkType GetMarkType(IVsVisibleTextMarkerTag tag)
        {
            tag.MarkerType.GetDisplayName(out string name);

            if (name.StartsWith("Breakpoint"))
                return MarkType.Breakpoint;

            if (name.StartsWith("Tracepoint"))
                return MarkType.Tracepoint;

            if (name.StartsWith("Bookmark"))
                return MarkType.Bookmark;

            return MarkType.Unknown;
        }

        public static TextMark Create(IMappingTagSpan<IVsVisibleTextMarkerTag> tag)
        {
            MarkType mark_type = GetMarkType(tag.Tag);
            if (mark_type == MarkType.Unknown)
                return null;

            ITextBuffer buffer = tag.Span.BufferGraph.TopBuffer;
            SnapshotPoint? pos = tag.Span.Start.GetPoint(buffer, PositionAffinity.Successor);
            if (!pos.HasValue)
                return null;

            return new TextMark()
            {
                line = buffer.CurrentSnapshot.GetLineNumberFromPosition(pos.Value.Position),
                type = mark_type
            };
        }
    }

    class MarksEnumerator
    {
        private ITagAggregator<IVsVisibleTextMarkerTag> _aggregator = null;
        private DTE dte;
        private ITextView _view;

        private List<Breakpoint> breakpoints = new List<Breakpoint>();
        private System.Timers.Timer breakpointTimer;
        private bool stop = false;
        private SynchronizationContext context = null;
        private string filepath;

        public List<TextMark> Marks { get; } = new List<TextMark>();

        public event EventHandler<EventArgs> MarksChanged;


        public MarksEnumerator(IViewTagAggregatorFactoryService aggregator_factory, EnvDTE.DTE dte, ITextDocumentFactoryService docFactory, ITextView view)
        {
            this.dte = dte;

            _view = view;
            _view.Closed += OnViewClosed;
            _view.LayoutChanged += OnLayoutChanged;

            if (docFactory.TryGetTextDocument(view.TextBuffer, out var TextDocument))
                this.filepath = TextDocument.FilePath;

            _aggregator = aggregator_factory.CreateTagAggregator<IVsVisibleTextMarkerTag>(view);
            _aggregator.BatchedTagsChanged += OnTagsChanged;

            breakpoints = getBreakpoints();
            breakpointTimer = new System.Timers.Timer();
            breakpointTimer.Enabled = false;
            breakpointTimer.Interval = 500;
            breakpointTimer.AutoReset = false;
            breakpointTimer.Elapsed += BreakpointTimer_Elapsed;
            breakpointTimer.Start();

        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (context == null)
                context = SynchronizationContext.Current;
        }

        private void BreakpointTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // If there is a way to check for changed Breakpoints, feel free to do a PR!
            // For many hours I've researched Stackoverflow, ChatGPT and Github Copilot.
            // All came to the same conclusion, that the only way is to poll with a timer, and check if the list hast changed...
            try
            {
                List<Breakpoint> actualBreakpoints = getBreakpoints();
                if (!(breakpoints.Count == actualBreakpoints.Count && !actualBreakpoints.Except(breakpoints).Any()))
                {
                    breakpoints.Clear();
                    breakpoints = actualBreakpoints;

                    // Post to UI Thread
                    context?.Post(new SendOrPostCallback((o) =>
                    {
                        UpdateMarks();
                    }), null);
                }
            }
            catch { }

            breakpointTimer?.Start();
        }

        private List<Breakpoint> getBreakpoints()
        {
            var result = new List<Breakpoint>();
            foreach (Breakpoint br in dte.Debugger.Breakpoints)
            {
                string brFile = br.File;
                if (brFile == filepath)
                    result.Add(br);
            }
            return result;
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            breakpointTimer?.Dispose();
            breakpointTimer = null;
            _aggregator.BatchedTagsChanged -= OnTagsChanged;
        }

        private void OnTagsChanged(object source, BatchedTagsChangedEventArgs e)
        {
            UpdateMarks();
        }

        public void UpdateMarks()
        {
            Marks.Clear();

            foreach (Breakpoint br in breakpoints)
            {
                Marks.Add(new TextMark()
                {
                    line = br.FileLine,
                    type = TextMark.MarkType.Breakpoint
                }); ;
            }

            ITextSnapshot snapshot = _view.VisualSnapshot;
            SnapshotSpan span = new SnapshotSpan(snapshot, 0, snapshot.Length);
            foreach (var tag in _aggregator.GetTags(span))
            {
                var mark = TextMark.Create(tag);
                if (mark != null)
                    Marks.Add(mark);
            }

            MarksChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
