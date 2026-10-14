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
        const int DefaultPatternNamePosX     = 2;
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
        int channelIconPosX;   
        int channelIconPosY;   
        int channelNamePosX;   
        int patternNamePosX;
        int scrollBarThickness;
        int minScrollBarLength;
        int virtualSizeY;
        int numRows;
        float noteSizeX;
        bool allowVerticalScrolling;

        int scrollX = 0;
        int scrollY = 0;
        int mouseLastX = 0;
        int mouseLastY = 0;
        int captureMouseX = -1;
        int captureMouseY = -1;
        int captureScrollX = -1;
        int captureScrollY = -1;
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
        CaptureOperation captureOperation = CaptureOperation.None;
        bool panning = false;
        bool canFling = false;
        bool continuouslyFollowing = false;
        bool captureThresholdMet = false;
        bool mouseMovedDuringCapture = false;
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
        Dictionary<Pattern, int> patternRefCounts = [];

        // Hover tracking
        int hoverRow = -1;
        int hoverPattern = -1;

        PatternBitmapCache patternCache;

        Color selectedPatternVisibleColor   = Color.FromArgb(64, Theme.LightGreyColor1);
        Color selectedPatternInvisibleColor = Color.FromArgb(16, Theme.LightGreyColor1);

        TextureAtlasRef bmpInstantiate;
        TextureAtlasRef bmpDuplicate;
        TextureAtlasRef bmpDuplicateMove;
        TextureAtlasRef bmpMenuInstance;

        enum CaptureOperation
        {
            None,
            SelectColumn,
            SelectRectangle,
            DragSelection,
            AltZoom,
            DragSeekBar,
            ScrollBarX,
            ScrollBarY,
            MobileZoom,
            MobilePan,
        }

        static readonly bool[] captureNeedsThreshold = new[]
        {
            false, // None
            Platform.IsMobile,  // SelectColumn
            false, // SelectRectangle
            true,  // DragSelection
            false, // AltZoom
            false, // DragSeekBar
            false, // ScrollBarX
            false, // ScrollBarY
            false, // MobileZoom,
            false, // MobilePan,
        };

        static readonly bool[] captureWantsRealTimeUpdate = new[]
        {
            false, // None
            true,  // SelectColumn
            true,  // SelectRectangle
            true,  // DragSelection
            false, // AltZoom
            true,  // DragSeekBar
            false, // ScrollBarX
            false, // ScrollBarY
            false, // MobileZoom,
            false, // MobilePan,
        };

        private ChannelNames channelNames;
        private SequencerTimeline timeline;

        public delegate void PatternClickedDelegate(int channelIdx, int patternIdx, bool setActive);
        public delegate void ChannelDelegate(int channelIdx);
        public delegate void EmptyDelegate();

        public event PatternClickedDelegate PatternClicked;
        public event EmptyDelegate PatternModified;
        public event EmptyDelegate PatternsPasted;
        public event EmptyDelegate SelectionChanged;

        public Color SelectedPatternInvisibleColor => selectedPatternInvisibleColor;
        public Color SelectedPatternVisibleColor   => selectedPatternVisibleColor;
        public PatternLocation SelectionMin => selectionMin;
        public PatternLocation SelectionMax => selectionMax;
        public bool HasCaptureOperation => captureOperation != CaptureOperation.None;
        public int ChannelIconPosX  => channelIconPosX;
        public int ChannelIconPosY  => channelIconPosY;
        public int ChannelNamePosX  => channelNamePosX;
        public int ChannelNameSizeX => channelNameSizeX;
        public int ChannelSizeY     => channelSizeY;
        public int HeaderSizeY      => headerSizeY;
        public int HoverPattern     => hoverPattern;
        public int HoverRow         => hoverRow;
        public int SeekX            => GetPixelForNote(GetSeekFrameToDraw());

        public float BitmapScale        => bitmapScale;
        public float ChannelBitmapScale => channelBitmapScale;


        #region Localization

        // Tooltips
        LocalizedString AddPatternTooltip;
        LocalizedString SetLoopPointTooltip;
        LocalizedString PanTooltip;
        LocalizedString SelectRectangleTooltip;
        LocalizedString OrTooltip;
        LocalizedString DeletePatternTooltip;
        LocalizedString MoreOptionsTooltip;
        LocalizedString MovePatternTooltip;
        LocalizedString ClonePatternTooltip;

        // Context menus
        LocalizedString GoToPianoRollLabel;
        LocalizedString ExpandSelectionLabel;
        LocalizedString InstanciateHereLabel;
        LocalizedString DuplicateHereLabel;
        LocalizedString ClearSelectionLabel;
        LocalizedString DeleteSelectionLabel;
        LocalizedString SelectedPatternPropertiesLabel;
        LocalizedString PatternPropertiesLabel;
        LocalizedString DeletePatternLabel;
        LocalizedString MakePatternsUniqueLabel;

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
            patternNamePosX    = DpiScaling.ScaleForWindow(DefaultPatternNamePosX);
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

            RebuildChannelMap();
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
            timeline?.Resize(width - channelNameSizeX, headerSizeY);
            timeline?.Move(channelNameSizeX, 0);

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

            channelNames?.Resize(channelNameSizeX, height);
            channelNames?.UpdateLayout();
        }

        public bool GetChannelVisibleForIndex(int index)
        {
            return channelVisible != null && channelVisible?.Length > index && channelVisible[index];
        }

        public int GetChannelToRowForIndex(int index)
        {
            return channelToRow != null && channelToRow.Length > index ? channelToRow[index] : -1;
        }

        public int GetRowToChannelForIndex(int index)
        {
            return rowToChannel != null && rowToChannel.Length > index ? rowToChannel[index] : -1;
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
            patternRefCounts?.Clear();
            SelectionChanged?.Invoke();
        }

        private void SetSelection(PatternLocation min, PatternLocation max, bool timeOnly = false)
        {
            selectionMin = min; 
            selectionMax = max;
            timeOnlySelection = timeOnly;
            SelectionChanged?.Invoke();
            UpdateSelectedPatternRefCounts();
        }

        private void EnsureSelectionInclude(PatternLocation location)
        {
            if (!IsSelectionValid())
            {
                SetSelection(location, location, false);
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

        private bool IsPatternColumnSelected(int patternIdx)
        {
            return IsValidTimeOnlySelection() && patternIdx >= selectionMin.PatternIndex && patternIdx <= selectionMax.PatternIndex;
        }

        private bool SelectionContainsMultiplePatterns()
        {
            return IsSelectionValid() && (selectionMax.ChannelIndex - selectionMin.ChannelIndex + 1) * (selectionMax.PatternIndex - selectionMin.PatternIndex + 1) > 1;
        }

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

        private void SetMouseLastPos(int x, int y)
        {
            mouseLastX = x;
            mouseLastY = y;
        }

        private void SetFlingVelocity(float x, float y)
        {
            flingVelX = x;
            flingVelY = y;
        }

        protected override void OnAddedToContainer()
        {
            UpdateRenderCoords();

            channelNames = new ChannelNames(this);
            channelNames.HoverChannelChanged += (ch) => SetHover(ch < 0 ? -1 : GetChannelToRowForIndex(ch), -1);
            AddControl(channelNames);

            timeline = new SequencerTimeline(this);
            timeline.CancelDragSeekBar += AbortCaptureOperation;
            timeline.ColumnSelect += StartColumnSelection;
            timeline.DragSeekBar += StartDragSeekBar;
            timeline.HoverPatternChanged += (x) => SetHover(-1, x);
            timeline.LoopPointToggled += SetLoopPoint;
            timeline.PatternModified += () => PatternModified?.Invoke();
            timeline.StartPanning += StartPanning;
            AddControl(timeline);

            base.OnAddedToContainer();

            var g = ParentWindow.Graphics;
            patternCache = new PatternBitmapCache(g);
            bmpInstantiate = g.GetTextureAtlasRef("Instance");
            bmpDuplicate = g.GetTextureAtlasRef("Duplicate");
            bmpDuplicateMove = g.GetTextureAtlasRef("DuplicateMove");

            if (Platform.IsMobile)
            {
                bitmapScale = DpiScaling.ScaleForWindowFloat(0.5f);
                channelBitmapScale = DpiScaling.ScaleForWindowFloat(0.25f);
            }

            bmpMenuInstance = g.GetTextureAtlasRef("MenuInstance");
        }

        protected override void OnResize(EventArgs e)
        {
            UpdateRenderCoords();
            ClampScroll();
        }

        protected bool IsSelectionValid()
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
            return captureOperation == CaptureOperation.DragSeekBar ? dragSeekPosition : App.CurrentFrame;
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

        private bool GetMinMaxSelectedRow(out int minSelRow, out int maxSelRow)
        {
            var minSelChannel = selectionMin.ChannelIndex;
            var maxSelChannel = selectionMax.ChannelIndex;
            
            while (minSelChannel < Song.Channels.Length && channelToRow[minSelChannel] < 0) minSelChannel++;
            while (maxSelChannel >= 0 && channelToRow[maxSelChannel] < 0) maxSelChannel--;

            if (minSelChannel < Song.Channels.Length && maxSelChannel >= 0)
            {
                minSelRow = channelToRow[minSelChannel];
                maxSelRow = channelToRow[maxSelChannel];
                return true;
            }
            else
            {
                minSelRow = -1;
                maxSelRow = -1;
                return false;
            }
        }

        protected void RenderPatternArea(Graphics g)
        {
            var c = g.DefaultCommandList;
            var b = g.BackgroundCommandList;
            var f = g.ForegroundCommandList;

            var actualSizeY = height - scrollBarThickness;
            var patternAreaSizeX = width - channelNameSizeX - (allowVerticalScrolling ? scrollBarThickness : 0);
            var isSelectionValid = IsSelectionValid();

            // Header
            c.PushTranslation(channelNameSizeX, 0); // TODO: Won't need this part when pattern area is separated.

            // Scrollable pattern area
            c.PushTranslation(0, headerSizeY);
            c.PushClipRegion(0, 1, patternAreaSizeX, height - headerSizeY - scrollBarThickness - 1);
            c.DrawLine(0, height - headerSizeY - 1, patternAreaSizeX, height - headerSizeY - 1, Theme.BlackColor);
            c.DrawLine(0, 0, patternAreaSizeX, 0, Theme.BlackColor);

            for (int i = MinVisiblePattern(); i < MaxVisiblePattern(); i++)
            {
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                var sx = GetPixelForNote(Song.GetPatternLength(i), false);
                var color = (i & 1) == 0 ? Theme.DarkGreyColor4 : Theme.DarkGreyColor2;
                b.FillRectangle(px, 0, px + sx, height - headerSizeY, color);
            }

            // Selection
            if (isSelectionValid)
            {
                if (GetMinMaxSelectedRow(out var minSelRow, out var maxSelRow))
                {
                    c.FillRectangle(
                        GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Math.Min(selectionMin.PatternIndex + 0, Song.Length))), channelSizeY * (minSelRow + 0) - scrollY,
                        GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Math.Min(selectionMax.PatternIndex + 1, Song.Length))), channelSizeY * (maxSelRow + 1) - scrollY,
                        IsActiveControl ? selectedPatternVisibleColor : selectedPatternInvisibleColor);
                }
            }

            // Vertical lines
            for (int i = Math.Max(1, MinVisiblePattern()); i <= MaxVisiblePattern(); i++)
            {
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                c.DrawLine(px, 0, px, height - scrollBarThickness, Theme.BlackColor);
            }

            c.PushTranslation(0, -scrollY);

            // Horizontal lines
            for (int i = 0, y = 0; i <= rowToChannel.Length; i++, y += channelSizeY)
                c.DrawLine(0, y, width, y, Theme.BlackColor);

            // TODO : This is really bad, since all the logic is in the rendering code. Make
            // this more like any other capture op eventually.
            if (captureOperation == CaptureOperation.DragSelection)
            {
                var pt = new Point(mouseLastX, mouseLastY);
                var noteIdx = GetNoteForPixel(pt.X - channelNameSizeX);

                if (noteIdx >= 0 && noteIdx < Song.GetPatternStartAbsoluteNoteIndex(Song.Length) && GetMinMaxSelectedRow(out var minSelRow, out var maxSelRow))
                {
                    var patternIdx = Song.PatternIndexFromAbsoluteNoteIndex(noteIdx);
                    var patternIdxDelta = patternIdx - selectionDragAnchorPatternIdx;

                    var dragRowIdxStart = captureRowIdx;
                    var dragRowIdxCurrent = GetRowIndexForCoord(pt.Y);
                    var rowIdxDelta = dragRowIdxCurrent - dragRowIdxStart;

                    for (int j = minSelRow + rowIdxDelta; j <= maxSelRow + rowIdxDelta; j++)
                    {
                        if (j < 0 || j >= rowToChannel.Length)
                            continue;

                        var y = j * channelSizeY;

                        // Center.
                        var patternSizeX = GetPixelForNote(Song.GetPatternLength(patternIdx), false);
                        var anchorOffsetLeftX = (int)(patternSizeX * selectionDragAnchorPatternXFraction);
                        var anchorOffsetRightX = (int)(patternSizeX * (1.0f - selectionDragAnchorPatternXFraction));

                        var instance = ModifierKeys.IsControlDown;
                        var duplicate = instance && ModifierKeys.IsShiftDown;

                        var bmpCopy = (TextureAtlasRef)null;
                        var bmpSize = DpiScaling.ScaleCustom(bmpDuplicate.ElementSize.Width, bitmapScale);

                        if (rowIdxDelta != 0)
                            bmpCopy = (duplicate || instance) ? bmpDuplicate : bmpDuplicateMove;
                        else
                            bmpCopy = duplicate ? bmpDuplicate : (instance ? bmpInstantiate : null);

                        c.PushTranslation(pt.X - channelNameSizeX, y);
                        c.FillAndDrawRectangle(-anchorOffsetLeftX, 0, -anchorOffsetLeftX + patternSizeX, channelSizeY, selectedPatternVisibleColor, Theme.BlackColor);

                        if (bmpCopy != null)
                            c.DrawTextureAtlas(bmpCopy, -anchorOffsetLeftX + patternSizeX / 2 - bmpSize / 2, channelSizeY / 2 - bmpSize / 2, bitmapScale, Theme.LightGreyColor1);

                        // Left side
                        for (int p = patternIdx - 1; p >= selectionMin.PatternIndex + patternIdxDelta && p >= 0; p--)
                        {
                            patternSizeX = GetPixelForNote(Song.GetPatternLength(p), false);
                            anchorOffsetLeftX += patternSizeX;

                            c.FillAndDrawRectangle(-anchorOffsetLeftX, 0, -anchorOffsetLeftX + patternSizeX, channelSizeY, selectedPatternVisibleColor, Theme.BlackColor);

                            if (bmpCopy != null)
                                c.DrawTextureAtlas(bmpCopy, -anchorOffsetLeftX + patternSizeX / 2 - bmpSize / 2, channelSizeY / 2 - bmpSize / 2, bitmapScale, Theme.LightGreyColor1);
                        }

                        // Right side
                        for (int p = patternIdx + 1; p <= selectionMax.PatternIndex + patternIdxDelta && p < Song.Length; p++)
                        {
                            patternSizeX = GetPixelForNote(Song.GetPatternLength(p), false);

                            c.FillAndDrawRectangle(anchorOffsetRightX, 0, anchorOffsetRightX + patternSizeX, channelSizeY, selectedPatternVisibleColor, Theme.BlackColor);

                            if (bmpCopy != null)
                                c.DrawTextureAtlas(bmpCopy, anchorOffsetRightX + patternSizeX / 2 - bmpSize / 2, channelSizeY / 2 - bmpSize / 2, bitmapScale, Theme.LightGreyColor1);

                            anchorOffsetRightX += patternSizeX;
                        }

                        c.PopTransform();
                    }
                }
            }

            // Patterns
            var patternCacheSizeY = channelSizeY - patternHeaderSizeY - 1;
            patternCache.Update(patternCacheSizeY);

            for (int pi = MinVisiblePattern(); pi < MaxVisiblePattern(); pi++)
            {
                var patternLen = Song.GetPatternLength(pi);
                var noteLen = Song.UsesFamiTrackerTempo ? 1 : Song.GetPatternNoteLength(pi);
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(pi));
                var sx = GetPixelForNote(patternLen, false);

                c.PushTranslation(px, 0);

                // TODO : Dont draw channels that are not visible!
                for (int ci = 0, py = 0; ci < Song.Channels.Length; ci++)
                {
                    if (channelVisible[ci])
                    {
                        var location = new PatternLocation(ci, pi);
                        var pattern = Song.GetPatternInstance(location);

                        if (pattern != null)
                        {
                            var bmp = patternCache.GetOrAddPattern(pattern, patternLen, noteLen, out var u0, out var v0, out var u1, out var v1);

                            c.PushTranslation(0, py);
                            c.FillRectangleGradient(1, 1, sx, patternHeaderSizeY, pattern.Color, pattern.Color.Scaled(0.8f), true, patternHeaderSizeY);
                            c.FillRectangle(1, patternHeaderSizeY, sx, channelSizeY, Color.FromArgb(75, pattern.Color));
                            c.DrawLine(0, patternHeaderSizeY, sx, patternHeaderSizeY, Theme.BlackColor);
                            c.DrawTexture(bmp, 1.0f, 1.0f + patternHeaderSizeY, sx - 1, patternCacheSizeY, u0, v0, u1, v1);
                            c.DrawText(pattern.Name, Fonts.FontSmall, patternNamePosX, 0, Theme.BlackColor, TextFlags.Left | TextFlags.Middle | TextFlags.Clip, sx - patternNamePosX, patternHeaderSizeY + 1);

                            var isSelected = IsPatternSelected(location);
                            if (isSelected)
                                c.DrawRectangle(0, 0, sx, channelSizeY, Theme.LightGreyColor1, 3, true, true);

                            c.PopTransform();

                            if (isSelectionValid && patternRefCounts.TryGetValue(pattern, out var count) && count > 1)
                            {
                                // TODO: Get the correct icon instead of resizing. This is a placeholder.
                                var scale = bitmapScale * (Platform.IsDesktop ? 1.0f : 0.25f);
                                var iconH = bmpMenuInstance.ElementSize.Height * scale;
                                var iconX = sx / 2 - iconH / 2;
                                var iconY = patternHeaderSizeY / 2 + channelSizeY / 2 - iconH / 2;

                                f.PushTranslation(0, py);
                                f.DrawTextureAtlas(bmpMenuInstance, iconX + 1, iconY + 1, scale, Theme.BlackColor);
                                f.DrawTextureAtlas(bmpMenuInstance, iconX, iconY, scale, Theme.WhiteColor);
                                f.PopTransform();
                            }
                        }

                        if (Platform.IsMobile && highlightLocation == location)
                        {
                            c.DrawRectangle(0, py, sx, py + channelSizeY, Theme.WhiteColor, 3, true, true);
                        }

                        py += channelSizeY;
                    }
                }

                c.PopTransform();
            }

            // Piano roll view rect
            if (App.GetPianoRollViewRange(out var pianoRollMinNoteIdx, out var pianoRollMaxNoteIdx, out var pianoRollChannelIndex) && channelToRow[pianoRollChannelIndex] >= 0)
            {
                c.PushTranslation(GetPixelForNote(pianoRollMinNoteIdx), channelToRow[pianoRollChannelIndex] * channelSizeY);
                c.DrawRectangle(1, patternHeaderSizeY + 1, GetPixelForNote(pianoRollMaxNoteIdx - pianoRollMinNoteIdx, false) - 1, channelSizeY - 1, Theme.LightGreyColor2);
                c.PopTransform();
            }

            // Seek bar
            b.DrawLine(SeekX, 0, SeekX, virtualSizeY, GetSeekBarColor(), 3);

            c.PopTransform();
            c.PopClipRegion();

            c.PopTransform();
            c.PopTransform();

            // Scroll bar (optional)
            if (GetScrollBarParams(true, out var scrollBarThumbPosX, out var scrollBarSizeThumbX, out var scrollBarSizeX))
            {
                c.PushTranslation(channelNameSizeX - 1, 0);
                c.FillAndDrawRectangle(0, actualSizeY, scrollBarSizeX, height - 1, Theme.DarkGreyColor4, Theme.BlackColor);
                c.FillAndDrawRectangle(scrollBarThumbPosX, actualSizeY, scrollBarThumbPosX + scrollBarSizeThumbX, height, Theme.MediumGreyColor1, Theme.BlackColor);
                c.PopTransform();
            }

            if (GetScrollBarParams(false, out var scrollBarThumbPosY, out var scrollBarThumbSizeY, out var scrollBarSizeY))
            {
                c.PushTranslation(0, headerSizeY);
                c.FillAndDrawRectangle(width - scrollBarThickness, 0, width, scrollBarSizeY, Theme.DarkGreyColor4, Theme.BlackColor);
                c.FillAndDrawRectangle(width - scrollBarThickness, scrollBarThumbPosY, width, scrollBarThumbPosY + scrollBarThumbSizeY, Theme.MediumGreyColor1, Theme.BlackColor);
                c.PopTransform();
            }
        }

        protected void RenderDebug(Graphics g)
        {
#if DEBUG
            if (Platform.IsMobile)
            {
                g.OverlayCommandList.FillRectangle(mouseLastX - 30, mouseLastY - 30, mouseLastX + 30, mouseLastY + 30, Theme.WhiteColor);
            }
#endif
        }

        protected override void OnRender(Graphics g)
        {
            // Happens when piano roll is maximized.
            if (height <= 1)
                return;

            base.OnRender(g);

            RenderPatternArea(g);
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
                patternCache.Remove(pattern);
            }
        }

