using System;
using System.Collections.Generic;
using System.Data.Common;

namespace FamiStudio
{
    public partial class Sequencer : Container
    {
        private class PatternArea : Container
        {
            private const int DefaultPatternNamePosX = 2;

            private Sequencer sequencer;
            private PatternBitmapCache patternCache;
            private int channelSizeY;
            private int patternHeaderSizeY;
            private int patternNamePosX;
            private int rowToChannelCount;
            private int virtualSizeY;

            private Song Song => App?.SelectedSong;

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
            public event EmptyDelegate MakeSelectedPatternsUnique;
            public event EmptyDelegate MergeIdenticalPatterns;
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
            LocalizedString MergeIdenticalPatternsLabel;

            public PatternArea(Sequencer seq)
            {
                Localization.Localize(this);
                supportsLongPress = true;
                supportsDoubleClick = true;
                sequencer = seq;
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

                virtualSizeY = rowToChannelCount * channelSizeY;
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
                var p = sequencer.mouseLastPos;
                return new Point(p.X - Left, p.Y - Top);
            }

            private int GetRowIndexForCoord(int y)
            {
                return Utils.Clamp((y + ScrollY) / channelSizeY, 0, rowToChannelCount - 1);
            }

            private int GetChannelIndexForCoord(int y)
            {
                return rowToChannelCount > 0 ? sequencer.GetRowToChannelForIndex(GetRowIndexForCoord(y)) : -1;
            }
            
    #if DEBUG
            public void ValidateIntegrity()
            {
                patternCache.ValidateIntegrity();
            }
    #endif

            private bool SelectionContainsMultiplePatterns()
            {
                return sequencer.IsSelectionValid() && sequencer.selectedPatternLocations.Count > 1;
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
                Song.InvalidatePatternRefCounts();
                App.UndoRedoManager.EndTransaction();

                ClearSelection?.Invoke();
                MarkDirty();
            }

            private void CreateNewPattern(PatternLocation location)
            {
                var channel = Song.Channels[location.ChannelIndex];

                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, location.ChannelIndex);
                channel.PatternInstances[location.PatternIndex] = channel.CreatePattern();
                channel.InvalidateCumulativePatternCache();
                PatternClicked?.Invoke(location.ChannelIndex, location.PatternIndex, false);
                Song.InvalidatePatternRefCounts();
                App.UndoRedoManager.EndTransaction();

                ClearSelection?.Invoke();
                MarkDirty();
            }

