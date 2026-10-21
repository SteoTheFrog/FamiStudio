using System;
using System.Collections.Generic;
using System.IO;

namespace FamiStudio
{
    public class PatternArea : Control
    {
        private const int DefaultPatternNamePosX = 2;

        private int channelSizeY;
        private int patternHeaderSizeY;
        private int patternNamePosX;
        private int rowToChannelCount;
        private int scrollY;
        private int virtualSizeY;

        private PatternBitmapCache patternCache;
        private Dictionary<Pattern, int> patternRefCounts = [];

        private Sequencer Sequencer => App?.Sequencer;
        private Song Song => App?.SelectedSong;

        private bool IsDragSelectionInProgress => Sequencer.IsDragSelectionInProgress;
        private bool IsValidTimeOnlySelection => Sequencer.HasValidTimeOnlySelection;
        private int SeekX => Sequencer.SeekX;
        public int SelectionDragAnchorPatternIdx => Sequencer.SelectionDragAnchorPatternIdx;
        public float SelectionDragAnchorPatternXFraction => Sequencer.SelectionDragAnchorPatternXFraction;

        private Color SelectedPatternInvisibleColor => Sequencer.SelectedPatternInvisibleColor;
        private Color SelectedPatternVisibleColor => Sequencer.SelectedPatternVisibleColor;
        private PatternLocation HighlightLocation => Sequencer.HighlightLocation;
        private PatternLocation SelectionMax => Sequencer.SelectionMax;
        private PatternLocation SelectionMin => Sequencer.SelectionMin;
        private bool HasCaptureOperation => Sequencer.HasCaptureOperation;
        private int CaptureRowIdx => Sequencer.CaptureRowIdx;
        private float BitmapScale => Sequencer.BitmapScale;
        private float ChannelBitmapScale => Sequencer.ChannelBitmapScale;

        public delegate void EditPatternDelegate(Point p, Pattern pat, PatternLocation l, bool sel);
        public delegate void EmptyDelegate();
        public delegate void MoveCopyDelegate(PatternLocation l, bool copy);
        public delegate void LocationDelegate(PatternLocation l);
        public delegate void PatternClickedDelegate(int channelIdx, int patternIdx, bool setActive);
        public delegate void PointerDelegate(Control c, PointerEventArgs e);
        public delegate void SelectionDelegate(Control c, PointerEventArgs e, int idx);
        public delegate void TouchDelegate(PointerEventArgs e);

        public event EditPatternDelegate EditPatternProperties;
        public event EmptyDelegate ClearHighlightedPattern;
        public event EmptyDelegate ClearSelection;
        public event EmptyDelegate DeleteSelection;
        public event EmptyDelegate PatternModified;
        public event EmptyDelegate SelectionChanged;
        public event MoveCopyDelegate CopySelectionToCursor;
        public event LocationDelegate EnsureSelectionInclude;
        public event LocationDelegate SetHighlightedPattern;
        public event LocationDelegate SetSelection;
        public event PatternClickedDelegate PatternClicked;
        public event PointerDelegate StartAltZoom;
        public event SelectionDelegate StartDragSelection;
        public event SelectionDelegate StartRectangleSelection;

        TextureAtlasRef bmpInstantiate;
        TextureAtlasRef bmpDuplicate;
        TextureAtlasRef bmpDuplicateMove;
        TextureAtlasRef bmpMenuInstance;

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

        // Context menu
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

        public PatternArea()
        {
            Localization.Localize(this);
            supportsLongPress = true;
            supportsDoubleClick = true;
        }

        protected override void OnAddedToContainer()
        {
            var g = ParentWindow.Graphics;
            patternCache = new PatternBitmapCache(g);
            bmpInstantiate = g.GetTextureAtlasRef("Instance");
            bmpDuplicate = g.GetTextureAtlasRef("Duplicate");
            bmpDuplicateMove = g.GetTextureAtlasRef("DuplicateMove");
            bmpMenuInstance = g.GetTextureAtlasRef("MenuInstance");
        }

        public void UpdateLayout(int channelSizeY, int patternHeaderSizeY, int rowToChannelCount)
        {
            this.channelSizeY = channelSizeY;
            this.patternHeaderSizeY = patternHeaderSizeY;
            this.rowToChannelCount = rowToChannelCount;

            virtualSizeY    = rowToChannelCount * channelSizeY;
            patternNamePosX = DpiScaling.ScaleForWindow(DefaultPatternNamePosX);
        }