#if DEBUG
        public void ValidateIntegrity()
        {
            if (patternCache != null)
            {
                patternCache.ValidateIntegrity();
            }
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

            channelNames?.SetScroll(scrollY);

            return scrolledX || scrolledY;
        }

        private bool DoScroll(int deltaX, int deltaY)
        {
            scrollX -= deltaX;
            scrollY -= deltaY; 
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
            return Utils.Clamp(((y - headerSizeY) + scrollY) / channelSizeY, 0, rowToChannel.Length - 1);
        }

        private int GetChannelIndexForCoord(int y)
        {
            return rowToChannel.Length == 0 ? -1 : rowToChannel[GetRowIndexForCoord(y)];
        }

        private void GetClampedPatternForCoord(int x, int y, out int channelIdx, out int patternIdx)
        {
            patternIdx = Song.PatternIndexFromAbsoluteNoteIndex(Utils.Clamp(GetNoteForPixel(x - channelNameSizeX), 0, Song.GetPatternStartAbsoluteNoteIndex(Song.Length) - 1));
            channelIdx = GetChannelIndexForCoord(y);
        }

        bool IsPointInPatternArea(int x, int y)
        {
            return x > channelNameSizeX && y > headerSizeY;
        }

        private void CaptureMouse(int x, int y, Control ctrl = null)
        {
            SetMouseLastPos(x, y);
            captureMouseX = x;
            captureMouseY = y;
            captureScrollX = scrollX;
            captureScrollY = scrollY;

            ctrl ??= this;
            ctrl.CapturePointer();
        }

        private void StartCaptureOperation(int x, int y, CaptureOperation op, Control ctrl = null)
        {
            Debug.Assert(captureOperation == CaptureOperation.None);

            CaptureMouse(x, y, ctrl);

            canFling = false;
            captureOperation = op;
            captureThresholdMet = !captureNeedsThreshold[(int)op];
            mouseMovedDuringCapture = false;
            captureRealTimeUpdate = captureWantsRealTimeUpdate[(int)op];
            captureRowIdx = GetRowIndexForCoord(y);
            GetClampedPatternForCoord(x, y, out captureChannelIdx, out capturePatternIdx);

            if (captureOperation == CaptureOperation.SelectRectangle)
                patternRefCounts.Clear();
        }

        private void SetLoopPoint(int patternIdx)
        {
            App.UndoRedoManager.BeginTransaction(TransactionScope.Song, Song.Id);
            Song.SetLoopPoint(Song.LoopPoint == patternIdx ? -1 : patternIdx);
            App.UndoRedoManager.EndTransaction();
            MarkDirty();
        }

        private void CreateNewPattern(PatternLocation location)
        {
            var channel = Song.Channels[location.ChannelIndex];

            App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, location.ChannelIndex);
            channel.PatternInstances[location.PatternIndex] = channel.CreatePattern();
            channel.InvalidateCumulativePatternCache();
            PatternClicked?.Invoke(location.ChannelIndex, location.PatternIndex, false);
            App.UndoRedoManager.EndTransaction();

            ClearSelection();
            MarkDirty();
        }

        private void DeletePattern(PatternLocation location)
        {
            var channel = Song.Channels[location.ChannelIndex];
            var pattern = channel.PatternInstances[location.PatternIndex];

            App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, location.ChannelIndex);
            patternCache.Remove(pattern);
            channel.PatternInstances[location.PatternIndex] = null;
            channel.InvalidateCumulativePatternCache();
            PatternModified?.Invoke();
            App.UndoRedoManager.EndTransaction();

            ClearSelection();
            MarkDirty();
        }

        private bool HandleMouseDownPan(PointerEventArgs e)
        {
            bool middle = e.Middle || (e.Left && ModifierKeys.IsAltDown && Settings.AltLeftForMiddle);

            if (middle && e.X > channelNameSizeX)
            {
                panning = true;
                CaptureMouse(e.X, e.Y);
                return true;
            }

            return false;
        }

        private bool HandleMouseDownScrollbar(PointerEventArgs e)
        {
            if (e.Left && scrollBarThickness > 0 && e.X > channelNameSizeX && e.Y > headerSizeY)
            {
                if (e.Y >= (height - scrollBarThickness) && GetScrollBarParams(true, out var scrollBarPosX, out var scrollBarSizeX, out _))
                {
                    var x = e.X - channelNameSizeX;
                    if (x < scrollBarPosX)
                    {
                        scrollX -= (width - channelNameSizeX);
                        ClampScroll();
                    }
                    else if (x > (scrollBarPosX + scrollBarSizeX))
                    {
                        scrollX += (width - channelNameSizeX);
                        ClampScroll();
                    }
                    else if (x >= scrollBarPosX && x <= (scrollBarPosX + scrollBarSizeX))
                    {
                        StartCaptureOperation(e.X, e.Y, CaptureOperation.ScrollBarX);
                    }
                    return true;
                }
                if (e.X >= (width - scrollBarThickness) && GetScrollBarParams(false, out var scrollBarThumbPosY, out var scrollBarThumbSizeY, out _))
                {
                    var y = e.Y - headerSizeY;
                    if (y < scrollBarThumbPosY)
                    {
                        scrollY -= (height - headerSizeY);
                        ClampScroll();
                        MarkDirty();
                    }
                    else if (y > (scrollBarThumbPosY + scrollBarThumbSizeY))
                    {
                        scrollX += (height - headerSizeY);
                        ClampScroll();
                        MarkDirty();
                    }
                    else if (y >= scrollBarThumbPosY && y <= (scrollBarThumbPosY + scrollBarThumbSizeY))
                    {
                        StartCaptureOperation(e.X, e.Y, CaptureOperation.ScrollBarY);
                    }
                    return true;
                }
            }

            return false;
        }

        private bool HandleMouseUpPatternArea(PointerEventArgs e)
        {
            return e.Right && HandleContextMenuPatternArea(e.X, e.Y);
        }

        private bool HandleMouseDownSetLoopPoint(PointerEventArgs e)
        {
            bool setLoop = Settings.SetLoopPointShortcut.IsKeyDown(ParentWindow);

            if (setLoop && e.X > channelNameSizeX && e.Left)
            {
                var patternIdx = GetPatternIndexForCoord(e.X);
                if (patternIdx >= 0)
                    SetLoopPoint(patternIdx);
                return true;
            }

            return false;
        }

        private bool HandleMouseDownAltZoom(PointerEventArgs e)
        {
            if (e.Right && ModifierKeys.IsAltDown && Settings.AltZoomAllowed && GetPatternForCoord(e.X, e.Y, out _))
            {
                StartCaptureOperation(e.X, e.Y, CaptureOperation.AltZoom);
                return true;
            }

            return false;
        }

        private void StartDragSeekBar(Control control, PointerEventArgs e)
        {
            var p = WindowToControl(control.ControlToWindow(e.Position));
            StartCaptureOperation(p.X, p.Y, CaptureOperation.DragSeekBar, control);
            UpdateSeekDrag(p.X, p.Y, false);
        }
        
        private void StartDragSelection(int x, int y, int patternIdx)
        {
            selectionDragAnchorPatternIdx = patternIdx;
            selectionDragAnchorPatternXFraction = (x - channelNameSizeX + scrollX - GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(patternIdx), false)) / (float)GetPixelForNote(Song.GetPatternLength(patternIdx), false);
            StartCaptureOperation(x, y, CaptureOperation.DragSelection);
        }

        private void StartColumnSelection(Control control, PointerEventArgs e)
        {
            var p = WindowToControl(control.ControlToWindow(e.Position));
            StartCaptureOperation(p.X, p.Y, CaptureOperation.SelectColumn, control);
        }

        private void StartPanning(Control control, PointerEventArgs e)
        {
            var p = WindowToControl(control.ControlToWindow(e.Position));
            panning = true;
            CaptureMouse(p.X, p.Y);
        }

        private bool HandleMouseDownPatternArea(PointerEventArgs e)
        {
            bool inPatternZone = GetPatternForCoord(e.X, e.Y, out var location);

            if (inPatternZone)
            {
                var pattern = Song.GetPatternInstance(location);

                if (e.Left)
                {
                     if (pattern == null)
                    {
                        CreateNewPattern(location);
                    }
                    else
                    {
                        if (pattern != null)
                        {
                            if (ModifierKeys.IsShiftDown && !ModifierKeys.IsControlDown)
                            {
                                DeletePattern(location);
                                return true;
                            }

                            PatternClicked?.Invoke(location.ChannelIndex, location.PatternIndex, false);
                        }

                        if (!IsPatternSelected(location))
                        {
                            SetSelection(location, location);
                            timeOnlySelection = false;
                            SelectionChanged?.Invoke();
                        }

                        StartDragSelection(e.X, e.Y, location.PatternIndex);
                    }

                    return true;
                }
                else if (e.Right)
                {
                    e.DelayRightClick(); // Need to wait to see difference between context menu or selection.
                    return true;
                }
            }

            return false;
        }

        public override void OnContainerPointerMoveNotify(Control control, PointerEventArgs e)
        {
            var p = WindowToControl(control.ControlToWindow(e.Position));
            var x = p.X;
            var y = p.Y;

            UpdateCaptureOperation(x, y);

            if (panning)
                DoScroll(x - mouseLastX, y - mouseLastY);

            SetMouseLastPos(x, y);
            UpdateCursor();
        }

        public override void OnContainerPointerUpNotify(Control control, PointerEventArgs e)
        {
            var p = WindowToControl(control.ControlToWindow(e.Position));
            EndCaptureOperation(p.X, p.Y);
            UpdateCursor();

            panning = false;
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                OnTouchDown(e);
                return;
            }

            if (captureOperation != CaptureOperation.None && (e.Left || e.Right))
                return;

            UpdateCursor();

            if (HandleMouseDownPan(e)) goto Handled;
            if (HandleMouseDownScrollbar(e)) goto Handled;
            if (HandleMouseDownSetLoopPoint(e)) goto Handled;
            if (HandleMouseDownAltZoom(e)) goto Handled;
            if (HandleMouseDownPatternArea(e)) goto Handled;

            return;

        Handled:
            MarkDirty();
        }

        private bool HandleMouseDownDelayedRectangleSelection(PointerEventArgs e)
        {
            if (e.Right && IsMouseInPatternZone(e))
            {
                StartCaptureOperation(e.X, e.Y, CaptureOperation.SelectRectangle);
                return true;
            }

            return false;
        }

        protected override void OnPointerDownDelayed(PointerEventArgs e)
        {

            if (HandleMouseDownDelayedRectangleSelection(e)) goto Handled;

            return;

        Handled:
            MarkDirty();
        }

        private bool HandleTouchDownPan(int x, int y)
        {
            if (IsPointInPatternArea(x, y))
            {
                StartCaptureOperation(x, y, CaptureOperation.MobilePan);
                return true;
            }

            return false;
        }

        private bool HandleTouchDownPatternArea(int x, int y)
        {
            if (IsPointInPatternArea(x, y))
            {
                bool inPatternZone = GetPatternForCoord(x, y, out var location);

                if (inPatternZone && highlightLocation == location)
                {
                    if (!IsPatternSelected(highlightLocation))
                        SetSelection(highlightLocation, highlightLocation);

                    StartDragSelection(x, y, highlightLocation.PatternIndex);
                    return true;
                }
            }

            return false;
        }

        private bool HandleTouchClickPatternArea(int x, int y)
        {
            bool inPatternZone = GetPatternForCoord(x, y, out var location);

            if (inPatternZone)
            {
                var pattern = Song.GetPatternInstance(location);

                if (pattern == null)
                {
                    CreateNewPattern(location);
                    SetHighlightedPattern(location);
                }
                else 
                {
                    if (highlightLocation == location)
                        ClearHighlightedPatern();
                    else
                        SetHighlightedPattern(location);

                    // Tapping inside the selection toggles highlight, but doesnt clear selection.
                    if (IsPatternSelected(location))
                        return true;
                }

                // CreateNewPattern clears the selection. Ugh, call after.
                SetSelection(location, location);

                return true;
            }

            return false;
        }

        private bool HandleTouchDoubleClickPatternArea(int x, int y)
        {
            bool inPatternZone = GetPatternForCoord(x, y, out var location);

            if (inPatternZone)
            {
                var pattern = Song.GetPatternInstance(location);

                if (pattern != null)
                {
                    DeletePattern(location);
                    ClearHighlightedPatern();
                }

                return true;
            }

            return false;
        }

        private void GotoPianoRoll(PatternLocation location)
        {
            PatternClicked?.Invoke(location.ChannelIndex, location.PatternIndex, true);
        }

        private void CopySelectionToCursor(bool copy)
        {
            var channelDeltaIdx = highlightLocation.ChannelIndex - selectionMin.ChannelIndex;
            var patternDeltaIdx = highlightLocation.PatternIndex - selectionMin.PatternIndex;

            MoveCopyOrDuplicateSelection(channelDeltaIdx, patternDeltaIdx, true, copy);
        }

        private bool UpdateSelectedPatternRefCounts()
        {
            if (!IsSelectionValid())
                return false;

            var selectedPatterns = new HashSet<Pattern>();

            foreach (var pattern in GetSelectedPatterns(out _))
            {
                if (pattern != null)
                    selectedPatterns.Add(pattern);
            }

            var counts = new Dictionary<Pattern, int>();
            bool duplicateFound = false;

            for (int i = 0; i < Song.Channels.Length; i++)
            {
                var channel = Song.Channels[i];

                for (int j = 0; j < Song.Length; j++)
                {
                    var pattern = channel.PatternInstances[j];
                    if (pattern == null || !selectedPatterns.Contains(pattern))
                        continue;

                    var count = counts.TryGetValue(pattern, out var c) ? c + 1 : 1;
                    counts[pattern] = count;

                    if (count > 1)
                        duplicateFound = true;
                }
            }

            patternRefCounts = counts;
            return duplicateFound;
        }

        private void MakeSelectedPatternsUnique()
        {
            if (patternRefCounts.Count == 0)
                return;

            App.UndoRedoManager.BeginTransaction(TransactionScope.Song, Song.Id);
            
            var patternCounts = new Dictionary<Pattern, int>(patternRefCounts); // Safety.

            for (int i = selectionMin.ChannelIndex; i <= selectionMax.ChannelIndex; i++)
            {
                var channel = Song.Channels[i];
                for (int j = selectionMin.PatternIndex; j <= selectionMax.PatternIndex; j++)
                {
                    var pattern = channel.PatternInstances[j];
                    if (pattern != null && patternCounts.TryGetValue(pattern, out var count) && count > 1)
                    {
                        var newPattern = CreateUniquePatternClone(pattern, channel);
                        channel.PatternInstances[j] = newPattern;
                        patternCounts[pattern]--;
                    }
                }
            }

            Song.DeleteNotesPastMaxInstanceLength();
            Song.InvalidateCumulativePatternCache();
            UpdateSelectedPatternRefCounts();

            App.UndoRedoManager.EndTransaction();
        }

        private bool HandleContextMenuPatternArea(int x, int y)
        {
            bool inPatternZone = GetPatternForCoord(x, y, out var location);

            if (inPatternZone)
            {
                var pattern = Song.GetPatternInstance(location);

                SetHighlightedPattern(location);

                var menu = new List<ContextMenuOption>();
                var sel  = new List<ContextMenuOption>();

                if (Platform.IsMobile)
                {
                    menu.Add(new ContextMenuOption("MenuPiano", GoToPianoRollLabel, () => { GotoPianoRoll(location); }));
                }
                else if (pattern != null &&!IsPatternSelected(location))
                {
                    SetSelection(location, location);
                }

                if (IsSelectionValid() && !IsPatternSelected(location))
                {
                    if (Platform.IsMobile)
                        menu.Add(new ContextMenuOption("MenuExpandSelection", ExpandSelectionLabel, () => { EnsureSelectionInclude(location); }));
                    if (selectionMin.ChannelIndex == location.ChannelIndex)
                        menu.Add(new ContextMenuOption("MenuInstance", InstanciateHereLabel, () => { CopySelectionToCursor(false); }));
                    menu.Add(new ContextMenuOption("MenuDuplicate", DuplicateHereLabel, () => { CopySelectionToCursor(true); }));
                }

                if (IsSelectionValid())
                {
                    menu.Add(new ContextMenuOption("MenuClearSelection", ClearSelectionLabel, () => { ClearSelection(); ClearHighlightedPatern(); }, ContextMenuSeparator.Before));
                }

                if (pattern != null)
                {
                    if (IsPatternSelected(location) && SelectionContainsMultiplePatterns())
                    {
                        menu.Insert(0, new ContextMenuOption("MenuDeleteSelection", DeleteSelectionLabel, () => { DeleteSelection(true); }));
                        menu.Add(new ContextMenuOption("MenuProperties", SelectedPatternPropertiesLabel, () => { EditPatternProperties(new Point(x, y), pattern, location, true); }, ContextMenuSeparator.Before));
                    }
                    else
                    {
                        menu.Add(new ContextMenuOption("MenuProperties", PatternPropertiesLabel, () => { EditPatternProperties(new Point(x, y), pattern, location, false); }, ContextMenuSeparator.Before));
                    }

                    menu.Insert(0, new ContextMenuOption("MenuDelete", DeletePatternLabel, () => { DeletePattern(location); }));
                }

                if (IsPatternSelected(location))
                {
                    if (UpdateSelectedPatternRefCounts())
                        menu.Insert(1, new ContextMenuOption("MenuUnlink", MakePatternsUniqueLabel, () => { MakeSelectedPatternsUnique(); }));
                }

                if (menu.Count > 0)
                    App.ShowContextMenuAsync(menu.ToArray());
                
                return true;
            }

            return false;
        }

        private bool HandleTouchLongPressPatternArea(int x, int y)
        {
            return HandleContextMenuPatternArea(x, y);
        }

        protected void OnTouchDown(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            SetFlingVelocity(0, 0);
            SetMouseLastPos(x, y);

            if (HandleTouchDownPatternArea(x, y)) goto Handled;
            if (HandleTouchDownPan(x, y)) goto Handled;
            return;

        Handled:
            MarkDirty();
        }

        protected void OnTouchMove(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            UpdateCaptureOperation(x, y);
            SetMouseLastPos(x, y);
        }

        protected void OnTouchUp(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            EndCaptureOperation(x, y);
            SetMouseLastPos(x, y);
        }

        protected override void OnTouchFling(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            if (canFling)
            {
                EndCaptureOperation(x, y);
                SetFlingVelocity(e.FlingVelocityX, e.FlingVelocityY);
            }
        }

        protected override void OnTouchScaleBegin(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            if (captureOperation != CaptureOperation.None)
            {
                Debug.Assert(captureOperation != CaptureOperation.MobileZoom);
                AbortCaptureOperation();
            }

            StartCaptureOperation(x, y, CaptureOperation.MobileZoom);
            SetMouseLastPos(x, y);
        }

        protected override void OnTouchScale(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            UpdateCaptureOperation(x, y, e.TouchScale);
            SetMouseLastPos(x, y);
        }

        protected override void OnTouchScaleEnd(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            EndCaptureOperation(x, y);
            SetMouseLastPos(x, y);
        }

        protected override void OnTouchClick(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            if (HandleTouchClickPatternArea(x, y)) goto Handled;

            return;

        Handled:
            MarkDirty();
        }

        protected override void OnTouchDoubleClick(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            SetMouseLastPos(x, y);

            // Ignore double tap if we handled a single tap recently.
            if (captureOperation != CaptureOperation.None/* || (DateTime.Now - lastPatternCreateTime).TotalMilliseconds < 500*/)
            {
                return;
            }

            if (HandleTouchDoubleClickPatternArea(x, y)) goto Handled;

            return;

        Handled:
            MarkDirty();
        }

        protected override void OnTouchLongPress(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            if (e.IsDoubleTapLongPress)
            {
                return;
            }

            // Header:
            // - Context menu : seet loop point, custom settings
            // Pattern names:
            // - Context menu : Mute/Unmute, Toggle force display, etc. (click on icon???)
            // Pattern area:
            // - Context menu : Pattern properties, etc. (if in selection)

            AbortCaptureOperation();

            if (HandleTouchLongPressPatternArea(x, y)) goto Handled;

            return;

        Handled:
            MarkDirty();
        }

        private Pattern[,] GetSelectedPatterns(out Song.PatternCustomSetting[] customSettings)
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
            if (captureOperation == CaptureOperation.DragSelection)
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

        private Pattern CreateUniquePatternClone(Pattern pattern, Channel channel)
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

        private void EndDragSelection(int x, int y)
        {
            if (captureThresholdMet)
            {
                if (!IsSelectionValid()) // No clue how we end up here with invalid selection.
                {
                    CancelDragSelection();
                }
                else
                {
                    var noteIdx = GetNoteForPixel(x - channelNameSizeX);

                    if (noteIdx >= 0 && noteIdx < Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                    {
                        var patternIdx = Song.PatternIndexFromAbsoluteNoteIndex(GetNoteForPixel(x - channelNameSizeX));
                        var patternIdxDelta = patternIdx - selectionDragAnchorPatternIdx;

                        var dragRowIdxStart   = captureRowIdx;
                        var dragRowIdxCurrent = GetRowIndexForCoord(y);
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

        private void EndCaptureOperation(int x, int y)
        {
            if (captureOperation != CaptureOperation.None)
            {
                switch (captureOperation)
                {
                    case CaptureOperation.DragSelection:
                        EndDragSelection(x, y);
                        break;
                    case CaptureOperation.DragSeekBar:
                        UpdateSeekDrag(x, y, true);
                        break;
                    case CaptureOperation.MobilePan:
                    case CaptureOperation.MobileZoom:
                        canFling = true;
                        break;

                }

                panning = false;
                captureOperation = CaptureOperation.None;
                ReleasePointer();
                MarkDirty();
            }
        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                OnTouchUp(e);
                return;
            }

            bool middle = e.Middle;
            bool doMouseUp = false;

            if (middle)
            {
                panning = false;
            }
            else
            {
                doMouseUp = captureOperation == CaptureOperation.None;
                EndCaptureOperation(e.X, e.Y);
            }

            UpdateCursor();

            if (doMouseUp)
            {
                if (HandleMouseUpPatternArea(e)) goto Handled;
                return;
                Handled:
                    MarkDirty();
            }
            else if (e.Right && IsSelectionValid())
            {
                UpdateSelectedPatternRefCounts();
            }
        }

        private void AbortCaptureOperation()
        {
            if (captureOperation != CaptureOperation.None)
            {
                if (App.UndoRedoManager.HasTransactionInProgress)
                    App.UndoRedoManager.AbortTransaction();

                panning = false;
                canFling = false;
                captureOperation = CaptureOperation.None;

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
            if (captureOperation == CaptureOperation.DragSelection)
            {
                selectionDragAnchorPatternIdx = -1;
                selectionDragAnchorPatternXFraction = -1.0f;
                captureOperation = CaptureOperation.None;
            }

            captureMouseX = -1;
            captureMouseY = -1;
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
                        patternCache.Remove(pattern);
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

            if (captureOperation == CaptureOperation.DragSelection)
            {
                UpdateCursor();
                MarkDirty();
            }
            
/*             if (IsActiveControl)
                UpdateToolTip(null); */
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (captureOperation == CaptureOperation.DragSelection)
            {
                UpdateCursor();
                MarkDirty();
            }

/*             if (IsActiveControl)
                UpdateToolTip(null); */
        }

        private void UpdateSeekDrag(int mouseX, int mouseY, bool final)
        {
            ScrollIfNearEdge(mouseX, mouseY);

            dragSeekPosition = Utils.Clamp(GetNoteForPixel(mouseX - channelNameSizeX), 0, Song.GetPatternStartAbsoluteNoteIndex(Song.Length) - 1);

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
            var deltaY = y - captureMouseY;

            if (Math.Abs(deltaY) > 50)
            {
                ZoomAtLocation(x, deltaY < 0.0f ? 2.0f : 0.5f);
                captureMouseY = y;
            }
        }

        private bool IsMouseInPatternZone(PointerEventArgs e)
        {
            return e.Y > headerSizeY && e.X > channelNameSizeX;
        }

        private void UpdateToolTip(PointerEventArgs e)
        {
            if (e == null)
            {
                var pt = ScreenToControl(CursorPosition);
                e = new PointerEventArgs(0, pt.X, pt.Y);
            }

            string tooltip = "";

            bool inPatternZone = GetPatternForCoord(e.X, e.Y, out var location);

            if (inPatternZone)
            {
                var pattern = Song.GetPatternInstance(location);

                var tooltipList = new List<string>();

                if (pattern == null)
                    tooltipList.Add($"<MouseLeft> {AddPatternTooltip}");

                if (Settings.SetLoopPointShortcut.IsShortcutValid(0))
                    tooltipList.Add($"{Settings.SetLoopPointShortcut.TooltipString}<MouseLeft> {SetLoopPointTooltip}");

                tooltipList.Add($"<MouseWheel><Drag> {PanTooltip}");
                tooltipList.Add($"<MouseRight><Drag> {SelectRectangleTooltip}");

                if (pattern != null)
                {
                    tooltipList.Add($"<MouseLeft><MouseLeft> {OrTooltip} <Shift><MouseLeft> {DeletePatternTooltip}");
                    tooltipList.Add($"<MouseRight> {MoreOptionsTooltip}");
                }

                if (IsPatternSelected(location))
                {
                    tooltipList.Add($"<Drag> {MovePatternTooltip}");
                    tooltipList.Add($"<Ctrl><Drag> {ClonePatternTooltip}");
                }

                if (tooltipList.Count >= 3)
                {
                    var array = tooltipList.ToArray();
                    var numFirstLine = array.Length / 2;
                    tooltip = string.Join(" - ", array, 0, numFirstLine) + "\n" + string.Join(" - ", array, numFirstLine, array.Length - numFirstLine);
                }
                else
                {
                    tooltip = string.Join(" - ", tooltipList);
                }
            }

            App.SetToolTip(tooltip);
        }

        private void UpdateScrollBarX(int x, int y)
        {
            GetScrollBarParams(true, out _, out var scrollBarSizeX, out _);
            GetMinMaxScroll(out _, out var maxScrollX, out _, out _);
            int scrollAreaSizeX = width - channelNameSizeX;
            scrollX = (int)Math.Round(captureScrollX + ((x - captureMouseX) / (float)(scrollAreaSizeX - scrollBarSizeX) * maxScrollX));
            ClampScroll();
            MarkDirty();
        }

        private void UpdateScrollBarY(int x, int y)
        {
            GetScrollBarParams(false, out _, out var scrollBarSizeY, out _);
            GetMinMaxScroll(out _, out _, out _, out var maxScrollY);
            int scrollAreaSizeY = height - headerSizeY - scrollBarThickness;
            scrollY = (int)Math.Round(captureScrollY + ((y - captureMouseY) / (float)(scrollAreaSizeY - scrollBarSizeY) * maxScrollY));
            ClampScroll();
            MarkDirty();
        }

        private bool CanScrollVertically()
        {
            GetMinMaxScroll(out _, out _, out var minScrollY, out var maxScrollY);
            return minScrollY != maxScrollY;
        }

        private void UpdateCaptureOperation(int x, int y, float scale = 1.0f, bool realTime = false)
        {
            const int CaptureThreshold = Platform.IsDesktop ? 5 : 50;

            var captureThresholdJustMet = false;

            if (captureOperation != CaptureOperation.None)
            {
                if (!captureThresholdMet)
                {
                    if (Math.Abs(x - captureMouseX) >= CaptureThreshold ||
                        Math.Abs(y - captureMouseY) >= CaptureThreshold)
                    {
                        captureThresholdMet = true;
                        captureThresholdJustMet = true;
                    }
                }

                mouseMovedDuringCapture |= (x != captureMouseX || y != captureMouseY);
            }

            if (captureOperation != CaptureOperation.None && captureThresholdMet && (captureRealTimeUpdate || !realTime))
            {
                switch (captureOperation)
                {
                    case CaptureOperation.SelectColumn:
                        UpdateSelection(x, y, true, captureThresholdJustMet);
                        break;
                    case CaptureOperation.SelectRectangle:
                        UpdateSelection(x, y, false, captureThresholdJustMet);
                        break;
                    case CaptureOperation.AltZoom:
                        UpdateAltZoom(x, y);
                        break;
                    case CaptureOperation.DragSeekBar:
                        UpdateSeekDrag(x, y, false);
                        break;
                    case CaptureOperation.ScrollBarX:
                        UpdateScrollBarX(x, y);
                        break;
                    case CaptureOperation.ScrollBarY:
                        UpdateScrollBarY(x, y);
                        break;
                    case CaptureOperation.MobilePan:
                        DoScroll(x - mouseLastX, y - mouseLastY);
                        break;
                    case CaptureOperation.DragSelection:
                        UpdateDragSelection(x, y);
                        break;
                    case CaptureOperation.MobileZoom:
                        ZoomAtLocation(x, scale);
                        DoScroll(x - mouseLastX, y - mouseLastY);
                        break;
                    default:
                        MarkDirty();
                        break;
                }
            }
        }

        protected override void OnPointerMove(PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                OnTouchMove(e);
                return;
            }

            bool middle = e.Middle || (e.Left && ModifierKeys.IsAltDown && Settings.AltLeftForMiddle);

            UpdateCursor();
            UpdateCaptureOperation(e.X, e.Y);

            // TODO: There is a niche issue here that I've noticed on Wayland. If the pointer
            // pointer leaves the window during a pan, then the middle button is released,
            // the pointer events are delayed and fire after the cursor enters the window.
            // On re-entry, this fires again before pointer up, leading the panning to 
            // snap the distance of e.X - mouseLastX before middle and panning are false.
            // There is surely a way to fire these events while the cursor is outside?
            if (middle && panning)
                DoScroll(e.X - mouseLastX, e.Y - mouseLastY);

            if (IsCursorInContainer())
                UpdateHover(e);

            // TODO: This will change when the pattern area becaomes its own control.
            // For now, this stops it breaking the tooltips during a capture op.
            if (mouseLastX > channelNameSizeX && mouseLastY > headerSizeY)
                UpdateToolTip(e);

            SetMouseLastPos(e.X, e.Y);
            ShowExpansionIcons = false;
        }

        private void UpdateHover(PointerEventArgs e)
        {
            if (Platform.IsDesktop)
            {
                var row     = GetRowIndexForCoord(e.Y);
                var pattern = GetPatternIndexForCoord(e.X);

                SetHover(row, pattern);
            }
        }

        public void SetHover(int row, int pattern)
        {
            if (Platform.IsDesktop)
            {
                if (hoverRow != row)
                {
                    SetAndMarkDirty(ref hoverRow, row);
                    channelNames.SetHover(row >= 0 ? GetRowToChannelForIndex(row) : -1);
                }

                if (hoverPattern != pattern)
                {
                    SetAndMarkDirty(ref hoverPattern, pattern);
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
                SetHover(-1, -1);
            }
        }

        private void ConditionalClearHover()
        {
            if (!IsCursorInContainer())
            {
                ClearHover();
            }
        }

        public override void OnContainerPointerLeaveNotify(Control c, EventArgs e)
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

        protected bool HandleMouseDoubleClickPatternArea(PointerEventArgs e)
        {
            if (e.Left && IsMouseInPatternZone(e) && GetPatternForCoord(e.X, e.Y, out var location))
            {
                var pattern = Song.GetPatternInstance(location);
                if (pattern != null)
                    DeletePattern(location);
                return true;
            }

            return false;
        }

        protected override void OnMouseDoubleClick(PointerEventArgs e)
        {
            if (HandleMouseDoubleClickPatternArea(e)) goto Handled;

            return;

        Handled:
            MarkDirty();
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

        protected override void OnMouseWheel(PointerEventArgs e)
        {
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
                ZoomAtLocation(e.X, e.ScrollY < 0.0f ? 0.5f : 2.0f);
            }
        }

        protected override void OnMouseHorizontalWheel(PointerEventArgs e)
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
                captureOperation == CaptureOperation.None && !window.IsAsyncDialogInProgress && !window.IsOutOfProcessDialogInProgress)
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

                if ((deltaPixelX != 0 || deltaPixelY != 0) && DoScroll(deltaPixelX, deltaPixelY))
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

            UpdateCaptureOperation(mouseLastX, mouseLastY, 1.0f, true);
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
            if (patternCache != null)
                patternCache.Clear();
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
                UpdateSelectedPatternRefCounts();
                InvalidatePatternCache();
                UpdateRenderCoords();
                CancelDragSelection();
                MarkDirty();
            }
        }
    }
}
