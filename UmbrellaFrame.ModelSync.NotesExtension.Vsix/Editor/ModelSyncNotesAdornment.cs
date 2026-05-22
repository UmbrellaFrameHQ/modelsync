using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Shell;
using UmbrellaFrame.ModelSync.NotesExtension.Forms;
using UmbrellaFrame.ModelSync.NotesExtension.Services;
using UmbrellaFrame.ModelSync.NotesExtension.Vsix.Services;

namespace UmbrellaFrame.ModelSync.NotesExtension.Vsix.Editor
{
    internal sealed class ModelSyncNotesAdornment
    {
        private readonly IWpfTextView _textView;
        private readonly IAdornmentLayer _layer;
        private readonly object _refreshGate = new object();
        private bool _refreshQueued;

        public ModelSyncNotesAdornment(IWpfTextView textView)
        {
            _textView = textView ?? throw new ArgumentNullException(nameof(textView));
            _layer = textView.GetAdornmentLayer(ModelSyncNotesAdornmentLayerDefinition.LayerName);
            _textView.LayoutChanged += OnLayoutChanged;
            _textView.Closed += OnClosed;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            RefreshAdornments();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _textView.LayoutChanged -= OnLayoutChanged;
            _textView.Closed -= OnClosed;
            _layer.RemoveAllAdornments();
        }

        private void RefreshAdornments()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            _layer.RemoveAllAdornments();

            if (_textView.TextViewLines == null)
            {
                return;
            }

            foreach (ITextViewLine viewLine in _textView.TextViewLines)
            {
                var line = viewLine.Start.GetContainingLine();
                var context = CSharpModelPropertyParser.TryGetContext(line);
                if (context == null)
                {
                    continue;
                }

                var noteCount = GetNoteCount(context);
                var button = CreateNotesButton(context, noteCount);
                Canvas.SetLeft(button, GetIconLeft(viewLine, button.Width));
                Canvas.SetTop(button, GetIconTop(viewLine, button.Height));

                var span = new SnapshotSpan(line.Start, Math.Max(1, line.Length));
                _layer.AddAdornment(
                    AdornmentPositioningBehavior.TextRelative,
                    span,
                    context.NoteKey,
                    button,
                    null);
            }
        }

        private double GetIconLeft(ITextViewLine viewLine, double iconWidth)
        {
            var line = viewLine.Start.GetContainingLine();
            var text = line.GetText();
            var firstTextOffset = 0;

            while (firstTextOffset < text.Length && char.IsWhiteSpace(text[firstTextOffset]))
            {
                firstTextOffset++;
            }

            var textPoint = line.Start + Math.Min(firstTextOffset, line.Length);
            var bounds = viewLine.GetCharacterBounds(textPoint);
            var desiredLeft = bounds.Left - iconWidth - 7;
            var minimumLeft = _textView.ViewportLeft + 2;
            if (desiredLeft >= minimumLeft)
            {
                return desiredLeft;
            }

            var rightSideLeft = viewLine.Right + 6;
            var maximumLeft = _textView.ViewportLeft + _textView.ViewportWidth - iconWidth - 4;
            return Math.Min(rightSideLeft, maximumLeft);
        }

        private static double GetIconTop(ITextViewLine viewLine, double iconHeight)
        {
            var textTop = viewLine.TextTop;
            var textHeight = Math.Max(1, viewLine.TextHeight);
            return textTop + Math.Max(0, (textHeight - iconHeight) / 2) + 1;
        }

        private int GetNoteCount(ModelPropertyContext context)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var noteCount = ModelNotesCountCache.GetCount(
                    VisualStudioNotesPaths.GetNotesFilePath(),
                    context.NoteKey,
                    QueueRefreshAdornments);

