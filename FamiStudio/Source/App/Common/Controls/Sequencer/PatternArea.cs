using System;
using System.Collections.Generic;

namespace FamiStudio
{
    public class PatternArea : Container
    {
        private const int DefaultPatternNamePosX = 2;

        private int channelSizeY;
        private int patternHeaderSizeY;
        private int patternNamePosX;
        private int rowToChannelCount;
        private int virtualSizeY;
        private List<PatternPanel> panels;

        private PatternBitmapCache patternCache;

        private Sequencer Sequencer => App?.Sequencer;
        private Song Song => App?.SelectedSong;

        private bool CaptureThresholdMet => Sequencer.CaptureThresholdMet;
        private bool IsColumnSelectionInProgress => Sequencer.IsColumnSelectionInProgress;
        private bool IsDragSelectionInProgress => Sequencer.IsDragSelectionInProgress;
        private bool IsRectangleSelectionInProgress => Sequencer.IsRectangleSelectionInProgress;
        private bool IsValidTimeOnlySelection => Sequencer.HasValidTimeOnlySelection;
        private int SeekX => Sequencer.SeekX;
        public int SelectionDragAnchorPatternIdx => Sequencer.SelectionDragAnchorPatternIdx;
        public float SelectionDragAnchorPatternXFraction => Sequencer.SelectionDragAnchorPatternXFraction;

        private Color SelectedPatternInvisibleColor => Sequencer.SelectedPatternInvisibleColor;
        private Color SelectedPatternVisibleColor => Sequencer.SelectedPatternVisibleColor;
        private IReadOnlyCollection<PatternLocation> SelectedPatternLocations => Sequencer.SelectedPatternLocations;
        private PatternLocation HighlightLocation => Sequencer.HighlightLocation;
        private PatternLocation MarqueeMax => Sequencer.MarqueeMax;
        private PatternLocation MarqueeMin => Sequencer.MarqueeMin;
        private PatternLocation SelectionMax => Sequencer.SelectionMax;
        private PatternLocation SelectionMin => Sequencer.SelectionMin;
        private int CaptureRowIdx => Sequencer.CaptureRowIdx;
        private float BitmapScale => Sequencer.BitmapScale;
        
        public PatternBitmapCache PatternCache => patternCache;

        public delegate void EditPatternDelegate(Point p, Pattern pat, PatternLocation l, bool sel);
        public delegate void EmptyDelegate();
        public delegate void MoveCopyDelegate(PatternLocation l, bool copy);
        public delegate void LocationDelegate(PatternLocation l);
        public delegate void PatternClickedDelegate(int channelIdx, int patternIdx, bool setActive);
        public delegate void PointerDelegate(Control c, PointerEventArgs e);
        public delegate void SelectionDelegate(Control c, Point p);
        public delegate void SelectionDelegatePattern(Control c, Point p, int idx);
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
        public event SelectionDelegatePattern StartDragSelection;
        public event SelectionDelegatePattern StartRectangleSelection;

        TextureAtlasRef bmpInstantiate;
        TextureAtlasRef bmpDuplicate;
        TextureAtlasRef bmpDuplicateMove;

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
        }

        private void CreateOrRecreatePanels()
        {
            if (Song == null || patternCache == null)
                return;

            if (panels != null)
                RemoveAllControls();

            panels = [];

            for (int ci = 0; ci < Song.Channels.Length; ci++)
            {
                for (int pi = 0; pi < Song.Length; pi++)
                {
                    var panel = new PatternPanel(patternCache, new PatternLocation(ci, pi));
                    panels.Add(panel);
                    AddControl(panel);

                    panel.PointerDown        += Panel_PointerDown;
                    panel.PointerUp          += Panel_PointerUp;
                    panel.PointerDownDelayed += Panel_PointerDownDelayed;
                    panel.MouseDoubleClick   += Panel_MouseTouchDoubleClick;
                    panel.TouchClick         += Panel_TouchClick;
                    panel.TouchDoubleClick   += Panel_MouseTouchDoubleClick;
                    panel.TouchLongPress     += Panel_TouchLongPress;
                }
            }

            UpdatePanelLayouts();
        }