            private bool GetMinMaxSelectedRow(out int minSelRow, out int maxSelRow)
            {
                var isMarquee = sequencer.captureOperation == SelectRectangle || sequencer.captureOperation == SelectColumn;

                var minSelChannel = isMarquee ? sequencer.marqueeMin.ChannelIndex : sequencer.selectionMin.ChannelIndex;
                var maxSelChannel = isMarquee ? sequencer.marqueeMax.ChannelIndex : sequencer.selectionMax.ChannelIndex;
                
                while (minSelChannel < Song.Channels.Length && sequencer.GetChannelToRowForIndex(minSelChannel) < 0) minSelChannel++;
                while (maxSelChannel >= 0 && sequencer.GetChannelToRowForIndex(maxSelChannel) < 0) maxSelChannel--;

                if (minSelChannel < Song.Channels.Length && maxSelChannel >= 0)
                {
                    minSelRow = sequencer.GetChannelToRowForIndex(minSelChannel);
                    maxSelRow = sequencer.GetChannelToRowForIndex(maxSelChannel);
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
                var noteIdx = sequencer.GetNoteForPixel(x);

                if (noteIdx < 0 || noteIdx >= Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                {
                    location = PatternLocation.Invalid;
                    return false;
                }

                location = new PatternLocation(GetChannelIndexForCoord(y), Song.PatternIndexFromAbsoluteNoteIndex(noteIdx));

                return location.IsChannelInSong(Song);
            }

            private bool SelectionContainsSharedPatterns()
            {
                foreach (var p in sequencer.selectedPatternLocations)
                {
                    if (Song.IsPatternShared(Song.GetPatternInstance(p)))
                        return true;
                }

                return false;
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
                else if (pattern != null && !sequencer.IsPatternSelected(location))
                {
                    SetSelection?.Invoke(location);
                }

                if (sequencer.IsSelectionValid() && !sequencer.IsPatternSelected(location))
                {
                    if (Platform.IsMobile)
                        menu.Add(new ContextMenuOption("MenuExpandSelection", ExpandSelectionLabel, () => { EnsureSelectionInclude?.Invoke(location); }));
                    if (sequencer.selectionMin.ChannelIndex == location.ChannelIndex)
                        menu.Add(new ContextMenuOption("MenuInstance", InstanciateHereLabel, () => { CopySelectionToCursor?.Invoke(location, false); }));
                    menu.Add(new ContextMenuOption("MenuDuplicate", DuplicateHereLabel, () => { CopySelectionToCursor?.Invoke(location, true); }));
                }

                if (sequencer.IsSelectionValid())
                {
                    menu.Add(new ContextMenuOption("MenuClearSelection", ClearSelectionLabel, () => { ClearSelection?.Invoke(); ClearHighlightedPattern?.Invoke(); }, ContextMenuSeparator.Before));
                }

                if (pattern != null)
                {
                    if (sequencer.IsPatternSelected(location) && SelectionContainsMultiplePatterns())
                    {
                        menu.Insert(0, new ContextMenuOption("MenuDeleteSelection", DeleteSelectionLabel, () => { DeleteSelection?.Invoke(); }));
                        menu.Add(new ContextMenuOption("MenuProperties", SelectedPatternPropertiesLabel, () => { EditPatternProperties(new Point(x, y), pattern, location, true); }, ContextMenuSeparator.Before));
                    }
                    else
                    {
                        menu.Add(new ContextMenuOption("MenuProperties", PatternPropertiesLabel, () => { EditPatternProperties?.Invoke(new Point(x, y), pattern, location, false); }, ContextMenuSeparator.Before));
                    }

                    menu.Insert(0, new ContextMenuOption("MenuDelete", DeletePatternLabel, () => { DeletePattern(location); }));
                }

                if (sequencer.IsPatternSelected(location))
                {
                    if (SelectionContainsSharedPatterns())
                        menu.Insert(1, new ContextMenuOption("MenuUnlink", MakePatternsUniqueLabel, () => { MakeSelectedPatternsUnique?.Invoke(); }));
                    menu.Insert(1, new ContextMenuOption("MenuInstance", MergeIdenticalPatternsLabel, () => { MergeIdenticalPatterns?.Invoke(); }));
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

                    if (sequencer.IsPatternSelected(location))
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

            private void StartSelection(PointerEventArgs e)
            {
                if (GetPatternForCoord(e.X, e.Y, out var location))
                {
                    SetHighlightedPattern(location);
                    StartRectangleSelection?.Invoke(this, e, location.PatternIndex);
                }
            }
            
            protected void OnTouchDown(PointerEventArgs e)
            {
                if (GetPatternForCoord(e.X, e.Y, out var location))
                {
                    if (sequencer.highlightLocation == location)
                    {
                        if (!sequencer.IsPatternSelected(sequencer.highlightLocation))
                            SetSelection?.Invoke(sequencer.highlightLocation);

                        StartDragSelection?.Invoke(this, e, sequencer.highlightLocation.PatternIndex);
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
                        if (sequencer.highlightLocation == location)
                            ClearHighlightedPattern?.Invoke();
                        else
                            SetHighlightedPattern?.Invoke(location);

                        // Tapping inside the selection toggles highlight, but doesnt clear selection.
                        if (sequencer.IsPatternSelected(location))
                            return;
                    }

                    // CreateNewPattern clears the selection. Ugh, call after.
                    ClearSelection?.Invoke();
                    SetSelection?.Invoke(location);
                }
            }

            protected override void OnTouchDoubleClick(PointerEventArgs e)
            {
                OnMouseDoubleClick(e);
            }

            protected override void OnTouchLongPress(PointerEventArgs e)
            {
                Platform.VibrateClick();
                StartSelection(e);
            }

            protected override void OnPointerDown(PointerEventArgs e)
            {
                if (e.IsTouchEvent)
                {
                    OnTouchDown(e);
                    return;
                }

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

                            if (!sequencer.IsPatternSelected(location))
                            {
                                ClearSelection?.Invoke();
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
                    StartSelection(e);
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
                        if ((!e.IsTouchEvent && sequencer.IsPatternHighlighted(location)) || sequencer.highlightLocation == location)
                        {
                            DeletePattern(location);
                            ClearHighlightedPattern?.Invoke();
                        }
                        else
                        {
                            if (!sequencer.IsPatternSelected(location))
                                ClearSelection?.Invoke();

                            SetSelection?.Invoke(location);
                            SetHighlightedPattern?.Invoke(location);
                        }
                    }
                    else
                    {
                        CreateNewPattern(location);
                    }
                }
            }

            protected override void OnPointerUp(PointerEventArgs e)
            {
                if (e.Right || e.IsLongPress)
                {
                    if (!sequencer.captureThresholdMet)
                    {
                        if (!ModifierKeys.IsControlDown && GetPatternForCoord(e.X, e.Y, out var location) && !sequencer.IsPatternSelected(location))
                            ClearSelection?.Invoke();

                        ShowContextMenu(e.Position);
                    }
                }
            }

            protected override void OnPointerMove(PointerEventArgs e)
            {
                UpdateTooltip(e);
            }

            public void SetScrollX(int s)
            {
                if (ScrollY != s)
                {
                    ScrollY = s;
                    MarkDirty();
                }
            }

            public void SetScrollY(int s)
            {
                if (ScrollY != s)
                {
                    ScrollY = s;
                    MarkDirty();
                }
            }

            protected override void OnRender(Graphics g)
            {
                var c = g.DefaultCommandList;
                var b = g.BackgroundCommandList;
                var f = g.ForegroundCommandList;

                c.PushClipRegion(0, 0, width, height);

                // Background
                c.DrawLine(0, 0, width, 0, Theme.BlackColor);
                c.DrawLine(0, height - 1, width, height - 1, Theme.BlackColor);

                for (int i = sequencer.MinVisiblePattern(); i < sequencer.MaxVisiblePattern(); i++)
                {
                    var px = sequencer.GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                    var sx = sequencer.GetPixelForNote(Song.GetPatternLength(i), false);
                    var color = (i & 1) == 0 ? Theme.DarkGreyColor4 : Theme.DarkGreyColor2;
                    b.FillRectangle(px, 0, px + sx, height, color);
                }

                // Selection highlight
                if (sequencer.captureOperation == SelectRectangle || sequencer.captureOperation == SelectColumn)
                {
                    if (GetMinMaxSelectedRow(out var minSelRow, out var maxSelRow))
                    {
                        c.FillRectangle(
                            sequencer.GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Math.Min(sequencer.marqueeMin.PatternIndex + 0, Song.Length))), channelSizeY * (minSelRow + 0) - ScrollY,
                            sequencer.GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Math.Min(sequencer.marqueeMax.PatternIndex + 1, Song.Length))), channelSizeY * (maxSelRow + 1) - ScrollY,
                            sequencer.IsActiveControl ? sequencer.selectedPatternVisibleColor : sequencer.selectedPatternInvisibleColor);
                    }
                }

