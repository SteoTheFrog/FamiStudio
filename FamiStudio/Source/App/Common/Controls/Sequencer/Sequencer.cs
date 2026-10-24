using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FamiStudio
{
    // This and the Piano Roll are the only 2 "Uber-Control" left, where everything is in one class with 
    // no sub-widgets whatsoever. Both of these need a full rewrite. This is for historical reason, when
    // the app started, we didnt have a proper widget system, we sort-of do now. Also, all the touch and
    // mouse input would need to be unified as much as possible.
    //
    // The sequencer would need to be broken down into a timeline, a pattern area, the channel names and
    // associated icons needs to be made into real buttons, etc.
    public class Sequencer : Container
    {
        const int DefaultChannelNameSizeX    = Platform.IsMobile ? 68 : 94;
        const int DefaultHeaderSizeY         = 17;
        const int DefaultPatternHeaderSizeY  = 13;
        const int DefaultScrollBarThickness1 = 10;
        const int DefaultScrollBarThickness2 = 16;
        const int DefaultMinScrollBarLength  = 64;
        const float ScrollSpeedFactor        = Platform.IsMobile ? 2.0f : 1.0f;
        const float DefaultZoom              = Platform.IsMobile ? 0.5f : 2.0f;

        const float MinZoom = 0.25f;
        const float MaxZoom = 16.0f;

        int channelNameSizeX;
        int headerSizeY;
        int channelSizeY;
        int patternHeaderSizeY;
        int scrollMargin;
        int scrollBarThickness;
        int minScrollBarLength;
        int virtualSizeY;
        int numRows;
        float noteSizeX;
        bool allowVerticalScrolling;

        int scrollX = 0;
        int scrollY = 0;
        Point mouseLastPos = new Point(-1, -1);
        Point captureMousePos = new Point(-1, -1);
        int captureChannelIdx = -1;
        int captureRowIdx = -1;
        int capturePatternIdx = -1;
        int dragSeekPosition = -1;
        int selectionDragAnchorPatternIdx = -1;
        float zoom = DefaultZoom;
        float bitmapScale = 1.0f;
        float channelBitmapScale = 1.0f;
        float flingVelX;
        float flingVelY;
        float selectionDragAnchorPatternXFraction = -1.0f;
        CaptureOperation captureOperation;
        bool panning = false;
        bool canFling = false;
        bool continuouslyFollowing = false;
        bool captureThresholdMet = false;
        bool captureRealTimeUpdate = false;
        bool showExpansionIcons = false;
        bool timeOnlySelection = false;
        bool hideEmptyChannels = false;
        bool[] channelVisible;
        int[] channelToRow;
        int[] rowToChannel;
        PatternLocation selectionMin = PatternLocation.Invalid;
        PatternLocation selectionMax = PatternLocation.Invalid;
        PatternLocation highlightLocation = PatternLocation.Invalid;

        // Hover tracking
        Point hoverPos = new Point(-1, -1);

        //PatternBitmapCache patternCache;

        Color selectedPatternVisibleColor   = Color.FromArgb(64, Theme.LightGreyColor1);
        Color selectedPatternInvisibleColor = Color.FromArgb(16, Theme.LightGreyColor1);

        private class CaptureOperation
        {
            private string name;
            private bool needsThreshold;
            private bool realTimeUpdate;

            public string Name => name;
            public bool NeedsThreshold => needsThreshold;
            public bool RealTimeUpdate => realTimeUpdate;

            public CaptureOperation(string desc, bool realTime = true, bool threshold = true)
            {
                name = desc;
                needsThreshold = threshold;
                realTimeUpdate = realTime;
            }

            public override string ToString()
            {
                return name;
            }
        }

        private static readonly CaptureOperation SelectColumn    = new CaptureOperation("SelectColumn", true, Platform.IsMobile);
        private static readonly CaptureOperation SelectRectangle = new CaptureOperation("SelectRectangle", true, false);
        private static readonly CaptureOperation DragSelection   = new CaptureOperation("DragSelection");
        private static readonly CaptureOperation AltZoom         = new CaptureOperation("AltZoom", false, false);
        private static readonly CaptureOperation DragSeekBar     = new CaptureOperation("DragSeekBar", true, false);
        private static readonly CaptureOperation MobileZoom      = new CaptureOperation("MobileZoom", false, false);
        private static readonly CaptureOperation MobilePan       = new CaptureOperation("MobilePan", false, false);

        // Controls.
        private ChannelNameArea channelNameArea;
        private PatternArea patternArea;
        private SequencerTimeline timeline;
        private ScrollBar horizontalScrollBar;
        private ScrollBar verticalScrollBar;

        public delegate void PatternClickedDelegate(int channelIdx, int patternIdx, bool setActive);
        public delegate void ChannelDelegate(int channelIdx);
        public delegate void EmptyDelegate();

        public event PatternClickedDelegate PatternClicked;
        public event EmptyDelegate PatternModified;
        public event EmptyDelegate PatternsPasted;
        public event EmptyDelegate SelectionChanged;

        public Color SelectedPatternInvisibleColor => selectedPatternInvisibleColor;
        public Color SelectedPatternVisibleColor => selectedPatternVisibleColor;
        public PatternLocation HighlightLocation => highlightLocation;
        public PatternLocation SelectionMin => selectionMin;
        public PatternLocation SelectionMax => selectionMax;
        public Point MouseLastPosition => mouseLastPos;
        public Point HoverPosition => hoverPos;
        public bool HasCaptureOperation => captureOperation != null;
        public bool HasValidTimeOnlySelection => IsValidTimeOnlySelection();
        public bool IsDragSelectionInProgress => captureOperation == DragSelection;
        public int CaptureRowIdx => captureRowIdx;
        public int SeekX => GetPixelForNote(GetSeekFrameToDraw());
        public int SelectionDragAnchorPatternIdx => selectionDragAnchorPatternIdx;
        public float BitmapScale => bitmapScale;
        public float ChannelBitmapScale => channelBitmapScale;
        public float SelectionDragAnchorPatternXFraction => selectionDragAnchorPatternXFraction;


        #region Localization

        // Dialogs
        LocalizedString PasteTitle;
        LocalizedString PasteMissingInstrumentsMessage;
        LocalizedString PasteMissingArpeggiosMessage;
        LocalizedString PasteMissingSamplesMessage;

        // Paste special dialog
        LocalizedString PasteSpecialTitle;
        LocalizedString InsertLabel;
        LocalizedString InsertTooltip;
        LocalizedString ExtendSongLabel;
        LocalizedString ExtendSongTooltip;
        LocalizedString RepeatLabel;
        LocalizedString RepeatTooltip;

        // Pattern properties dialog
        LocalizedString PatternPropertiesTitle;
        LocalizedString MultiplePatternsSelectedLabel;
        LocalizedString ErrorRenamingPattern;

        #endregion

        public Sequencer()
        {
            Localization.Localize(this);
            SetTickEnabled(true);
            supportsLongPress = true;
            supportsDoubleClick = true;
        }

        private Song Song
        {
            get { return App?.SelectedSong; }
        }

        public bool ShowExpansionIcons
        {
            get { return showExpansionIcons; }
            set
            {
                if (showExpansionIcons != value)
                {
                    showExpansionIcons = value;
                    MarkDirty();
                }
            }
        }

        private void UpdateRenderCoords()
        {
            var scrollBarSize  = Settings.ScrollBars == 1 ? DefaultScrollBarThickness1 : (Settings.ScrollBars == 2 ? DefaultScrollBarThickness2 : 0);
            var patternZoom    = Song != null ? 128.0f / (float)Utils.NextPowerOfTwo(Song.PatternLength) : 1.0f;

            ComputeDesiredSizeY(out var unscaledChannelSizeY, out allowVerticalScrolling);

            channelNameSizeX   = DpiScaling.ScaleForWindow(DefaultChannelNameSizeX);
            headerSizeY        = DpiScaling.ScaleForWindow(DefaultHeaderSizeY);
            channelSizeY       = DpiScaling.ScaleForWindow(unscaledChannelSizeY);
            scrollBarThickness = DpiScaling.ScaleForWindow(scrollBarSize);
            minScrollBarLength = DpiScaling.ScaleForWindow(DefaultMinScrollBarLength);
            noteSizeX          = DpiScaling.ScaleForWindowFloat(zoom * patternZoom);
            virtualSizeY       = rowToChannel != null ? channelSizeY * rowToChannel.Length : 0;
            scrollMargin       = (width - channelNameSizeX) / 8;

            // Shave a couple pixels when the size is getting too small.
            if (unscaledChannelSizeY < 24)
                patternHeaderSizeY = DpiScaling.ScaleForFont(DefaultPatternHeaderSizeY - 2);
            else
                patternHeaderSizeY = DpiScaling.ScaleForFont(DefaultPatternHeaderSizeY);

            var patternAreaWidth = width - channelNameSizeX - (verticalScrollBar != null && verticalScrollBar.Visible ? scrollBarThickness - 1 : 0);
            ConditionalAddOrRemoveScrollbars();

            channelNameArea?.Resize(channelNameSizeX, height);
            channelNameArea?.UpdateLayout(headerSizeY, channelSizeY);
            timeline?.Move(channelNameSizeX, 0, width - channelNameSizeX, headerSizeY);
            timeline?.UpdateLayout(channelSizeY);
            patternArea?.Move(channelNameSizeX, headerSizeY, patternAreaWidth, height - headerSizeY - scrollBarThickness + 1);
            patternArea?.UpdateLayout(channelSizeY, patternHeaderSizeY, rowToChannel?.Length ?? 0);

            RebuildChannelMap();
            SetHoverByCoords(mouseLastPos); // For undo / redo.
        }

        private void ConditionalAddOrRemoveScrollbars()
        {
            if (Settings.ScrollBars != 0)
            {
                UpdateScrollBarCoords();

                if (verticalScrollBar == null)
                {
                    verticalScrollBar = new ScrollBar();
                    verticalScrollBar.LineColor = Color.Black;
                    verticalScrollBar.Scrolled += VerticalScrollBar_Scrolled;
                    AddControl(verticalScrollBar);
                }

                if (horizontalScrollBar == null)
                {
                    horizontalScrollBar = new ScrollBar(true);
                    horizontalScrollBar.LineColor = Color.Black;
                    horizontalScrollBar.Scrolled += HorizontalScrollBar_Scrolled;
                    AddControl(horizontalScrollBar);
                }
            }
            else
            {
                if (verticalScrollBar != null)
                {
                    verticalScrollBar.Scrolled -= VerticalScrollBar_Scrolled;
                    RemoveControl(verticalScrollBar);
                    verticalScrollBar = null;
                }

                if (horizontalScrollBar != null)
                {
                    horizontalScrollBar.Scrolled -= HorizontalScrollBar_Scrolled;
                    RemoveControl(horizontalScrollBar);
                    horizontalScrollBar = null;
                }
            }
        }

        private void UpdateScrollBarCoords()
        {
            if (scrollBarThickness > 0)
            {
                if (horizontalScrollBar == null)
                    return;

                GetMinMaxScroll(out _, out var maxScrollX, out _, out var maxScrollY);
                verticalScrollBar.Visible = maxScrollY > 0;

                if (verticalScrollBar.Visible)
                {
                    var vSize = height - headerSizeY - scrollBarThickness;

                    verticalScrollBar.Move(width - scrollBarThickness + 1, headerSizeY, scrollBarThickness, vSize);
                    verticalScrollBar.VirtualSize = maxScrollY + vSize;
                    verticalScrollBar.SetScroll(scrollY, false);
                    verticalScrollBar.UpdateThickness();
                }

                var hSize = width - channelNameSizeX + 1;
                horizontalScrollBar.Move(channelNameSizeX - 1, height - scrollBarThickness, hSize, scrollBarThickness);
                horizontalScrollBar.VirtualSize = maxScrollX + hSize;
                horizontalScrollBar.SetScroll(scrollX, false);
                horizontalScrollBar.UpdateThickness();
            }
        }

        public int GetPixelForNote(int n, bool scroll = true)
        {
            // On PC, all math noteSizeX are always integer, but on mobile, they 
            // can be float. We need to cast into double since at the maximum zoom,
            // in a *very* long song, we are hitting the precision limit of floats.
            var x = (int)(n * (double)noteSizeX);
            if (scroll)
                x -= scrollX;
            return x;
        }

        public int GetNoteForPixel(int x, bool scroll = true)
        {
            if (scroll)
                x += scrollX;
            return (int)(x / (double)noteSizeX);
        }

        private int GetNonEmptyChannelCount()
        {
            var count = 0;

            foreach (var c in App.SelectedSong.Channels)
            {
                if (c.HasAnyPatternInstances)
                    count++;
            }

            return count;
        }

        private int GetChannelCount(bool allowHideEmptyChannel = true)
        {
            if (App != null && App.Project != null && App.SelectedSong != null)
            {
                if (hideEmptyChannels && allowHideEmptyChannel)
                    return GetNonEmptyChannelCount();

                return App.SelectedSong.Channels.Length;
            }
            else
            {
                return 5;
            }
        }

        public int ComputeDesiredSizeY(out int channelSizeY, out bool verticalScoll)
        {
            var channelCount = GetChannelCount(false);
            var visibleChannelCount = GetChannelCount(true);
            var scrollBarSize = Settings.ScrollBars == 1 ? DefaultScrollBarThickness1 : (Settings.ScrollBars == 2 ? DefaultScrollBarThickness2 : 0);
            var constantSize = DefaultHeaderSizeY + scrollBarSize + 1;

            if (Platform.IsMobile)
            {
                verticalScoll = true;
                channelSizeY = visibleChannelCount > 0 ? Math.Clamp(((int)Math.Ceiling(height / DpiScaling.Window) - DefaultHeaderSizeY) / visibleChannelCount, 21, 80) : 21;
                return channelSizeY * channelCount + constantSize;
            }
            else
            {
                // Keep size a multiple of 2 at 150%, multiple of 4 at 125%/175%, etc.
                var frac = Utils.Frac(DpiScaling.Window);
                var divider = (frac == 0.25f || frac == 0.75f) ? 4 : (frac == 0.5f) ? 2 : 1;
                var minChannelSize = Utils.RoundUp(21, divider);
                var idealSequencerHeight = (int)Math.Round(ParentWindow.Height / DpiScaling.Window * Settings.IdealSequencerSize / 100);
                        
                channelSizeY = visibleChannelCount > 0 ? Math.Max(Utils.RoundDown(idealSequencerHeight / visibleChannelCount, divider), minChannelSize) : minChannelSize;
                
                var actualSequencerHeight = channelSizeY * visibleChannelCount;

                if (Settings.AllowSequencerVerticalScroll && actualSequencerHeight > idealSequencerHeight)
                {
                    verticalScoll = true;
                    return idealSequencerHeight + constantSize;
                }
                else
                {
                    verticalScoll = false;
                    return actualSequencerHeight + constantSize;
                }
            }
        }

        public void LayoutChanged()
        {
            UpdateRenderCoords();
            ClampScroll();
            InvalidatePatternCache();
            MarkDirty();
        }

        public void Reset()
        {
            scrollX = 0;
            scrollY = 0;
            zoom = DefaultZoom;
            ClearSelection();
            channelNameArea?.Reset();
            UpdateRenderCoords();
            InvalidatePatternCache();
        }

        private void RebuildChannelMap()
        {
            if (Song == null)
            {
                return;
            }

            channelVisible = new bool[Song.Channels.Length];
            channelToRow = new int[Song.Channels.Length];

            if (hideEmptyChannels)
            {
                numRows = GetChannelCount(true);
                rowToChannel = new int[numRows];

                for (int i = 0, j = 0; i < Song.Channels.Length; i++)
                {
                    if (Song.Channels[i].HasAnyPatternInstances)
                    {
                        channelVisible[i] = true;
                        channelToRow[i] = j;
                        rowToChannel[j] = i;
                        j++;
                    }
                    else
                    {
                        channelToRow[i] = -1;
                    }
                }
            }
            else
            {
                numRows = channelVisible.Length;
                rowToChannel = channelToRow;

                for (int i = 0; i < channelVisible.Length; i++)
                {
                    channelVisible[i] = true;
                    channelToRow[i] = i;
                    rowToChannel[i] = i;
                }
            }
        }

        public bool GetChannelVisibleForIndex(int index)
        {
            return channelVisible != null && channelVisible?.Length > index && index >= 0 && channelVisible[index];
        }

        public int GetChannelToRowForIndex(int index)
        {
            return channelToRow != null && channelToRow.Length > index && index >= 0 ? channelToRow[index] : -1;
        }

        public int GetRowToChannelForIndex(int index)
        {
            return rowToChannel != null && rowToChannel.Length > index && index >= 0 ? rowToChannel[index] : -1;
        }

        public void SetHideEmptyChannels(bool hide)
        {
            hideEmptyChannels = hide;
            RebuildChannelMap();
            MarkDirty();
        }

        private void ClearSelection()
        {
            selectionMin = PatternLocation.Invalid;
            selectionMax = PatternLocation.Invalid;
            patternArea?.ClearSelectedPatternRefCounts();
            SelectionChanged?.Invoke();
        }

        private void SetSelection(PatternLocation location)
        {
            SetSelection(location, location, false);
        }

        private void SetSelection(PatternLocation min, PatternLocation max, bool timeOnly = false)
        {
            selectionMin = min; 
            selectionMax = max;
            timeOnlySelection = timeOnly;
            SelectionChanged?.Invoke();
            patternArea?.UpdateSelectedPatternRefCounts();
        }

        private void EnsureSelectionInclude(PatternLocation location)
        {
            if (!IsSelectionValid())
            {
                SetSelection(location);
            }
            else
            {
                SetSelection(PatternLocation.Min(selectionMin, location), PatternLocation.Max(selectionMax, location));
            }
        }

        private bool IsPatternSelected(PatternLocation location)
        {
            return location.ChannelIndex >= selectionMin.ChannelIndex && location.ChannelIndex <= selectionMax.ChannelIndex &&
                   location.PatternIndex >= selectionMin.PatternIndex && location.PatternIndex <= selectionMax.PatternIndex;
        }

/*         private bool IsPatternColumnSelected(int patternIdx)
        {
            return IsValidTimeOnlySelection() && patternIdx >= selectionMin.PatternIndex && patternIdx <= selectionMax.PatternIndex;
        } */

/*         private bool SelectionContainsMultiplePatterns()
        {
            return IsSelectionValid() && (selectionMax.ChannelIndex - selectionMin.ChannelIndex + 1) * (selectionMax.PatternIndex - selectionMin.PatternIndex + 1) > 1;
        } */

        public bool GetPatternTimeSelectionRange(out int minPatternIdx, out int maxPatternIdx)
        {
            if (IsSelectionValid())
            {
                minPatternIdx = selectionMin.PatternIndex;
                maxPatternIdx = selectionMax.PatternIndex;
                return true;
            }
            else
            {
                minPatternIdx = -1;
                maxPatternIdx = -1;
                return false;
            }
        }

        private void SetMouseLastPos(Point p)
        {
            mouseLastPos = p;
        }

        private void SetFlingVelocity(float x, float y)
        {
            flingVelX = x;
            flingVelY = y;
        }

        protected override void OnAddedToContainer()
        {
            UpdateRenderCoords();

            ContainerTouchFlingNotify += MainContainer_ContainerTouchFlingNotify;

            channelNameArea = new ChannelNameArea();
            AddControl(channelNameArea);

            timeline = new SequencerTimeline();
            timeline.CancelDragSeekBar += AbortCaptureOperation;
            timeline.ColumnSelect += StartColumnSelection;
            timeline.DragSeekBar += StartDragSeekBar;
            timeline.LoopPointToggled += SetLoopPoint;
            timeline.PatternModified += () => PatternModified?.Invoke();
            timeline.StartAltZoom += StartAltZoom;
            AddControl(timeline);

            patternArea = new PatternArea();
            patternArea.ClearHighlightedPattern += ClearHighlightedPatern;
            patternArea.ClearSelection += ClearSelection;
            patternArea.CopySelectionToCursor += CopySelectionToCursor;
            patternArea.DeleteSelection += () => DeleteSelection(true);
            patternArea.EditPatternProperties += EditPatternProperties;
            patternArea.EnsureSelectionInclude += EnsureSelectionInclude;
            patternArea.PatternClicked += PatternArea_PatternClicked;
            patternArea.PatternModified += () => PatternModified?.Invoke();
            patternArea.SetHighlightedPattern += SetHighlightedPattern;
            patternArea.SetSelection += SetSelection;
            patternArea.SelectionChanged += () => SelectionChanged?.Invoke();
            patternArea.StartAltZoom += StartAltZoom;
            patternArea.StartDragSelection += StartDragSelection;
            patternArea.StartRectangleSelection += StartRectangleSelection;
            AddControl(patternArea);

            base.OnAddedToContainer();

            if (Platform.IsMobile)
            {
                bitmapScale = DpiScaling.ScaleForWindowFloat(0.5f);
                channelBitmapScale = DpiScaling.ScaleForWindowFloat(0.25f);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            UpdateRenderCoords();
            ClampScroll();
        }

        public bool IsSelectionValid()
        {
            return selectionMin.IsValid && selectionMax.IsValid;
        }

        public bool IsValidTimeOnlySelection()
        {
            return IsSelectionValid() && timeOnlySelection;
        }

        private void SetHighlightedPattern(PatternLocation location)
        {
            highlightLocation = location;
        }

        private void ClearHighlightedPatern()
        {
            highlightLocation = PatternLocation.Invalid;
        }

        private bool HasHighlightedPattern()
        {
            return highlightLocation.IsValid;
        }

        public Color GetSeekBarColor()
        {
            if (App.IsRecording)
            {
                return Theme.DarkRedColor;
            }
            else
            {
                if (App.IsSeeking)
                {
                    return Theme.Lighten(Theme.YellowColor, (int)(Math.Abs(Math.Sin(Platform.TimeSeconds() * 12.0)) * 75));
                }
                else
                {
                    return Theme.YellowColor;
                }
            }
        }

        public int GetSeekFrameToDraw()
        {
            return captureOperation == DragSeekBar ? dragSeekPosition : App.CurrentFrame;
        }

        private int MinVisibleNoteIdx()
        {
            return Math.Max(GetNoteForPixel(0), 0);
        }

        private int MaxVisibleNoteIdx()
        {
            return Math.Min(GetNoteForPixel(width) + 1, Song.GetPatternStartAbsoluteNoteIndex(Song.Length));
        }

        public int MinVisiblePattern()
        {
            return Utils.Clamp(Song.PatternIndexFromAbsoluteNoteIndex(MinVisibleNoteIdx()) + 0, 0, Song.Length);
        }

        public int MaxVisiblePattern()
        {
            return Utils.Clamp(Song.PatternIndexFromAbsoluteNoteIndex(MaxVisibleNoteIdx()) + 1, 0, Song.Length);
        }

        protected void RenderDebug(Graphics g)
        {
#if DEBUG
            if (Platform.IsMobile)
            {
                g.OverlayCommandList.FillRectangle(mouseLastPos.X - 30, mouseLastPos.Y - 30, mouseLastPos.X + 30, mouseLastPos.Y + 30, Theme.WhiteColor);
            }
#endif
        }

        protected override void OnRender(Graphics g)
        {
            // Happens when piano roll is maximized.
            if (height <= 1)
                return;

            base.OnRender(g);

            //RenderPatternArea(g);
            RenderDebug(g);
        }

        private void ReplaceSelectionUtil(Point pos, bool forceInSelection, Func<Channel, bool> channelValid, Action<Pattern> action)
        {
            Debug.Assert(!forceInSelection || IsSelectionValid());

            if (GetPatternForCoord(pos.X, pos.Y, out var location))
            {
                // If we drag on selection, we process the whole selection, otherwise
                // just the pattern under the mouse.
                if (IsPatternSelected(location))
                {
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Song, Song.Id);
                    var replacedAnything = false;

                    for (int i = selectionMin.ChannelIndex; i <= selectionMax.ChannelIndex; i++)
                    {
                        var channel = Song.Channels[i];
                        if (channelValid(channel))
                        {
                            for (int j = selectionMin.PatternIndex; j <= selectionMax.PatternIndex; j++)
                            {
                                var pattern = channel.PatternInstances[j];
                                if (pattern != null)
                                {
                                    action(pattern);
                                    NotifyPatternChange(pattern);
                                    replacedAnything = true;
                                }
                            }

                            channel.InvalidateCumulativePatternCache();
                        }
                    }

                    App.UndoRedoManager.AbortOrEndTransaction(replacedAnything);
                    MarkDirty();
                }
                else
                {
                    var channel = Song.Channels[location.ChannelIndex];
                    if (channelValid(channel))
                    {
                        var pattern = channel.PatternInstances[location.PatternIndex];
                        if (pattern != null)
                        {
                            App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
                            action(pattern);
                            NotifyPatternChange(pattern);
                            channel.InvalidateCumulativePatternCache(pattern);
                            App.UndoRedoManager.EndTransaction();
                            MarkDirty();
                        }
                    }
                }
            }
        }

        public void ReplaceSelectionInstrument(Instrument instrument, Point pos, bool forceInSelection = false)
        {
            ReplaceSelectionUtil(
                pos, forceInSelection,
                (channel) => channel.SupportsInstrument(instrument),
                (pattern) =>
                {
                    foreach (var n in pattern.Notes.Values)
                    {
                        if (n.IsMusical)
                            n.Instrument = instrument;
                    }
                });
        }

        public void ReplaceSelectionArpeggio(Arpeggio arpeggio, Point pos, bool forceInSelection = false)
        {
            ReplaceSelectionUtil(
                pos, forceInSelection,
                (channel) => channel.SupportsArpeggios,
                (pattern) =>
                {
                    foreach (var n in pattern.Notes.Values)
                    {
                        if (n.IsMusical)
                            n.Arpeggio = arpeggio;
                    }
                });
        }

        private bool GetScrollBarParams(bool horizontal, out int thumbPos, out int thumbSize, out int scrollSize)
        {
            thumbPos   = 0;
            thumbSize  = 0;
            scrollSize = 0;

            if (scrollBarThickness > 0)
            {
                if (horizontal)
                {
                    GetMinMaxScroll(out _, out var maxScrollX, out _, out _);

                    scrollSize = width - channelNameSizeX + 1;
                    thumbSize = Math.Max(minScrollBarLength, (int)Math.Round(scrollSize * Math.Min(1.0f, scrollSize / (float)(maxScrollX + scrollSize))));
                    thumbPos = (int)Math.Round((scrollSize - thumbSize) * (scrollX / (float)maxScrollX));
                    return true;
                }
                else if (allowVerticalScrolling)
                {
                    GetMinMaxScroll(out _, out _, out _, out var maxScrollY);

                    scrollSize = height - headerSizeY - scrollBarThickness;
                    thumbSize = Math.Max(minScrollBarLength, (int)Math.Round(scrollSize * Math.Min(1.0f, scrollSize / (float)(maxScrollY + scrollSize))));
                    thumbPos = (int)Math.Round((scrollSize - thumbSize) * (scrollY / (float)maxScrollY));
                    return true;
                }
            }

            return false;
        }

        public void NotifyPatternChange(Pattern pattern)
        {
            if (pattern != null)
            {
                patternArea?.InvalidatePattern(pattern);
            }
        }

#if DEBUG
        public void ValidateIntegrity()
        {
            patternArea?.ValidateIntegrity();
        }
#endif

        private void GetMinMaxScroll(out int minScrollX, out int maxScrollX, out int minScrollY, out int maxScrollY)
        {
            minScrollX = 0;
            maxScrollX = Song != null ? Math.Max(GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Song.Length), false) - scrollMargin, 0) : 0;
            minScrollY = 0;
            maxScrollY = allowVerticalScrolling ? Math.Max(virtualSizeY + headerSizeY - height + scrollBarThickness, 0) : 0;
        }

        private bool ClampScroll()
        {
            GetMinMaxScroll(out var minScrollX, out var maxScrollX, out var minScrollY, out var maxScrollY);

            var scrolledX = true;
            var scrolledY = true;

            if (scrollX < minScrollX) { scrollX = minScrollX; scrolledX = false; }
            if (scrollX > maxScrollX) { scrollX = maxScrollX; scrolledY = false; }
            if (scrollY < minScrollY) { scrollY = minScrollY; scrolledY = false; }
            if (scrollY > maxScrollY) { scrollY = maxScrollY; scrolledY = false; }

            channelNameArea?.SetScrollY(scrollY);
            patternArea?.SetScrollY(scrollY);

            horizontalScrollBar?.SetScroll(scrollX, false);
            verticalScrollBar?.SetScroll(scrollY, false);

            return scrolledX || scrolledY;
        }

        private bool DoScroll(Point delta)
        {
            scrollX -= delta.X;
            scrollY -= delta.Y; 
            MarkDirty();
            return ClampScroll();
        }

        public int GetPatternIndexForCoord(int x)
        {
            var noteIdx = GetNoteForPixel(x - channelNameSizeX);

            if (noteIdx < 0 || noteIdx >= Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                return -1;

            return Song.PatternIndexFromAbsoluteNoteIndex(noteIdx);
        }

        private bool GetPatternForCoord(int x, int y, out PatternLocation location)
        {
            var noteIdx = GetNoteForPixel(x - channelNameSizeX);

            if (noteIdx < 0 || noteIdx >= Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
            {
                location = PatternLocation.Invalid;
                return false;
            }

            location = new PatternLocation(GetChannelIndexForCoord(y), Song.PatternIndexFromAbsoluteNoteIndex(noteIdx));

            return x > channelNameSizeX && y > headerSizeY && location.IsChannelInSong(Song);
        }

        private int GetRowIndexForCoord(int y)
        {
            return y > headerSizeY ? Utils.Clamp(((y - headerSizeY) + scrollY) / channelSizeY, 0, rowToChannel.Length - 1) : -1;
        }

        private int GetChannelIndexForCoord(int y)
        {
            return y > headerSizeY && rowToChannel.Length > 0 ? rowToChannel[GetRowIndexForCoord(y)] : -1;
        }

        private void GetClampedPatternForCoord(int x, int y, out int channelIdx, out int patternIdx)
        {
            patternIdx = Song.PatternIndexFromAbsoluteNoteIndex(Utils.Clamp(GetNoteForPixel(x - channelNameSizeX), 0, Song.GetPatternStartAbsoluteNoteIndex(Song.Length) - 1));
            channelIdx = Utils.Clamp(GetChannelIndexForCoord(y), 0, Song.Length);
        }

        private void CaptureMouse(Control control, Point p)
        {
            SetMouseLastPos(p);
            captureMousePos = p;

            control ??= this; // Safety.
            control.CapturePointer();
        }

        private void StartCaptureOperation(Control control, Point p, CaptureOperation op)
        {
            Debug.Assert(captureOperation == null);

            var ctrlPos = ControlToLocal(control, p);

            CaptureMouse(control, ctrlPos);

            canFling = false;
            captureOperation = op;
            captureThresholdMet = !op.NeedsThreshold;
            captureRealTimeUpdate = op.RealTimeUpdate;
            captureRowIdx = GetRowIndexForCoord(ctrlPos.Y);
            GetClampedPatternForCoord(ctrlPos.X, ctrlPos.Y, out captureChannelIdx, out capturePatternIdx);

            if (captureOperation == SelectRectangle)
            {
                patternArea?.ClearSelectedPatternRefCounts();
            }
            else if (captureOperation == DragSeekBar)
            {
                UpdateSeekDrag(p, false);
            }
            else if (captureOperation == MobileZoom)
            {
                SetMouseLastPos(ctrlPos);
            }
        }

        private void SetLoopPoint(int patternIdx)
        {
            App.UndoRedoManager.BeginTransaction(TransactionScope.Song, Song.Id);
            Song.SetLoopPoint(Song.LoopPoint == patternIdx ? -1 : patternIdx);
            App.UndoRedoManager.EndTransaction();
            MarkDirty();
        }

        private void VerticalScrollBar_Scrolled(Control sender, int pos)
        {
            if (sender == verticalScrollBar)
            {
                scrollY = pos;
                channelNameArea?.SetScrollY(scrollY);
                patternArea?.SetScrollY(scrollY);
            }
            else
            {
                scrollX = pos;
            }

            MarkDirty();
        }

        private void HorizontalScrollBar_Scrolled(Control sender, int pos)
        {
            scrollX = pos;
            MarkDirty();
        }

        private void PatternArea_PatternClicked(int channelIdx, int patternIdx, bool setActive)
        {
            PatternClicked?.Invoke(channelIdx, patternIdx, setActive);
            channelNameArea?.SetSelectedChannel(channelIdx);
            App.SelectedChannelIndex = channelIdx;
        }

        private Point ControlToLocal(Control control, Point p)
        {
            return WindowToControl(control.ControlToWindow(p));
        }

        private void StartAltZoom(Control control, PointerEventArgs e)
        {
            StartCaptureOperation(control, e.Position, AltZoom);
        }

        private void StartDragSeekBar(Control control, PointerEventArgs e)
        {
            StartCaptureOperation(control, e.Position, DragSeekBar);
        }

        private void StartRectangleSelection(Control control, PointerEventArgs e, int patternIdx)
        {
            StartCaptureOperation(control, e.Position, SelectRectangle);
        }

        private void StartDragSelection(Control control, PointerEventArgs e, int patternIdx)
        {
            selectionDragAnchorPatternIdx = patternIdx;
            selectionDragAnchorPatternXFraction = (e.X + scrollX - GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(patternIdx), false)) / (float)GetPixelForNote(Song.GetPatternLength(patternIdx), false);
            StartCaptureOperation(control, e.Position, DragSelection);
        }

        private void StartColumnSelection(Control control, PointerEventArgs e)
        {
            StartCaptureOperation(control, e.Position, SelectColumn);
        }

        private void StartMobilePanning(Control control, PointerEventArgs e)
        {
            StartCaptureOperation(control, e.Position, MobilePan);
        }

        public override void OnContainerPointerDownNotify(Control control, PointerEventArgs e)
        {
            var p = ControlToLocal(control, e.Position);
            SetMouseLastPos(p);

            if (e.IsTouchEvent && captureOperation == null)
            {
                StartMobilePanning(control, e);
                return;
            }

            if (e.Middle)
            {
                panning = true;
                CapturePointer();
            }
            
            if (IsCursorInContainer())
            {
                SetHoverByCoords(p);
            }

            UpdateTooltipControl(control);
            ShowExpansionIcons = false;
        }

        public override void OnContainerPointerMoveNotify(Control control, PointerEventArgs e)
        {
            var p = ControlToLocal(control, e.Position);
            UpdateCaptureOperation(null, p); // Already passing control coords. Pass null instead of calculating twice.

            if (IsCursorInContainer())
            {
                SetHoverByCoords(p);
            }

            if (e.Middle)
                DoScroll(p - mouseLastPos);

            SetMouseLastPos(p);
            UpdateCursor();

            UpdateTooltipControl(control);
            ShowExpansionIcons = false;
        }

        public override void OnContainerPointerUpNotify(Control control, PointerEventArgs e)
        {
            EndCaptureOperation(control, e.Position);
            UpdateCursor();

            if (e.Right)
            {
                patternArea?.UpdateSelectedPatternRefCounts();
            }
        }

        private void CopySelectionToCursor(PatternLocation location, bool copy)
        {
            var channelDeltaIdx = location.ChannelIndex - selectionMin.ChannelIndex;
            var patternDeltaIdx = location.PatternIndex - selectionMin.PatternIndex;

            MoveCopyOrDuplicateSelection(channelDeltaIdx, patternDeltaIdx, true, copy);
        }

        private void MainContainer_ContainerTouchFlingNotify(Control sender, PointerEventArgs e)
        {
            if (canFling)
            {
                EndCaptureOperation(sender, e.Position);
                SetFlingVelocity(e.FlingVelocityX, e.FlingVelocityY);
            }
        }

        public override void OnContainerTouchScaleBeginNotify(Control control, PointerEventArgs e)
        {
            if (captureOperation != null)
            {
                Debug.Assert(captureOperation != MobileZoom);
                AbortCaptureOperation();
            }

            StartCaptureOperation(control, e.Position, MobileZoom);
        }

        public override void OnContainerTouchScaleNotify(Control control, PointerEventArgs e)
        {
            UpdateCaptureOperation(control, e.Position, e.TouchScale);
        }

        public override void OnContainerTouchScaleEndNotify(Control control, PointerEventArgs e)
        {
            EndCaptureOperation(control, e.Position);
        }

        public Pattern[,] GetSelectedPatterns(out Song.PatternCustomSetting[] customSettings)
        {
            customSettings = null;

            if (!IsSelectionValid())
                return null;

            var patterns = new Pattern[selectionMax.PatternIndex - selectionMin.PatternIndex + 1, selectionMax.ChannelIndex - selectionMin.ChannelIndex + 1];

            for (int i = 0; i < patterns.GetLength(0); i++)
            {
                for (int j = 0; j < patterns.GetLength(1); j++)
                {
                    patterns[i, j] = Song.Channels[selectionMin.ChannelIndex + j].PatternInstances[selectionMin.PatternIndex + i];
                }
            }

            if (IsValidTimeOnlySelection())
            {
                customSettings = new Song.PatternCustomSetting[patterns.GetLength(0)];

                for (int i = 0; i < patterns.GetLength(0); i++)
                    customSettings[i] = Song.GetPatternCustomSettings(selectionMin.PatternIndex + i).Clone();
            }

            return patterns;
        }

        public bool CanCopy   => IsActiveControl && IsSelectionValid();
        public bool CanPaste  => IsActiveControl && IsSelectionValid() && ClipboardUtils.ContainsPatterns;
        public bool CanDelete => CanCopy;
        public bool IsActiveControl => App != null && App.ActiveControl == this;

        public void Copy()
        {
            if (IsSelectionValid())
            {
                var selPatterns = GetSelectedPatterns(out var customSettings);
                ClipboardUtils.SavePatterns(App.Project, selPatterns, customSettings);
            }
        }

        public void Cut()
        {
            if (IsSelectionValid())
            {
                var selPatterns = GetSelectedPatterns(out var customSettings);
                ClipboardUtils.SavePatterns(App.Project, selPatterns, customSettings);
                CancelDragSelection(); // Safety in case a transaction is in progress.
                DeleteSelection(true, customSettings != null);
            }
        }

        private void PasteInternal(bool insert, bool extend, int repeat, ClipboardImportFlags instImportFlags, ClipboardImportFlags arpImportFlags, ClipboardImportFlags sampleImportFlags, ClipboardImportFlags patternImportFlags)
        {
            var createAnythingMissing =
                instImportFlags.HasFlag(ClipboardImportFlags.CreateMissing) ||
                arpImportFlags.HasFlag(ClipboardImportFlags.CreateMissing)  ||
                sampleImportFlags.HasFlag(ClipboardImportFlags.CreateMissing);

            App.UndoRedoManager.BeginTransaction(createAnythingMissing ? TransactionScope.Project : TransactionScope.Song, Song.Id);

            var song = Song;
            var patterns = ClipboardUtils.LoadPatterns(song, instImportFlags, arpImportFlags, sampleImportFlags, patternImportFlags, out var customSettings);

            if (patterns == null)
            {
                App.UndoRedoManager.AbortTransaction();
                return;
            }

            var numColumnsToPaste = patterns.GetLength(0) * repeat;

            if (numColumnsToPaste == 0)
            {
                App.UndoRedoManager.AbortTransaction();
                return;
            }

            if (insert)
            {
                if (extend)
                {
                    song.SetLength(Math.Min(numColumnsToPaste + song.Length, Song.MaxLength));
                }

                // Move everything to the right.
                for (int i = song.Length - 1; i >= selectionMin.PatternIndex + numColumnsToPaste; i--)
                {
                    var srcIndex = i - numColumnsToPaste;

                    for (int j = 0; j < song.Channels.Length; j++)
                    {
                        song.Channels[j].PatternInstances[i] = song.Channels[j].PatternInstances[srcIndex];
                    }

                    if (song.PatternHasCustomSettings(srcIndex))
                    {
                        var srcCustomSettings = song.GetPatternCustomSettings(srcIndex);
                        song.SetPatternCustomSettings(i, srcCustomSettings.patternLength, srcCustomSettings.beatLength, srcCustomSettings.groove, srcCustomSettings.groovePaddingMode);
                    }
                    else if (song.PatternHasCustomSettings(i))
                    {
                        song.ClearPatternCustomSettings(i);
                    }
                }

                // Clear everything where we are pasting.
                for (int i = selectionMin.PatternIndex; i < selectionMax.PatternIndex + numColumnsToPaste; i++)
                {
                    song.ClearPatternCustomSettings(i);

                    for (int j = 0; j < song.Channels.Length; j++)
                    {
                        song.Channels[j].PatternInstances[i] = null;
                    }
                }
            }
            
            // Then do the actual paste.
            var startPatternIndex = selectionMin.PatternIndex;

            for (int r = 0; r < repeat; r++)
            {
                for (int i = 0; i < patterns.GetLength(0); i++)
                {
                    for (int j = 0; j < patterns.GetLength(1); j++)
                    {
                        var pattern = patterns[i, j];

                        if (pattern != null && (i + startPatternIndex) < song.Length && song.Project.IsChannelActive(pattern.ChannelType))
                        {
                            var channelIdx = Channel.ChannelTypeToIndex(pattern.ChannelType, song.Project.ExpansionAudioMask, song.Project.ExpansionNumN163Channels);
                            pattern.RemoveUnsupportedChannelFeatures();
                            song.Channels[channelIdx].PatternInstances[i + startPatternIndex] = pattern;
                        }
                    }
                }

                if (customSettings != null)
                {
                    for (int i = 0; i < patterns.GetLength(0); i++)
                    {
                        if (customSettings[i].useCustomSettings)
                        {
                            Song.SetPatternCustomSettings(
                                i + startPatternIndex,
                                customSettings[i].patternLength,
                                customSettings[i].beatLength,
                                customSettings[i].groove,
                                customSettings[i].groovePaddingMode);
                        }
                        else
                        {
                            Song.ClearPatternCustomSettings(i + selectionMin.PatternIndex);
                        }
                    }
                }

                startPatternIndex += patterns.GetLength(0);
            }

            selectionMax.PatternIndex = selectionMin.PatternIndex + numColumnsToPaste - 1;
            selectionMin.ChannelIndex = 0;
            selectionMax.ChannelIndex = Song.Channels.Length - 1;

            song.InvalidateCumulativePatternCache();
            song.DeleteNotesPastMaxInstanceLength();

            App.UndoRedoManager.EndTransaction();
            PatternsPasted?.Invoke();
            SelectionChanged?.Invoke();
            RebuildChannelMap();
            MarkDirty();
        }

        private void PasteInternalWithConflictDialog(bool insert, bool extend, int repeat)
        {
            if (!ClipboardUtils.GetClipboardContentFlags(Song, false, out var instFlags, out var arpFlags, out var sampleFlags, out var patternFlags))
            {
                return;
            }

            var anyConflicts =
                instFlags    != ClipboardContentFlags.None ||
                arpFlags     != ClipboardContentFlags.None ||
                sampleFlags  != ClipboardContentFlags.None ||
                patternFlags != ClipboardContentFlags.None;

            if (anyConflicts)
            {
                var dlg = new PasteConflictDialog(window, instFlags, arpFlags, sampleFlags, patternFlags);
                dlg.ShowDialogAsync((r) =>
                {
                    if (r == DialogResult.OK)
                    {
                        PasteInternal(insert, extend, repeat, dlg.InstrumentFlags, dlg.ArpeggioFlags, dlg.DPCMSampleFlags, dlg.PatternFlags);
                    }
                });
            }
            else
            {
                PasteInternal(insert, extend, repeat, ClipboardImportFlags.MatchByName, ClipboardImportFlags.MatchByName, ClipboardImportFlags.MatchByName, ClipboardImportFlags.MatchByName);
            }
        }

        public void Paste()
        {
            if (!IsSelectionValid())
                return;

            PasteInternalWithConflictDialog(false, false, 1);
        }

        public void PasteSpecial()
        {
            if (!IsSelectionValid())
                return;

            var dialog = new PropertyDialog(ParentWindow, PasteSpecialTitle, 200);
            dialog.Properties.AddLabelCheckBox(InsertLabel, false, 0, InsertTooltip); // 0
            dialog.Properties.AddLabelCheckBox(ExtendSongLabel, false, 0, ExtendSongTooltip); // 1
            dialog.Properties.AddNumericUpDown(RepeatLabel.Colon, 1, 1, 32, 1, RepeatTooltip); // 2
            dialog.Properties.SetPropertyEnabled(1, false);
            dialog.Properties.PropertyChanged += PasteSpecialDialog_PropertyChanged;
            dialog.Properties.Build();

            dialog.ShowDialogAsync((r) =>
            {
                if (r == DialogResult.OK)
                {
                    PasteInternalWithConflictDialog(
                        dialog.Properties.GetPropertyValue<bool>(0),
                        dialog.Properties.GetPropertyValue<bool>(1),
                        dialog.Properties.GetPropertyValue<int> (2));
                }
            });
        }

        private void PasteSpecialDialog_PropertyChanged(PropertyPage props, int propIdx, int rowIdx, int colIdx, object value)
        {
            if (propIdx == 0)
                props.SetPropertyEnabled(1, (bool)value);
        }

        protected void UpdateCursor()
        {
            if (captureOperation == DragSelection)
            {
                Cursor = Cursors.DragCursor;
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        private int IncrementChannelIndex(int channelIdx, int rowDelta, bool clamp)
        {
            if (hideEmptyChannels)
            {
                var idx = channelToRow[channelIdx] + rowDelta;

                if (clamp)
                    idx = Utils.Clamp(idx, 0, rowToChannel.Length - 1);
                else if (idx < 0 || idx >= rowToChannel.Length)
                    return -1;

                return rowToChannel[idx];
            }
            else
            {
                var idx = channelIdx + rowDelta;

                if (clamp)
                    idx = Utils.Clamp(idx, 0, Song.Channels.Length - 1);

                return idx;
            }
        }

        private int IncrementPatternIndex(int patternIndex, int delta, bool clamp)
        {
            patternIndex += delta;

            if (clamp)
                patternIndex = Utils.Clamp(patternIndex, 0, Song.Length - 1);

            return patternIndex;
        }

        public Pattern CreateUniquePatternClone(Pattern pattern, Channel channel)
        {
            var newName = pattern.Name;
            if (!channel.IsPatternNameUnique(newName))
                newName = channel.GenerateUniquePatternNameSmart(pattern.Name);

            var newPattern = pattern.ShallowClone(channel);
            newPattern.RemoveUnsupportedChannelFeatures();
            newPattern.Color = Theme.RandomCustomColor();
            channel.RenamePattern(newPattern, newName);

            return newPattern;
        }

        private void MoveCopyOrDuplicateSelection(int rowIdxDelta, int patternIdxDelta, bool copy, bool duplicate)
        {
            App.UndoRedoManager.BeginTransaction(TransactionScope.Song, Song.Id);

            var tmpPatterns = GetSelectedPatterns(out var customSettings);

            if (!copy)
                DeleteSelection(false, customSettings != null && !copy, false);

            var duplicatePatternMap = new Dictionary<Pattern, Pattern>();

            for (int i = selectionMin.ChannelIndex; i <= selectionMax.ChannelIndex; i++)
            {
                for (int j = selectionMin.PatternIndex; j <= selectionMax.PatternIndex; j++)
                {
                    var ni = IncrementChannelIndex(i, rowIdxDelta, false);
                    var nj = IncrementPatternIndex(j, patternIdxDelta, false);

                    if (nj >= 0 && nj < Song.Length && ni >= 0 && ni < Song.Channels.Length)
                    {
                        var sourcePattern = tmpPatterns[j - selectionMin.PatternIndex, i - selectionMin.ChannelIndex];

                        if (duplicate && sourcePattern != null)
                        {
                            if (!duplicatePatternMap.TryGetValue(sourcePattern, out var duplicatedPattern))
                            {
                                var destChannel = Song.Channels[ni];
                                duplicatedPattern = CreateUniquePatternClone(sourcePattern, destChannel);
                                duplicatePatternMap.Add(sourcePattern, duplicatedPattern);
                            }
                            Song.Channels[ni].PatternInstances[nj] = duplicatedPattern;
                        }
                        else
                        {
                            Song.Channels[ni].PatternInstances[nj] = sourcePattern;
                        }
                    }
                }
            }

            if (customSettings != null)
            {
                for (int j = selectionMin.PatternIndex; j <= selectionMax.PatternIndex; j++)
                {
                    var settings = customSettings[j - selectionMin.PatternIndex];

                    var nj = j + patternIdxDelta;
                    if (nj >= 0 && nj < Song.Length)
                    {
                        if (settings.useCustomSettings)
                        {
                            Song.SetPatternCustomSettings(
                                nj,
                                customSettings[j - selectionMin.PatternIndex].patternLength,
                                customSettings[j - selectionMin.PatternIndex].beatLength,
                                customSettings[j - selectionMin.PatternIndex].groove,
                                customSettings[j - selectionMin.PatternIndex].groovePaddingMode);
                        }
                        else
                        {
                            Song.ClearPatternCustomSettings(nj);
                        }
                    }
                }
            }

            Song.RemoveUnsupportedEffects();
            Song.RemoveUnsupportedInstruments();
            Song.DeleteNotesPastMaxInstanceLength();
            Song.InvalidateCumulativePatternCache();

            App.UndoRedoManager.EndTransaction();
        }

        private void EndDragSelection(Point p)
        {
            if (captureThresholdMet)
            {
                if (!IsSelectionValid()) // No clue how we end up here with invalid selection.
                {
                    CancelDragSelection();
                }
                else
                {
                    var noteIdx = GetNoteForPixel(p.X - channelNameSizeX);

                    if (noteIdx >= 0 && noteIdx < Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                    {
                        var patternIdx = Song.PatternIndexFromAbsoluteNoteIndex(GetNoteForPixel(p.X - channelNameSizeX));
                        var patternIdxDelta = patternIdx - selectionDragAnchorPatternIdx;

                        var dragRowIdxStart   = captureRowIdx;
                        var dragRowIdxCurrent = GetRowIndexForCoord(p.Y);
                        var rowIdxDelta = dragRowIdxCurrent - dragRowIdxStart;

                        // No need to proceed if the patterns haven't moved.
                        if (rowIdxDelta == 0 && patternIdxDelta == 0)
                            return;

                        var copy = ModifierKeys.IsControlDown;
                        var duplicate = copy && ModifierKeys.IsShiftDown || rowIdxDelta != 0;

                        MoveCopyOrDuplicateSelection(rowIdxDelta, patternIdxDelta, copy, duplicate);

                        var timeOnly = IsValidTimeOnlySelection();

                        var newSelectionMin = new PatternLocation(
                            IncrementChannelIndex(selectionMin.ChannelIndex, rowIdxDelta, true),
                            IncrementPatternIndex(selectionMin.PatternIndex, patternIdxDelta, true));
                        var newSelectionMax = new PatternLocation(
                            IncrementChannelIndex(selectionMax.ChannelIndex, rowIdxDelta, true),
                            IncrementPatternIndex(selectionMax.PatternIndex, patternIdxDelta, true));

                        if (timeOnly)
                        {
                            newSelectionMin.ChannelIndex = 0;
                            newSelectionMax.ChannelIndex = Song.Channels.Length - 1;
                        }

                        SetSelection(newSelectionMin, newSelectionMax, timeOnly);

                        if (HasHighlightedPattern())
                        {

                            highlightLocation = new PatternLocation(
                                IncrementChannelIndex(highlightLocation.ChannelIndex, rowIdxDelta, true),
                                IncrementPatternIndex(highlightLocation.PatternIndex, patternIdxDelta, true));
                        }

                        MarkDirty();
                        PatternModified?.Invoke();
                        SelectionChanged?.Invoke();
                    }
                }
            }
        }

        private void EndCaptureOperation(Control control, Point p)
        {
            if (captureOperation != null)
            {
                if (control != null)
                    p = ControlToLocal(control, p);

                if (captureOperation == DragSelection)
                {
                    EndDragSelection(p);
                }
                else if (captureOperation == DragSeekBar)
                {
                    UpdateSeekDrag(p, true);
                }
                else if (captureOperation == MobilePan || captureOperation == MobileZoom)
                {
                    canFling = true;
                }

                panning = false;
                captureOperation = null;
                ReleasePointer();
                MarkDirty();
            }
        }

        private void AbortCaptureOperation()
        {
            if (captureOperation != null)
            {
                if (App.UndoRedoManager.HasTransactionInProgress)
                    App.UndoRedoManager.AbortTransaction();

                panning = false;
                canFling = false;
                captureOperation = null;

                ReleasePointer();
                MarkDirty();
            }
            else
            {
                Debug.Assert(!App.UndoRedoManager.HasTransactionInProgress);
            }
        }

        protected void CancelDragSelection()
        {
            if (captureOperation == DragSelection)
            {
                selectionDragAnchorPatternIdx = -1;
                selectionDragAnchorPatternXFraction = -1.0f;
                captureOperation = null;
            }

            captureMousePos = new Point(-1, -1);
        }

        public void DeleteSelection()
        {
            DeleteSelection(true, IsValidTimeOnlySelection());
        }

        private void DeleteSelection(bool trans = true, bool clearCustomSettings = false, bool deleteNotesPastMax = true)
        {
            if (trans)
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Song, Song.Id);
            }

            for (int i = selectionMin.ChannelIndex; i <= selectionMax.ChannelIndex; i++)
            {
                for (int j = selectionMin.PatternIndex; j <= selectionMax.PatternIndex; j++)
                {
                    var pattern = Song.Channels[i].PatternInstances[j];
                    if (pattern != null)
                        patternArea?.InvalidatePattern(pattern);
                    Song.Channels[i].PatternInstances[j] = null;
                }
            }

            if (clearCustomSettings)
            {
                for (int j = selectionMin.PatternIndex; j <= selectionMax.PatternIndex; j++)
                    Song.ClearPatternCustomSettings(j);
            }

            Song.InvalidateCumulativePatternCache();

            if (deleteNotesPastMax)
                Song.DeleteNotesPastMaxInstanceLength();

            if (trans)
            {
                ClearSelection();
                App.UndoRedoManager.EndTransaction();
                MarkDirty();
                PatternModified?.Invoke();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Keys.Escape)
            {
                CancelDragSelection();
                UpdateCursor();
                ClearSelection();
                MarkDirty();
            }
            else if (IsActiveControl)
            {
                if (Settings.CopyShortcut.Matches(e))
                { 
                    Copy();
                }
                else if (Settings.CutShortcut.Matches(e))
                { 
                    Cut();
                }
                else if (Settings.PasteShortcut.Matches(e))
                { 
                    Paste();
                }
                else if (Settings.PasteSpecialShortcut.Matches(e))
                { 
                    PasteSpecial();
                }
                else if (Settings.DeleteShortcut.Matches(e) && IsSelectionValid())
                {
                    CancelDragSelection();
                    DeleteSelection();
                }
                else if (IsActiveControl && Settings.SelectAllShortcut.Matches(e))
                {
                    SetSelection(new PatternLocation(0, 0), new PatternLocation(Song.Channels.Length - 1, Song.Length - 1), true);
                }
            }

            if (captureOperation == DragSelection)
            {
                UpdateCursor();
                MarkDirty();
            }
            
/*             if (IsActiveControl)
                UpdateToolTip(null); */
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (captureOperation == DragSelection)
            {
                UpdateCursor();
                MarkDirty();
            }

/*             if (IsActiveControl)
                UpdateToolTip(null); */
        }

        private void UpdateSeekDrag(Point p, bool final)
        {
            ScrollIfNearEdge(p.X, p.Y);

            dragSeekPosition = Utils.Clamp(GetNoteForPixel(p.X - channelNameSizeX), 0, Song.GetPatternStartAbsoluteNoteIndex(Song.Length) - 1);

            if (final)
                App.SeekSong(dragSeekPosition);

            MarkDirty();
        }

        private void ScrollIfNearEdge(int x, int y, bool scrollHorizontal = true, bool scrollVertical = false)
        {
            if (scrollHorizontal)
            {
                int posMinX = 0;
                int posMaxX = Platform.IsDesktop ? width + channelNameSizeX : (IsLandscape ? width + headerSizeY : width);
                int marginMinX = channelNameSizeX;
                int marginMaxX = Platform.IsDesktop ? channelNameSizeX : headerSizeY;

                scrollX += Utils.ComputeScrollAmount(x, posMinX, marginMinX, App.AverageTickRate * ScrollSpeedFactor, true);
                scrollX += Utils.ComputeScrollAmount(x, posMaxX, marginMaxX, App.AverageTickRate * ScrollSpeedFactor, false);
                ClampScroll();
            }

            if (scrollVertical)
            {
                int posMinY = 0;
                int posMaxY = Platform.IsMobile && !IsLandscape || Platform.IsDesktop ? height + headerSizeY : height;
                int marginMinY = headerSizeY;
                int marginMaxY = headerSizeY;

                scrollY += Utils.ComputeScrollAmount(y, posMinY, marginMinY, App.AverageTickRate * ScrollSpeedFactor, true);
                scrollY += Utils.ComputeScrollAmount(y, posMaxY, marginMaxY, App.AverageTickRate * ScrollSpeedFactor, false);
                ClampScroll();
            }
        }

        private void UpdateSelection(int x, int y, bool timeOnly, bool first = false)
        {
            ScrollIfNearEdge(x, y, true, !timeOnly && CanScrollVertically());

            if (first)
            {
                Debug.Assert(captureChannelIdx >= 0 && capturePatternIdx >= 0);

                selectionMin.PatternIndex = capturePatternIdx;
                selectionMax.PatternIndex = capturePatternIdx;

                if (timeOnly)
                {
                    selectionMin.ChannelIndex = 0;
                    selectionMax.ChannelIndex = Song.Channels.Length - 1;
                    timeOnlySelection = true;
                }
                else
                {
                    selectionMin.ChannelIndex = captureChannelIdx;
                    selectionMax.ChannelIndex = captureChannelIdx;
                    timeOnlySelection = false;
                }
            }
            else
            {
                GetClampedPatternForCoord(x, y, out var channelIdx, out var patternIdx);

                selectionMin.PatternIndex = Math.Min(patternIdx, capturePatternIdx);
                selectionMax.PatternIndex = Math.Max(patternIdx, capturePatternIdx);

                if (timeOnly)
                {
                    selectionMin.ChannelIndex = 0;
                    selectionMax.ChannelIndex = Song.Channels.Length - 1;
                    timeOnlySelection = true;
                }
                else
                {
                    selectionMin.ChannelIndex = Math.Min(channelIdx, captureChannelIdx);
                    selectionMax.ChannelIndex = Math.Max(channelIdx, captureChannelIdx);
                    timeOnlySelection = false;
                }
            }

            MarkDirty();
            SelectionChanged?.Invoke();
        }

        private void UpdateDragSelection(int x, int y)
        {
            ScrollIfNearEdge(x, y, true, Platform.IsMobile || CanScrollVertically());
            MarkDirty();
        }

        private void UpdateAltZoom(int x, int y)
        {
            var deltaY = y - captureMousePos.Y;

            if (Math.Abs(deltaY) > 50)
            {
                ZoomAtLocation(x, deltaY < 0.0f ? 2.0f : 0.5f);
                captureMousePos.Y = y;
            }
        }

        private void UpdateTooltipControl(Control control)
        {
            var tooltip = control.ToolTip;
            if (string.IsNullOrEmpty(tooltip))
                tooltip = control.ParentContainer.ToolTip;
            App.SetToolTip(tooltip);
        }

        private bool CanScrollVertically()
        {
            GetMinMaxScroll(out _, out _, out var minScrollY, out var maxScrollY);
            return minScrollY != maxScrollY;
        }

        private void UpdateCaptureOperation(Control control, Point p, float scale = 1.0f, bool realTime = false)
        {
            const int CaptureThreshold = Platform.IsDesktop ? 5 : 50;

            if (control != null)
                p = ControlToLocal(control, p);

            var captureThresholdJustMet = false;
            var x = p.X;
            var y = p.Y;

            if (captureOperation != null)
            {
                if (!captureThresholdMet)
                {
                    if (Math.Abs(x - captureMousePos.X) >= CaptureThreshold || Math.Abs(y - captureMousePos.Y) >= CaptureThreshold)
                    {
                        captureThresholdMet = true;
                        captureThresholdJustMet = true;
                    }
                }
            }

            if (captureOperation != null && captureThresholdMet && (captureRealTimeUpdate || !realTime))
            {
                if (captureOperation == SelectColumn)
                {
                    UpdateSelection(x, y, true, captureThresholdJustMet);
                }
                else if (captureOperation == SelectRectangle)
                {
                    UpdateSelection(x, y, false, captureThresholdJustMet);
                }
                else if (captureOperation == AltZoom)
                {
                    UpdateAltZoom(x, y);
                }
                else if (captureOperation == DragSeekBar)
                {
                    UpdateSeekDrag(p, false);
                }
                else if (captureOperation == MobilePan)
                {
                    DoScroll(p - mouseLastPos);
                }
                else if (captureOperation == DragSelection)
                {
                    UpdateDragSelection(x, y);
                }
                else if (captureOperation == MobileZoom)
                {
                    ZoomAtLocation(x, scale);
                    DoScroll(p - mouseLastPos);
                    SetMouseLastPos(p);
                }
                else
                {
                    MarkDirty();
                }
            }
        }

        protected override void OnPointerMove(PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                //OnTouchMove(e);
                return;
            }

            bool middle = e.Middle || (e.Left && ModifierKeys.IsAltDown && Settings.AltLeftForMiddle);

            UpdateCursor();
            UpdateCaptureOperation(null, e.Position);

            if (middle)
                DoScroll(e.Position - mouseLastPos);

            if (IsCursorInContainer())
            {
                SetHoverByCoords(e.Position);
            }

            SetMouseLastPos(e.Position);
            ShowExpansionIcons = false;
        }

        private void SetHoverByCoords(Point p)
        {
            if (Platform.IsDesktop)
            {
                p = new Point(GetPatternIndexForCoord(p.X), GetRowIndexForCoord(p.Y));
                SetHover(p);
            }
        }

        public void SetHover(Point p)
        {
            if (Platform.IsDesktop)
            {
                if (hoverPos.X != p.X || hoverPos.Y != p.Y)
                {
                    hoverPos = new Point(p.X, p.Y);
                    channelNameArea?.SetHover(GetRowToChannelForIndex(hoverPos.Y));
                    MarkDirty();
                }
            }
        }

        private bool IsCursorInContainer()
        {
            if (!window.IsCursorInWindow)
                return false;

            var p = ScreenToControl(CursorPosition);
            return p.X >= 0 && p.X < width && p.Y >= 0 && p.Y < height;
        }

        private void ClearHover()
        {
            if (Platform.IsDesktop)
            {
                SetHover(new Point(-1, -1));
            }
        }

        private void ConditionalClearHover()
        {
            if (!IsCursorInContainer())
            {
                ClearHover();
            }
        }

        public override void OnContainerPointerEnterNotify(Control control, EventArgs e)
        {
            UpdateTooltipControl(control);
        }

        public override void OnContainerPointerLeaveNotify(Control control, EventArgs e)
        {
            ConditionalClearHover();
        }

        protected override void OnPointerLeave(EventArgs e)
        {
            ConditionalClearHover();
        }

        private void EditPatternProperties(Point pt, Pattern pattern, PatternLocation location, bool selection = true)
        {
            bool multipleChannelsSelected = selection && IsSelectionValid() && (selectionMax.ChannelIndex != selectionMin.ChannelIndex);
            bool multiplePatternsSelected = selection && IsSelectionValid() && ((selectionMax.ChannelIndex != selectionMin.ChannelIndex) || (selectionMin.PatternIndex != selectionMax.PatternIndex));

            var dlg = new PropertyDialog(ParentWindow, PatternPropertiesTitle, new Point(left + pt.X, top + pt.Y), 240, false, false, false);
            dlg.Properties.AddColoredTextBox(multiplePatternsSelected ? MultiplePatternsSelectedLabel : pattern.Name, pattern.Color);
            dlg.Properties.SetPropertyEnabled(0, !multiplePatternsSelected);
            dlg.Properties.AddColorPicker(pattern.Color);
            dlg.Properties.Build();

            dlg.ShowDialogAsync((r) =>
            {
                if (r == DialogResult.OK)
                {
                    if (!multipleChannelsSelected)
                        App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, location.ChannelIndex);
                    else
                        App.UndoRedoManager.BeginTransaction(TransactionScope.Song, Song.Id);

                    var newName  = dlg.Properties.GetPropertyValue<string>(0).Trim();
                    var newColor = dlg.Properties.GetPropertyValue<Color>(1);

                    if (multiplePatternsSelected)
                    {
                        for (int i = selectionMin.ChannelIndex; i <= selectionMax.ChannelIndex; i++)
                        {
                            for (int j = selectionMin.PatternIndex; j <= selectionMax.PatternIndex; j++)
                            {
                                var pat = Song.Channels[i].PatternInstances[j];
                                if (pat != null)
                                    pat.Color = newColor;
                            }
                        }
                        App.UndoRedoManager.EndTransaction();
                    }
                    else if (Song.Channels[location.ChannelIndex].RenamePattern(pattern, newName))
                    {
                        pattern.Color = newColor;
                        App.UndoRedoManager.EndTransaction();
                    }
                    else
                    {
                        App.UndoRedoManager.AbortTransaction();
                        App.DisplayNotification(ErrorRenamingPattern, true);
                    }

                    MarkDirty();
                    PatternModified?.Invoke();
                }
            });
        }

        private void ZoomAtLocation(int x, float scale)
        {
            if (scale == 1.0f)
                return;

            // When continuously following, zoom at the seek bar location.
            if (continuouslyFollowing)
                x = (int)(Width * Settings.FollowPercent);

            Debug.Assert(Platform.IsMobile || scale == 0.5f || scale == 2.0f);

            var pixelX = x - channelNameSizeX;
            var absoluteX = pixelX + scrollX;
            var prevNoteSizeX = noteSizeX;

            zoom *= scale;
            zoom = Utils.Clamp(zoom, MinZoom, MaxZoom);

            // This will update the noteSizeX.
            UpdateRenderCoords();

            absoluteX = (int)Math.Round(absoluteX * (noteSizeX / (double)prevNoteSizeX));
            scrollX = absoluteX - pixelX;

            ClampScroll();
            MarkDirty();
        }

        public override void OnContainerMouseWheelNotify(Control control, PointerEventArgs e)
        {
            // TODO: Vertical zooming in the channel name area once sequencer resizing is implemented.
            if (Settings.TrackPadControls && !ModifierKeys.IsControlDown && !ModifierKeys.IsAltDown)
            {
                if (CanScrollVertically() && !ModifierKeys.IsShiftDown)
                    scrollY -= Utils.SignedCeil(e.ScrollY);
                else
                    scrollX -= Utils.SignedCeil(e.ScrollY);

                ClampScroll();
                MarkDirty();
            }
            else
            {
                var p = ControlToLocal(control, e.Position);
                ZoomAtLocation(p.X, e.ScrollY < 0.0f ? 0.5f : 2.0f);
            }
        }

        public override void OnContainerMouseHorizontalWheelNotify(Control control, PointerEventArgs e)
        {
            scrollX += Utils.SignedCeil(e.ScrollX);
            ClampScroll();
            MarkDirty();
        }

        protected bool EnsureSeekBarVisible(float percent = float.MinValue)
        {
            if (percent == float.MinValue)
                percent = Settings.FollowPercent;

            var seekX = GetPixelForNote(App.CurrentFrame);
            var minX = 0;
            var maxX = (int)((width - channelNameSizeX) * percent);

            // Keep everything visible 
            if (seekX < minX)
                scrollX -= (minX - seekX);
            else if (seekX > maxX)
                scrollX += (seekX - maxX);

            ClampScroll();

            seekX = GetPixelForNote(App.CurrentFrame);
            return seekX == maxX;
        }

        public void UpdateFollowMode(bool force = false)
        {
            continuouslyFollowing = false;

            if ((App.IsPlaying || force) && App.FollowModeEnabled && Settings.FollowSync != Settings.FollowSyncPianoRoll && !panning && 
                captureOperation == null && !window.IsAsyncDialogInProgress && !window.IsOutOfProcessDialogInProgress)
            {
                var frame = App.CurrentFrame;
                var seekX = GetPixelForNote(App.CurrentFrame);

                if (Settings.FollowMode == Settings.FollowModeJump)
                {
                    var maxX = width - channelNameSizeX;
                    if (seekX < 0 || seekX > maxX)
                        scrollX = GetPixelForNote(frame, false);
                }
                else
                {
                    continuouslyFollowing = EnsureSeekBarVisible();
                }

                ClampScroll();
            }
        }

        private void TickFling(float delta)
        {
            if (flingVelX != 0.0f ||
                flingVelY != 0.0f)
            {
                var deltaPixelX = (int)Math.Round(flingVelX * delta);
                var deltaPixelY = (int)Math.Round(flingVelY * delta);

                if ((deltaPixelX != 0 || deltaPixelY != 0) && DoScroll(new Point(deltaPixelX, deltaPixelY)))
                {
                    flingVelX *= (float)Math.Exp(delta * -4.5f);
                    flingVelY *= (float)Math.Exp(delta * -4.5f);
                }
                else
                {
                    flingVelX = 0.0f;
                    flingVelY = 0.0f;
                }
            }
        }

        public override void Tick(float delta)
        {
            if (App == null)
                return;

            UpdateCaptureOperation(null, mouseLastPos, 1.0f, true);
            UpdateFollowMode();
            TickFling(delta);
        }

        public void SongModified()
        {
            UpdateRenderCoords();
            InvalidatePatternCache();
            ClearSelection();
            ClampScroll();
            MarkDirty();
        }

        public void InvalidatePatternCache()
        {
            patternArea?.InvalidateCache();
            MarkDirty();
        }

        public void Serialize(ProjectBuffer buffer)
        {
            if (Settings.RestoreViewOnUndoRedo || buffer.IsWriting)
            {
                buffer.Serialize(ref scrollX);
                buffer.Serialize(ref zoom);
            }
            else
            {
                var dummyScroll = 0;
                var dummyZoom = 0.0f;
                buffer.Serialize(ref dummyScroll);
                buffer.Serialize(ref dummyZoom);
            }

            buffer.Serialize(ref selectionMin.ChannelIndex);
            buffer.Serialize(ref selectionMax.ChannelIndex);
            buffer.Serialize(ref selectionMin.PatternIndex);
            buffer.Serialize(ref selectionMax.PatternIndex);
            buffer.Serialize(ref timeOnlySelection);
            buffer.Serialize(ref hideEmptyChannels);

            if (buffer.IsReading)
            {
                // TODO: This is overly aggressive. We should have the 
                // scope on the transaction on the buffer and filter by that.
                channelNameArea?.Refresh();
                patternArea?.UpdateSelectedPatternRefCounts();
                InvalidatePatternCache();
                UpdateRenderCoords();
                CancelDragSelection();
                MarkDirty();
            }
        }
    }
}