        public void InvalidatePattern(Pattern pattern)
        {
            if (pattern != null)
                patternCache?.Remove(pattern);
            MarkDirty();
        }

        public void InvalidateCache()
        {
            patternCache?.Clear();
            MarkDirty();
        }

        private void GotoPianoRoll(PatternLocation location)
        {
            PatternClicked?.Invoke(location.ChannelIndex, location.PatternIndex, true);
        }

        private Point MouseLastPosition()
        {
            var p = Sequencer.MouseLastPosition;
            return new Point(p.X - Left, p.Y - Top);
        }

        private Color GetSeekBarColor()
        {
            return Sequencer.GetSeekBarColor();
        }

        private bool IsSelectionValid()
        {
            return Sequencer.IsSelectionValid();
        }

        private bool IsPatternSelected(PatternLocation location)
        {
            return location.ChannelIndex >= SelectionMin.ChannelIndex && location.ChannelIndex <= SelectionMax.ChannelIndex &&
                   location.PatternIndex >= SelectionMin.PatternIndex && location.PatternIndex <= SelectionMax.PatternIndex;
        }

        private int GetPixelForNote(int note, bool scroll = true)
        {
            return Sequencer.GetPixelForNote(note, scroll);
        }

        private int GetNoteForPixel(int x, bool scroll = true)
        {
            return Sequencer.GetNoteForPixel(x, scroll);
        }

        private int MinVisiblePattern()
        {
            return Sequencer.MinVisiblePattern();
        }

        private int MaxVisiblePattern()
        {
            return Sequencer.MaxVisiblePattern();
        }

        private int GetChannelToRowForIndex(int idx)
        {
            return Sequencer.GetChannelToRowForIndex(idx);
        }

        private int GetRowToChannelForIndex(int idx)
        {
            return Sequencer.GetRowToChannelForIndex(idx);
        }

        private bool GetChannelVisibleForIndex(int idx)
        {
            return Sequencer.GetChannelVisibleForIndex(idx);
        }

        private int GetRowIndexForCoord(int y)
        {
            return Utils.Clamp((y + scrollY) / channelSizeY, 0, rowToChannelCount - 1);
        }

        private int GetChannelIndexForCoord(int y)
        {
            return rowToChannelCount > 0 ? GetRowToChannelForIndex(GetRowIndexForCoord(y)) : -1;
        }

        private Pattern CreateUniquePatternClone(Pattern pattern, Channel channel)
        {
            return Sequencer.CreateUniquePatternClone(pattern, channel);
        }

        private Pattern[,] GetSelectedPatterns(out Song.PatternCustomSetting[] customSettings)
        {
            return Sequencer.GetSelectedPatterns(out customSettings);
        }
        
        public void ClearSelectedPatternRefCounts()
        {
            patternRefCounts?.Clear();
        }

#if DEBUG
        public void ValidateIntegrity()
        {
            patternCache.ValidateIntegrity();
        }
#endif

        private bool IsPatternColumnSelected(int patternIdx)
        {
            return IsValidTimeOnlySelection && patternIdx >= SelectionMin.PatternIndex && patternIdx <= SelectionMax.PatternIndex;
        }

        private bool SelectionContainsMultiplePatterns()
        {
            return IsSelectionValid() && (SelectionMax.ChannelIndex - SelectionMin.ChannelIndex + 1) * (SelectionMax.PatternIndex - SelectionMin.PatternIndex + 1) > 1;
        }

        private void DeletePattern(PatternLocation location)
        {
            var channel = Song.Channels[location.ChannelIndex];
            var pattern = channel.PatternInstances[location.PatternIndex];

            App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, location.ChannelIndex);
            InvalidatePattern(pattern);
            channel.PatternInstances[location.PatternIndex] = null;
            channel.InvalidateCumulativePatternCache();
            PatternModified?.Invoke();
            App.UndoRedoManager.EndTransaction();

            ClearSelection();
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

            ClearSelection?.Invoke();
            MarkDirty();
        }

        public bool UpdateSelectedPatternRefCounts()
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