                // Vertical lines
                for (int i = Math.Max(1, sequencer.MinVisiblePattern()); i <= sequencer.MaxVisiblePattern(); i++)
                {
                    var px = sequencer.GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                    c.DrawLine(px, 0, px, height, Theme.BlackColor);
                }

                c.PushTranslation(0, -ScrollY);

                // Horizontal lines
                for (int i = 0, y = 0; i <= rowToChannelCount; i++, y += channelSizeY)
                    c.DrawLine(0, y, width, y, Theme.BlackColor);

                // TODO : This is really bad, since all the logic is in the rendering code. Make
                // this more like any other capture op eventually.
                if (sequencer.captureOperation == DragSelection)
                {
                    var pt = MouseLastPosition();
                    var noteIdx = sequencer.GetNoteForPixel(pt.X);

                    if (noteIdx >= 0 && noteIdx < Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                    {
                        var patternIdx      = Song.PatternIndexFromAbsoluteNoteIndex(noteIdx);
                        var patternIdxDelta = patternIdx - sequencer.selectionDragAnchorPatternIdx;
                        var rowIdxDelta     = GetRowIndexForCoord(pt.Y) - sequencer.captureRowIdx;

                        // Anchor for where the pattern under the cursor should start.
                        var anchorSizeX = sequencer.GetPixelForNote(Song.GetPatternLength(patternIdx), false);
                        var anchorLeftX = pt.X - (int)(anchorSizeX * sequencer.selectionDragAnchorPatternXFraction);
                        var shiftX      = anchorLeftX - sequencer.GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(patternIdx), false);

                        var instance  = ModifierKeys.IsControlDown;
                        var duplicate = instance && ModifierKeys.IsShiftDown;
                        var bmpCopy   = rowIdxDelta != 0 ? ((duplicate || instance) ? bmpDuplicate : bmpDuplicateMove) : (duplicate ? bmpDuplicate : (instance ? bmpInstantiate : null));
                        var bmpSize   = DpiScaling.ScaleCustom(bmpDuplicate.ElementSize.Width, sequencer.bitmapScale);

                        foreach (var loc in sequencer.selectedPatternLocations)
                        {
                            var row = sequencer.GetChannelToRowForIndex(loc.ChannelIndex) + rowIdxDelta;
                            var tp  = loc.PatternIndex + patternIdxDelta;

                            if (row < 0 || row >= rowToChannelCount || tp < 0 || tp >= Song.Length)
                                continue;

                            var x = sequencer.GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(tp), false) + shiftX;
                            var w = sequencer.GetPixelForNote(Song.GetPatternLength(tp), false);
                            var y = row * channelSizeY;

                            c.FillAndDrawRectangle(x, y, x + w, y + channelSizeY, sequencer.selectedPatternVisibleColor, Theme.BlackColor);

                            if (bmpCopy != null)
                                c.DrawTextureAtlas(bmpCopy, x + w / 2 - bmpSize / 2, y + channelSizeY / 2 - bmpSize / 2, sequencer.bitmapScale, Theme.LightGreyColor1);
                        }
                    }
                }

