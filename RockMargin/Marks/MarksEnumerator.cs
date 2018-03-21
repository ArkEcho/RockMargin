﻿using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio;
using System.Windows.Media;


namespace RockMargin
{
	class TextMark
	{
		public int line;
		public Brush brush;

		public static TextMark Create(IMappingTagSpan<IVsVisibleTextMarkerTag> tag)
		{
			uint flags;
			int hr = tag.Tag.StreamMarker.GetVisualStyle(out flags);
			if (ErrorHandler.Succeeded(hr) &&
					((flags & (uint)MARKERVISUAL.MV_GLYPH) != 0) &&
					((flags & ((uint)MARKERVISUAL.MV_COLOR_ALWAYS | (uint)MARKERVISUAL.MV_COLOR_LINE_IF_NO_MARGIN)) != 0))
			{
				COLORINDEX[] foreground = new COLORINDEX[1];
				COLORINDEX[] background = new COLORINDEX[1];
				hr = tag.Tag.MarkerType.GetDefaultColors(foreground, background);
				if (ErrorHandler.Succeeded(hr))
				{
					ITextBuffer buffer = tag.Span.BufferGraph.TopBuffer;
					SnapshotPoint? pos = tag.Span.Start.GetPoint(buffer, PositionAffinity.Successor);
					if (pos.HasValue)
					{
						var text_mark = new TextMark
						{
							line = buffer.CurrentSnapshot.GetLineNumberFromPosition(pos.Value.Position),
							brush = ColorExtractor.GetBrushFromIndex(background[0])
						};
						return text_mark;
					}
				}
			}

			return null;
		}
	}

	class MarksEnumerator
	{
		private ITagAggregator<IVsVisibleTextMarkerTag> _aggregator = null;
		private ITextView _view;

		public List<TextMark> Marks { get; } = new List<TextMark>();

		public event EventHandler<EventArgs> MarksChanged;


		public MarksEnumerator(IViewTagAggregatorFactoryService aggregator_factory, ITextView view)
		{
			_view = view;
			_view.Closed += OnViewClosed;

			_aggregator = aggregator_factory.CreateTagAggregator<IVsVisibleTextMarkerTag>(view);
			_aggregator.BatchedTagsChanged += OnTagsChanged;
		}

		void OnViewClosed(object sender, EventArgs e)
		{
			_aggregator.BatchedTagsChanged -= OnTagsChanged;
		}

		private void OnTagsChanged(object source, BatchedTagsChangedEventArgs e)
		{
			UpdateMarks();
		}

		public void UpdateMarks()
		{
			Marks.Clear();

			var snapshot = _view.VisualSnapshot;
			var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
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