            for (int i = SelectionMin.ChannelIndex; i <= SelectionMax.ChannelIndex; i++)
            {
                var channel = Song.Channels[i];
                for (int j = SelectionMin.PatternIndex; j <= SelectionMax.PatternIndex; j++)
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

        private bool GetMinMaxSelectedRow(out int minSelRow, out int maxSelRow)
        {
            var minSelChannel = SelectionMin.ChannelIndex;
            var maxSelChannel = SelectionMax.ChannelIndex;
            
            while (minSelChannel < Song.Channels.Length && GetChannelToRowForIndex(minSelChannel) < 0) minSelChannel++;
            while (maxSelChannel >= 0 && GetChannelToRowForIndex(maxSelChannel) < 0) maxSelChannel--;

            if (minSelChannel < Song.Channels.Length && maxSelChannel >= 0)
            {
                minSelRow = GetChannelToRowForIndex(minSelChannel);
                maxSelRow = GetChannelToRowForIndex(maxSelChannel);
                return true;
            }
            else
            {
                minSelRow = -1;
                maxSelRow = -1;
                return false;
            }
        }

        private bool GetPatternForCoord(int x, int y, out PatternLocation location)
        {
            var noteIdx = GetNoteForPixel(x);

            if (noteIdx < 0 || noteIdx >= Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
            {
                location = PatternLocation.Invalid;
                return false;
            }

            location = new PatternLocation(GetChannelIndexForCoord(y), Song.PatternIndexFromAbsoluteNoteIndex(noteIdx));

            return location.IsChannelInSong(Song);
        }

        private void ShowContextMenu(Point p)
        {
            var x = p.X;
            var y = p.Y;

            GetPatternForCoord(x, y, out var location);
            var pattern = Song.GetPatternInstance(location);

            SetHighlightedPattern?.Invoke(location);

            var menu = new List<ContextMenuOption>();
            var sel  = new List<ContextMenuOption>();

            if (Platform.IsMobile)
            {
                menu.Add(new ContextMenuOption("MenuPiano", GoToPianoRollLabel, () => { GotoPianoRoll(location); }));
            }
            else if (pattern != null &&!IsPatternSelected(location))
            {
                SetSelection?.Invoke(location);
            }

            if (IsSelectionValid() && !IsPatternSelected(location))
            {
                if (Platform.IsMobile)
                    menu.Add(new ContextMenuOption("MenuExpandSelection", ExpandSelectionLabel, () => { EnsureSelectionInclude?.Invoke(location); }));
                if (SelectionMin.ChannelIndex == location.ChannelIndex)
                    menu.Add(new ContextMenuOption("MenuInstance", InstanciateHereLabel, () => { CopySelectionToCursor?.Invoke(location, false); }));
                menu.Add(new ContextMenuOption("MenuDuplicate", DuplicateHereLabel, () => { CopySelectionToCursor?.Invoke(location, true); }));
            }

            if (IsSelectionValid())
            {
                menu.Add(new ContextMenuOption("MenuClearSelection", ClearSelectionLabel, () => { ClearSelection?.Invoke(); ClearHighlightedPattern?.Invoke(); }, ContextMenuSeparator.Before));
            }

            if (pattern != null)
            {
                if (IsPatternSelected(location) && SelectionContainsMultiplePatterns())
                {
                    menu.Insert(0, new ContextMenuOption("MenuDeleteSelection", DeleteSelectionLabel, () => { DeleteSelection?.Invoke(); }));
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
        }

        private void UpdateTooltip(PointerEventArgs e)
        {
            if (GetPatternForCoord(e.X, e.Y, out var location))
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
            else
            {
                tooltip = "";
            }
        }
        
        protected void OnTouchDown(PointerEventArgs e)
        {
            if (GetPatternForCoord(e.X, e.Y, out var location))
            {
                if (HighlightLocation == location)
                {
                    if (!IsPatternSelected(HighlightLocation))
                        SetSelection?.Invoke(HighlightLocation);

                    StartDragSelection?.Invoke(this, e, HighlightLocation.PatternIndex);
                    e.MarkHandled();
                }

                //StartMobilePanning?.Invoke(this, e);
            }
        }

        protected override void OnTouchClick(PointerEventArgs e)
        {
            bool inPatternZone = GetPatternForCoord(e.X, e.Y, out var location);

            if (inPatternZone)
            {
                var pattern = Song.GetPatternInstance(location);

                if (pattern == null)
                {
                    CreateNewPattern(location);
                    SetHighlightedPattern?.Invoke(location);
                }
                else 
                {
                    if (HighlightLocation == location)
                        ClearHighlightedPattern?.Invoke();
                    else
                        SetHighlightedPattern?.Invoke(location);

                    // Tapping inside the selection toggles highlight, but doesnt clear selection.
                    if (IsPatternSelected(location))
                        return;
                }

                // CreateNewPattern clears the selection. Ugh, call after.
                SetSelection?.Invoke(location);
            }
        }

        protected override void OnTouchDoubleClick(PointerEventArgs e)
        {
            OnMouseDoubleClick(e);
        }

        protected override void OnTouchLongPress(PointerEventArgs e)
        {
            ShowContextMenu(e.Position);
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                OnTouchDown(e);
                return;
            }

/*             if (e.Middle)
            {
                StartPanning?.Invoke(this, e);
                return;
            } */

            if (e.Left)
            {
                if (GetPatternForCoord(e.X, e.Y, out var location))
                {
                    var pattern = Song.GetPatternInstance(location);
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
                                return;
                            }

                            PatternClicked?.Invoke(location.ChannelIndex, location.PatternIndex, false);
                        }

                        if (!IsPatternSelected(location))
                        {
                            SetSelection?.Invoke(location);
                            SelectionChanged?.Invoke();
                        }

                        StartDragSelection?.Invoke(this, e, location.PatternIndex);
                    }
                }
            }
            else if (e.Right)
            {
                if (ModifierKeys.IsAltDown && Settings.AltZoomAllowed && GetPatternForCoord(e.X, e.Y, out _))
                {   
                    StartAltZoom?.Invoke(this, e);
                    return;
                }

                e.DelayRightClick(); // Need to wait to see difference between context menu or selection.
            }
        }