                // Patterns
                var patternCacheSizeY = channelSizeY - patternHeaderSizeY - 1;
                patternCache.Update(patternCacheSizeY);

                for (int pi = sequencer.MinVisiblePattern(); pi < sequencer.MaxVisiblePattern(); pi++)
                {
                    var patternLen = Song.GetPatternLength(pi);
                    var noteLen = Song.UsesFamiTrackerTempo ? 1 : Song.GetPatternNoteLength(pi);
                    var px = sequencer.GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(pi));
                    var sx = sequencer.GetPixelForNote(patternLen, false);

                    c.PushTranslation(px, 0);

                    // TODO : Dont draw channels that are not visible!
                    for (int ci = 0, py = 0; ci < Song.Channels.Length; ci++)
                    {
                        if (sequencer.GetChannelVisibleForIndex(ci))
                        {
                            var location = new PatternLocation(ci, pi);
                            var pattern = Song.GetPatternInstance(location);

                            if (pattern != null)
                            {
                                var bmp = patternCache.GetOrAddPattern(pattern, patternLen, noteLen, out var u0, out var v0, out var u1, out var v1);
                                var isSelected = sequencer.IsPatternHighlighted(location);
                                var font = isSelected ? Fonts.FontSmallBold : Fonts.FontSmall;

                                c.PushTranslation(0, py);
                                c.FillRectangleGradient(1, 1, sx, patternHeaderSizeY, pattern.Color, pattern.Color.Scaled(0.8f), true, patternHeaderSizeY);
                                c.FillRectangle(1, patternHeaderSizeY, sx, channelSizeY, Color.FromArgb(64, pattern.Color));
                                c.DrawLine(0, patternHeaderSizeY, sx, patternHeaderSizeY, Theme.BlackColor);
                                c.DrawTexture(bmp, 1.0f, 1.0f + patternHeaderSizeY, sx - 1, patternCacheSizeY, u0, v0, u1, v1);

                                if (isSelected)
                                {
                                    var highlighted = Platform.IsMobile && sequencer.highlightLocation == location;
                                    var color = highlighted ? Theme.BlueColor2 : Theme.WhiteColor;

                                    f.FillRectangle(0, 0, sx, channelSizeY, Color.FromArgb(48, color));

                                    if (highlighted)
                                        f.DrawRectangle(0, 0, sx, channelSizeY, color, 3, true, true);
                                    else
                                        c.DrawRectangle(0, 0, sx, channelSizeY, color, 3, true, true);
                                        
                                    f.DrawLine(0, patternHeaderSizeY, sx, patternHeaderSizeY, color, 3, true, true);
                                    f.DrawText(pattern.Name, font, patternNamePosX + 1, 1, Theme.BlackColor, TextFlags.Left | TextFlags.Middle | TextFlags.Clip, sx - patternNamePosX, patternHeaderSizeY + 1);
                                }

                                f.DrawText(pattern.Name, font, patternNamePosX, 0, isSelected ? Theme.WhiteColor : Theme.BlackColor, TextFlags.Left | TextFlags.Middle | TextFlags.Clip, sx - patternNamePosX, patternHeaderSizeY + 1);

                                c.PopTransform();

                                var capture = sequencer.captureOperation;
                                if (capture != SelectRectangle && capture != SelectColumn && capture != DragSelection && sequencer.IsPatternSelected(location) && Song.IsPatternShared(pattern))
                                {
                                    var scale = sequencer.channelBitmapScale;
                                    var iconH = bmpMenuInstance.ElementSize.Height * scale;
                                    var iconX = sx / 2 - iconH / 2;
                                    var iconY = patternHeaderSizeY / 2 + channelSizeY / 2 - iconH / 2;

                                    f.PushTranslation(0, py);
                                    f.DrawTextureAtlas(bmpMenuInstance, iconX + 1, iconY + 1, scale, Theme.BlackColor);
                                    f.DrawTextureAtlas(bmpMenuInstance, iconX, iconY, scale, Theme.WhiteColor);
                                    f.PopTransform();
                                }
                            }

                            py += channelSizeY;
                        }
                    }

                    c.PopTransform();
                }

                // Piano roll view rect
                if (App.GetPianoRollViewRange(out var pianoRollMinNoteIdx, out var pianoRollMaxNoteIdx, out var pianoRollChannelIndex) && sequencer.GetChannelToRowForIndex(pianoRollChannelIndex) >= 0)
                {
                    c.PushTranslation(sequencer.GetPixelForNote(pianoRollMinNoteIdx), sequencer.GetChannelToRowForIndex(pianoRollChannelIndex) * channelSizeY);
                    c.DrawRectangle(1, patternHeaderSizeY + 1, sequencer.GetPixelForNote(pianoRollMaxNoteIdx - pianoRollMinNoteIdx, false) - 1, channelSizeY - 1, Theme.LightGreyColor2);
                    c.PopTransform();
                }

                // Seek bar
                b.DrawLine(sequencer.SeekX, 0, sequencer.SeekX, virtualSizeY, sequencer.GetSeekBarColor(), 3);

                c.PopTransform();
                c.PopClipRegion();
            }
        }
    }
}