                return noteCount > 0 ? noteCount : GetLegacyNoteCount(context);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ModelSync Notes badge count failed: " + ex);
                return 0;
            }
        }

        private int GetLegacyNoteCount(ModelPropertyContext context)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrWhiteSpace(context.LegacyNoteKey) ||
                string.Equals(context.LegacyNoteKey, context.NoteKey, StringComparison.Ordinal))
            {
                return 0;
            }

            return ModelNotesCountCache.GetCount(
                VisualStudioNotesPaths.GetNotesFilePath(),
                context.LegacyNoteKey,
                QueueRefreshAdornments);
        }

        private void QueueRefreshAdornments()
        {
            lock (_refreshGate)
            {
                if (_refreshQueued)
                {
                    return;
                }

                _refreshQueued = true;
            }

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    if (!_textView.IsClosed)
                    {
                        RefreshAdornments();
                    }
                }
                finally
                {
                    lock (_refreshGate)
                    {
                        _refreshQueued = false;
                    }
                }
            });
        }

        private Button CreateNotesButton(ModelPropertyContext context, int noteCount)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            var button = new Button
            {
                Width = 16,
                Height = 16,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Content = CreateHistoryButtonContent(noteCount),
                ToolTip = $"Notes - {context.DisplayName}",
                Background = new SolidColorBrush(Color.FromRgb(245, 248, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(118, 142, 170)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            button.Resources.Add(typeof(Border), CreateRoundedButtonBorderStyle());
            button.MouseEnter += (_, _) =>
            {
                button.Background = new SolidColorBrush(Color.FromRgb(224, 238, 255));
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(45, 114, 210));
            };
            button.MouseLeave += (_, _) =>
            {
                button.Background = new SolidColorBrush(Color.FromRgb(245, 248, 252));
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(118, 142, 170));
            };
            button.Click += (_, _) => OpenNotes(context);
            return button;
        }

        private static Style CreateRoundedButtonBorderStyle()
        {
            var style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(4)));
            return style;
        }

        private static FrameworkElement CreateHistoryButtonContent(int noteCount)
        {
            var grid = new Grid
            {
                Width = 14,
                Height = 14,
                ClipToBounds = false
            };

            grid.Children.Add(CreateHistoryIcon());

            if (noteCount > 0)
            {
                var badge = CreateCountBadge(noteCount);
                badge.HorizontalAlignment = HorizontalAlignment.Right;
                badge.VerticalAlignment = VerticalAlignment.Top;
                badge.Margin = new Thickness(0, -5, -6, 0);
                grid.Children.Add(badge);
            }

            return grid;
        }

        private static FrameworkElement CreateCountBadge(int noteCount)
        {
            var text = noteCount > 9 ? "9+" : noteCount.ToString();
            return new Border
            {
                MinWidth = text.Length > 1 ? 13 : 11,
                Height = 11,
                Padding = new Thickness(2, 0, 2, 0),
                CornerRadius = new CornerRadius(5.5),
                Background = new SolidColorBrush(Color.FromRgb(45, 114, 210)),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontSize = 7,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private static FrameworkElement CreateHistoryIcon()
        {
            var canvas = new Canvas
            {
                Width = 11,
                Height = 11
            };

            var stroke = new SolidColorBrush(Color.FromRgb(36, 70, 110));

            var circle = new Ellipse
            {
                Width = 8.6,
                Height = 8.6,
                Stroke = stroke,
                StrokeThickness = 1.25,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(circle, 1.2);
            Canvas.SetTop(circle, 1.2);

            var handHour = new Line
            {
                X1 = 5.5,
                Y1 = 5.5,
                X2 = 5.5,
                Y2 = 3.3,
                Stroke = stroke,
                StrokeThickness = 1.15,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            var handMinute = new Line
            {
                X1 = 5.5,
                Y1 = 5.5,
                X2 = 7.4,
                Y2 = 6.6,
                Stroke = stroke,
                StrokeThickness = 1.15,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            var arrow = new Path
            {
                Data = Geometry.Parse("M1.9,5.3 L0.5,5.3 L0.5,3.2"),
                Stroke = stroke,
                StrokeThickness = 1.15,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent
            };

            canvas.Children.Add(circle);
            canvas.Children.Add(handHour);
            canvas.Children.Add(handMinute);
            canvas.Children.Add(arrow);
            return canvas;
        }

        private void OpenNotes(ModelPropertyContext context)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var notesFilePath = VisualStudioNotesPaths.GetNotesFilePath();
            var service = CreateNotesService();

            service.MoveNotes(context.LegacyNoteKey, context.NoteKey);
            NotesPopup.ShowForKey(null, service, context.NoteKey, context.DisplayName);
            ModelNotesCountCache.SetCount(notesFilePath, context.NoteKey, service.GetNotes(context.NoteKey).Count);
            ModelNotesCountCache.SetCount(notesFilePath, context.LegacyNoteKey, 0);
            RefreshAdornments();
        }

        private static ModelNotesService CreateNotesService()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            return new ModelNotesService(
                new JsonModelNotesRepository(VisualStudioNotesPaths.GetNotesFilePath()),
                new VisualStudioNotesUserProvider());
        }
    }
}