        private void UpdatePanelLayouts()
        {
            if (panels == null)
                return;

            foreach (var p in panels)
            {
                var loc = p.Location;
                var row = GetChannelToRowForIndex(loc.ChannelIndex); // < 0 when channel is hidden
                var px  = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(loc.PatternIndex), false);
                var sx  = GetPixelForNote(Song.GetPatternLength(loc.PatternIndex), false);

                p.Visible = row >= 0;

                if (p.Visible)
                {
                    p.Move(px, row * channelSizeY, sx, channelSizeY);
                    p.UpdateLayout(channelSizeY, patternHeaderSizeY, patternNamePosX);
                }
            }
        }

        public void UpdateLayout(int channelSizeY, int patternHeaderSizeY, int rowToChannelCount)
        {
            this.channelSizeY = channelSizeY;
            this.patternHeaderSizeY = patternHeaderSizeY;
            this.rowToChannelCount = rowToChannelCount;

            virtualSizeY = rowToChannelCount * channelSizeY;
            patternNamePosX = DpiScaling.ScaleForWindow(DefaultPatternNamePosX);

            patternCache.Update(channelSizeY - patternHeaderSizeY - 1);

            if (panels == null || panels.Count != Song.Channels.Length * Song.Length)
                CreateOrRecreatePanels();
            else
                UpdatePanelLayouts();
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
            return Sequencer.IsPatternSelected(location);
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
            return Utils.Clamp((y + ScrollY) / channelSizeY, 0, rowToChannelCount - 1);
        }

        private int GetChannelIndexForCoord(int y)
        {
            return rowToChannelCount > 0 ? GetRowToChannelForIndex(GetRowIndexForCoord(y)) : -1;
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
            return IsSelectionValid() && SelectedPatternLocations.Count > 1;
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
            var pattern = channel.PatternInstances[location.PatternIndex];

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
            var isMarquee = IsRectangleSelectionInProgress || IsColumnSelectionInProgress;

            var minSelChannel = isMarquee ? MarqueeMin.ChannelIndex : SelectionMin.ChannelIndex;
            var maxSelChannel = isMarquee ? MarqueeMax.ChannelIndex : SelectionMax.ChannelIndex;
            
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

        private bool SelectionContainsSharedPatterns()
        {
            foreach (var p in SelectedPatternLocations)
            {
                if (Song.IsPatternShared(Song.GetPatternInstance(p)))
                    return true;
            }

            return false;
        }

        private void ShowContextMenu(Point p, PatternLocation location)
        {
            var x = p.X;
            var y = p.Y;

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
                    menu.Add(new ContextMenuOption("MenuProperties", PatternPropertiesLabel, () => { EditPatternProperties?.Invoke(new Point(x, y), pattern, location, false); }, ContextMenuSeparator.Before));
                }

                menu.Insert(0, new ContextMenuOption("MenuDelete", DeletePatternLabel, () => { DeletePattern(location); }));
            }

            if (IsPatternSelected(location))
            {
                if (SelectionContainsSharedPatterns())
                    menu.Insert(1, new ContextMenuOption("MenuUnlink", MakePatternsUniqueLabel, () => { MakeSelectedPatternsUnique?.Invoke(); }));
                menu.Insert(1, new ContextMenuOption("MenuInstance", MergeIdenticalPatternsLabel, () => { MergeIdenticalPatterns?.Invoke(); }));
            }

            if (menu.Count > 0)
                App.ShowContextMenuAsync(menu.ToArray());
        }

        private void UpdateTooltip(PatternLocation location)
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

        private void StartSelection(PatternPanel panel, PointerEventArgs e)
        {
            var p = WindowToControl(panel.ControlToWindow(e.Position));
            StartRectangleSelection?.Invoke(this, p, panel.Location.PatternIndex);
        }

        private void HandlePanelPointerDownTouchClick(Control control, PointerEventArgs e)
        {
            if (e.Left)
            {
                if (control is PatternPanel panel)
                {
                    var location = panel.Location;
                    var pattern  = Song.GetPatternInstance(location);
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
                            ClearSelection?.Invoke();
                            SetSelection?.Invoke(location);
                            SelectionChanged?.Invoke();
                        }

                        StartDragSelection?.Invoke(panel, e.Position, location.PatternIndex);
                    }
                }
            }
            else if (e.Right)
            {
                if (ModifierKeys.IsAltDown && Settings.AltZoomAllowed)
                {   
                    StartAltZoom?.Invoke(control, e);
                    return;
                }

                e.DelayRightClick(); // Need to wait to see difference between context menu or selection.
            }
        }

        private void Panel_PointerDown(Control control, PointerEventArgs e)
        {
            if (!e.IsTouchEvent)
                HandlePanelPointerDownTouchClick(control, e);
        }

        private void Panel_PointerUp(Control control, PointerEventArgs e)
        {
            if (e.Left)
            {
                //EndSelection?.Invoke(control, e.Position);
                return;
            }

            if (e.Right || e.IsLongPress)
            {
                if (control is PatternPanel panel)
                {
                    if (!CaptureThresholdMet)
                    {
                        if (!ModifierKeys.IsControlDown && !IsPatternSelected(panel.Location) && Song.GetPatternInstance(panel.Location) != null)
                            ClearSelection?.Invoke();

                        ShowContextMenu(e.Position, panel.Location);
                    }
                }

                //EndSelection?.Invoke(control, e.Position);

                //e.MarkHandled();
            }
        }

        private void Panel_PointerDownDelayed(Control control, PointerEventArgs e)
        {
            if (e.Right && control is PatternPanel panel)
            {
                StartSelection(panel, e);
            }
        }

        private void Panel_TouchClick(Control control, PointerEventArgs e)
        {
            HandlePanelPointerDownTouchClick(control, e);
        }

        private void Panel_MouseTouchDoubleClick(Control control, PointerEventArgs e)
        {
            if (control is PatternPanel panel)
            {
                var pattern = Song.GetPatternInstance(panel.Location);
                if (pattern != null)
                {
                    DeletePattern(panel.Location);
                    ClearHighlightedPattern?.Invoke();
                }
            }
        }

        private void Panel_TouchLongPress(Control control, PointerEventArgs e)
        {
            if (!e.IsDoubleTapLongPress && control is PatternPanel panel)
            {
                Platform.VibrateClick();
                StartSelection(panel, e);
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

                    StartDragSelection?.Invoke(this, e.Position, HighlightLocation.PatternIndex);
                    e.MarkHandled();
                }
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

        public override void OnContainerPointerMoveNotify(Control control, PointerEventArgs e)
        {
            if (control is PatternPanel panel)
                UpdateTooltip(panel.Location);
            else
                tooltip = "";
        }

        public void SetScrollX(int s)
        {
            if (ScrollX != s)
            {
                ScrollX = s;
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

            // Background
            c.DrawLine(0, 0, width, 0, Theme.BlackColor);
            c.DrawLine(0, height - 1, width, height - 1, Theme.BlackColor);

            for (int i = MinVisiblePattern(); i < MaxVisiblePattern(); i++)
            {
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                var sx = GetPixelForNote(Song.GetPatternLength(i), false);
                var color = (i & 1) == 0 ? Theme.DarkGreyColor4 : Theme.DarkGreyColor2;
                b.FillRectangle(px, 0, px + sx, height, color);
            }

            // Selection
            if (Sequencer.IsRectangleSelectionInProgress || Sequencer.IsColumnSelectionInProgress)
            {
                if (GetMinMaxSelectedRow(out var minSelRow, out var maxSelRow))
                {
                    c.FillRectangle(
                        GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Math.Min(MarqueeMin.PatternIndex + 0, Song.Length))), channelSizeY * (minSelRow + 0) - ScrollY,
                        GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Math.Min(MarqueeMax.PatternIndex + 1, Song.Length))), channelSizeY * (maxSelRow + 1) - ScrollY,
                        Sequencer.IsActiveControl ? SelectedPatternVisibleColor : SelectedPatternInvisibleColor);
                }
            }

            // Vertical lines
            for (int i = Math.Max(1, MinVisiblePattern()); i <= MaxVisiblePattern(); i++)
            {
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                c.DrawLine(px, 0, px, height, Theme.BlackColor);
            }

            c.PushTranslation(0, -ScrollY);

            // Horizontal lines
            for (int i = 0, y = 0; i <= rowToChannelCount; i++, y += channelSizeY)
                c.DrawLine(0, y, width, y, Theme.BlackColor);

            // TODO : This is really bad, since all the logic is in the rendering code. Make
            // this more like any other capture op eventually.
            if (IsDragSelectionInProgress)
            {
                var pt = MouseLastPosition();
                var noteIdx = GetNoteForPixel(pt.X);

                if (noteIdx >= 0 && noteIdx < Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                {
                    var patternIdx      = Song.PatternIndexFromAbsoluteNoteIndex(noteIdx);
                    var patternIdxDelta = patternIdx - SelectionDragAnchorPatternIdx;
                    var rowIdxDelta     = GetRowIndexForCoord(pt.Y) - CaptureRowIdx;

                    // Anchor for where the pattern under the cursor should start.
                    var anchorSizeX = GetPixelForNote(Song.GetPatternLength(patternIdx), false);
                    var anchorLeftX = pt.X - (int)(anchorSizeX * SelectionDragAnchorPatternXFraction);
                    var shiftX      = anchorLeftX - GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(patternIdx), false);

                    var instance  = ModifierKeys.IsControlDown;
                    var duplicate = instance && ModifierKeys.IsShiftDown;
                    var bmpCopy   = rowIdxDelta != 0 ? ((duplicate || instance) ? bmpDuplicate : bmpDuplicateMove) : (duplicate ? bmpDuplicate : (instance ? bmpInstantiate : null));
                    var bmpSize   = DpiScaling.ScaleCustom(bmpDuplicate.ElementSize.Width, BitmapScale);

                    foreach (var loc in SelectedPatternLocations)
                    {
                        var row = GetChannelToRowForIndex(loc.ChannelIndex) + rowIdxDelta;
                        var tp  = loc.PatternIndex + patternIdxDelta;

                        if (row < 0 || row >= rowToChannelCount || tp < 0 || tp >= Song.Length)
                            continue;

                        var x = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(tp), false) + shiftX;
                        var w = GetPixelForNote(Song.GetPatternLength(tp), false);
                        var y = row * channelSizeY;

                        c.FillAndDrawRectangle(x, y, x + w, y + channelSizeY, SelectedPatternVisibleColor, Theme.BlackColor);

                        if (bmpCopy != null)
                            c.DrawTextureAtlas(bmpCopy, x + w / 2 - bmpSize / 2, y + channelSizeY / 2 - bmpSize / 2, BitmapScale, Theme.LightGreyColor1);
                    }
                }
            }

            c.PopTransform();

            // Pattern panels
            base.OnRender(g);

            // Piano roll view rect
            if (App.GetPianoRollViewRange(out var pianoRollMinNoteIdx, out var pianoRollMaxNoteIdx, out var pianoRollChannelIndex) && GetChannelToRowForIndex(pianoRollChannelIndex) >= 0)
            {
                c.PushTranslation(GetPixelForNote(pianoRollMinNoteIdx), GetChannelToRowForIndex(pianoRollChannelIndex) * channelSizeY - ScrollY);
                c.DrawRectangle(1, patternHeaderSizeY + 1, GetPixelForNote(pianoRollMaxNoteIdx - pianoRollMinNoteIdx, false) - 1, channelSizeY - 1, Theme.LightGreyColor2);
                c.PopTransform();
            }

            // Seek bar
            b.DrawLine(SeekX, 0, SeekX, virtualSizeY, GetSeekBarColor(), 3);
        }
    }
}