        protected override void OnPointerDownDelayed(PointerEventArgs e)
        {
            if (e.Right)
            {
                if (GetPatternForCoord(e.X, e.Y, out var location))
                    StartRectangleSelection?.Invoke(this, e, location.PatternIndex);
            }
        }

        protected override void OnMouseDoubleClick(PointerEventArgs e)
        {
            var inPatternZone = GetPatternForCoord(e.X, e.Y, out var location);
            if (inPatternZone)
            {
                var pattern = Song.GetPatternInstance(location);
                if (pattern != null)
                {
                    DeletePattern(location);
                    ClearHighlightedPattern?.Invoke();
                }
            }
        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                //OnTouchUp(e);
                return;
            }

            // If we have a capture operation, we likely had a delayed right click.
            if (e.Right && !HasCaptureOperation)
            {
                ShowContextMenu(e.Position);
            }
        }

        protected override void OnPointerMove(PointerEventArgs e)
        {
            UpdateTooltip(e);
        }

        public void SetScrollY(int s)
        {
            if (scrollY != s)
            {
                scrollY = s;
                MarkDirty();
            }
        }

        protected override void OnRender(Graphics g)
        {
            var c = g.DefaultCommandList;
            var b = g.BackgroundCommandList;
            var f = g.ForegroundCommandList;

            var isSelectionValid = IsSelectionValid();

            c.PushClipRegion(0, 0, width, height);

            // Bottom line
            //c.DrawLine(0, height - 1, width, height - 1, Theme.BlackColor);

            // Background
            c.DrawLine(0, 0, width, 0, Theme.BlackColor);

            for (int i = MinVisiblePattern(); i < MaxVisiblePattern(); i++)
            {
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                var sx = GetPixelForNote(Song.GetPatternLength(i), false);
                var color = (i & 1) == 0 ? Theme.DarkGreyColor4 : Theme.DarkGreyColor2;
                b.FillRectangle(px, 0, px + sx, height, color);
            }

            // Selection
            if (isSelectionValid)
            {
                if (GetMinMaxSelectedRow(out var minSelRow, out var maxSelRow))
                {
                    c.FillRectangle(
                        GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Math.Min(SelectionMin.PatternIndex + 0, Song.Length))), channelSizeY * (minSelRow + 0) - scrollY,
                        GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Math.Min(SelectionMax.PatternIndex + 1, Song.Length))), channelSizeY * (maxSelRow + 1) - scrollY,
                        Sequencer.IsActiveControl ? SelectedPatternVisibleColor : SelectedPatternInvisibleColor);
                }
            }

            // Vertical lines
            for (int i = Math.Max(1, MinVisiblePattern()); i <= MaxVisiblePattern(); i++)
            {
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                c.DrawLine(px, 0, px, height, Theme.BlackColor);
            }

            c.PushTranslation(0, -scrollY);

            // Horizontal lines
            for (int i = 0, y = 0; i <= rowToChannelCount; i++, y += channelSizeY)
                c.DrawLine(0, y, width, y, Theme.BlackColor);

            // TODO : This is really bad, since all the logic is in the rendering code. Make
            // this more like any other capture op eventually.
            if (IsDragSelectionInProgress)
            {
                var pt = MouseLastPosition();
                var noteIdx = GetNoteForPixel(pt.X);

                if (noteIdx >= 0 && noteIdx < Song.GetPatternStartAbsoluteNoteIndex(Song.Length) && GetMinMaxSelectedRow(out var minSelRow, out var maxSelRow))
                {
                    var patternIdx = Song.PatternIndexFromAbsoluteNoteIndex(noteIdx);
                    var patternIdxDelta = patternIdx - SelectionDragAnchorPatternIdx;

                    var dragRowIdxStart = CaptureRowIdx;
                    var dragRowIdxCurrent = GetRowIndexForCoord(pt.Y);
                    var rowIdxDelta = dragRowIdxCurrent - dragRowIdxStart;

                    for (int j = minSelRow + rowIdxDelta; j <= maxSelRow + rowIdxDelta; j++)
                    {
                        if (j < 0 || j >= rowToChannelCount)
                            continue;

                        var y = j * channelSizeY;

                        // Center.
                        var patternSizeX = GetPixelForNote(Song.GetPatternLength(patternIdx), false);
                        var anchorOffsetLeftX = (int)(patternSizeX * SelectionDragAnchorPatternXFraction);
                        var anchorOffsetRightX = (int)(patternSizeX * (1.0f - SelectionDragAnchorPatternXFraction));

                        var instance = ModifierKeys.IsControlDown;
                        var duplicate = instance && ModifierKeys.IsShiftDown;

                        var bmpCopy = (TextureAtlasRef)null;
                        var bmpSize = DpiScaling.ScaleCustom(bmpDuplicate.ElementSize.Width, BitmapScale);

                        if (rowIdxDelta != 0)
                            bmpCopy = (duplicate || instance) ? bmpDuplicate : bmpDuplicateMove;
                        else
                            bmpCopy = duplicate ? bmpDuplicate : (instance ? bmpInstantiate : null);

                        c.PushTranslation(pt.X, y);
                        c.FillAndDrawRectangle(-anchorOffsetLeftX, 0, -anchorOffsetLeftX + patternSizeX, channelSizeY, SelectedPatternVisibleColor, Theme.BlackColor);

                        if (bmpCopy != null)
                            c.DrawTextureAtlas(bmpCopy, -anchorOffsetLeftX + patternSizeX / 2 - bmpSize / 2, channelSizeY / 2 - bmpSize / 2, BitmapScale, Theme.LightGreyColor1);

                        // Left side
                        for (int p = patternIdx - 1; p >= SelectionMin.PatternIndex + patternIdxDelta && p >= 0; p--)
                        {
                            patternSizeX = GetPixelForNote(Song.GetPatternLength(p), false);
                            anchorOffsetLeftX += patternSizeX;

                            c.FillAndDrawRectangle(-anchorOffsetLeftX, 0, -anchorOffsetLeftX + patternSizeX, channelSizeY, SelectedPatternVisibleColor, Theme.BlackColor);

                            if (bmpCopy != null)
                                c.DrawTextureAtlas(bmpCopy, -anchorOffsetLeftX + patternSizeX / 2 - bmpSize / 2, channelSizeY / 2 - bmpSize / 2, BitmapScale, Theme.LightGreyColor1);
                        }

                        // Right side
                        for (int p = patternIdx + 1; p <= SelectionMax.PatternIndex + patternIdxDelta && p < Song.Length; p++)
                        {
                            patternSizeX = GetPixelForNote(Song.GetPatternLength(p), false);

                            c.FillAndDrawRectangle(anchorOffsetRightX, 0, anchorOffsetRightX + patternSizeX, channelSizeY, SelectedPatternVisibleColor, Theme.BlackColor);

                            if (bmpCopy != null)
                                c.DrawTextureAtlas(bmpCopy, anchorOffsetRightX + patternSizeX / 2 - bmpSize / 2, channelSizeY / 2 - bmpSize / 2, BitmapScale, Theme.LightGreyColor1);

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
                    if (GetChannelVisibleForIndex(ci))
                    {
                        var location = new PatternLocation(ci, pi);
                        var pattern = Song.GetPatternInstance(location);

                        if (pattern != null)
                        {
                            var bmp = patternCache.GetOrAddPattern(pattern, patternLen, noteLen, out var u0, out var v0, out var u1, out var v1);
                            var isSelected = IsPatternSelected(location);
                            var font = isSelected ? Fonts.FontSmallBold : Fonts.FontSmall;

                            c.PushTranslation(0, py);
                            c.FillRectangleGradient(1, 1, sx, patternHeaderSizeY, pattern.Color, pattern.Color.Scaled(0.8f), true, patternHeaderSizeY);
                            c.FillRectangle(1, patternHeaderSizeY, sx, channelSizeY, Color.FromArgb(75, pattern.Color));
                            c.DrawLine(0, patternHeaderSizeY, sx, patternHeaderSizeY, Theme.BlackColor);
                            c.DrawTexture(bmp, 1.0f, 1.0f + patternHeaderSizeY, sx - 1, patternCacheSizeY, u0, v0, u1, v1);

                            if (isSelected)
                            {
                                c.DrawRectangle(0, 0, sx, channelSizeY, Theme.WhiteColor, 3, true, true);
                                c.DrawLine(0, patternHeaderSizeY, sx, patternHeaderSizeY, Theme.WhiteColor, 3, true, true);
                                c.DrawText(pattern.Name, font, patternNamePosX + 1, 1, Theme.BlackColor, TextFlags.Left | TextFlags.Middle | TextFlags.Clip, sx - patternNamePosX, patternHeaderSizeY + 1);
                            }

                            c.DrawText(pattern.Name, font, patternNamePosX, 0, isSelected ? Theme.WhiteColor : Theme.BlackColor, TextFlags.Left | TextFlags.Middle | TextFlags.Clip, sx - patternNamePosX, patternHeaderSizeY + 1);

                            c.PopTransform();

                            if (isSelectionValid && patternRefCounts.TryGetValue(pattern, out var count) && count > 1)
                            {
                                // TODO: Get the correct icon instead of resizing. This is a placeholder.
                                var scale = BitmapScale * (Platform.IsDesktop ? 1.0f : 0.25f);
                                var iconH = bmpMenuInstance.ElementSize.Height * scale;
                                var iconX = sx / 2 - iconH / 2;
                                var iconY = patternHeaderSizeY / 2 + channelSizeY / 2 - iconH / 2;

                                f.PushTranslation(0, py);
                                f.DrawTextureAtlas(bmpMenuInstance, iconX + 1, iconY + 1, scale, Theme.BlackColor);
                                f.DrawTextureAtlas(bmpMenuInstance, iconX, iconY, scale, Theme.WhiteColor);
                                f.PopTransform();
                            }
                        }

                        if (Platform.IsMobile && HighlightLocation == location)
                        {
                            c.DrawRectangle(0, py, sx, py + channelSizeY, Theme.WhiteColor, 3, true, true);
                        }

                        py += channelSizeY;
                    }
                }

                c.PopTransform();
            }

            // Piano roll view rect
            if (App.GetPianoRollViewRange(out var pianoRollMinNoteIdx, out var pianoRollMaxNoteIdx, out var pianoRollChannelIndex) && GetChannelToRowForIndex(pianoRollChannelIndex) >= 0)
            {
                c.PushTranslation(GetPixelForNote(pianoRollMinNoteIdx), GetChannelToRowForIndex(pianoRollChannelIndex) * channelSizeY);
                c.DrawRectangle(1, patternHeaderSizeY + 1, GetPixelForNote(pianoRollMaxNoteIdx - pianoRollMinNoteIdx, false) - 1, channelSizeY - 1, Theme.LightGreyColor2);
                c.PopTransform();
            }

            // Seek bar
            b.DrawLine(SeekX, 0, SeekX, virtualSizeY, GetSeekBarColor(), 3);

            // Scroll bar (optional) TODO: Use "real" scrollbar controls.
/*             if (GetScrollBarParams(true, out var scrollBarThumbPosX, out var scrollBarSizeThumbX, out var scrollBarSizeX))
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
            } */

            c.PopTransform();
            c.PopClipRegion();
        }
    }
}