using System;
using System.Collections.Generic;

namespace FamiStudio
{
    public class SequencerTimeline : Container
    {
        private List<SequencerTimelinePanel> panelList;
        private float[] seekGeometry;

        private Sequencer Sequencer => App?.Sequencer;
        private Song Song => App?.SelectedSong;
        
        private PatternLocation SelectionMax => Sequencer.SelectionMax;
        private PatternLocation SelectionMin => Sequencer.SelectionMin;

        private int SeekX => Sequencer.SeekX;

        public delegate void PointerDelegate(Control c, PointerEventArgs e);
        public delegate void EmptyDelegate();
        
        public event EmptyDelegate CancelDragSeekBar;
        public event EmptyDelegate PatternModified;
        public event PointerDelegate ColumnSelect;
        public event PointerDelegate DragSeekBar;
        public event PointerDelegate StartAltZoom;

        // Tooltips
        LocalizedString MoreOptionsTooltip;
        LocalizedString PanTooltip;
        LocalizedString SeekTooltip;
        LocalizedString SelectColumnTooltip;
        LocalizedString SetLoopPointTooltip;

        // Context menus
        LocalizedString ClearLoopPointLabel;
        LocalizedString SetLoopPointLabel;
        LocalizedString CustomPatternSettingsLabel;

        //Custom pattern settings dialog
        LocalizedString CustomPatternLabel;
        LocalizedString CustomPatternTitle;
        LocalizedString CustomPatternTooltip;

        public SequencerTimeline()
        {
            Localization.Localize(this);
            supportsLongPress = true;
        }

        protected override void OnAddedToContainer()
        {
            tooltip = $"<MouseLeft> {SeekTooltip} - <MouseRight> {MoreOptionsTooltip} - <MouseRight><Drag> {SelectColumnTooltip}\n<L><MouseLeft> {SetLoopPointTooltip} - <MouseWheel><Drag> {PanTooltip}";
        }

        public void Reset()
        {
            CreateOrRecreatePanels();
        }

        private void CreateOrRecreatePanels()
        {
            if (Song == null)
                return;

            if (panelList != null)
                RemoveAllControls();

            panelList = [];

            for (int i = 0; i < Song.Length; i++)
            {
                var patternLen = Song.GetPatternLength(i);
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                var sx = GetPixelForNote(patternLen, false);

                if (sx > 0)
                {
                    var panel = new SequencerTimelinePanel(i);
                    panel.Move(sx, 0, px + sx, height);
                    panelList.Add(panel);
                    AddControl(panel);
                }
            }

            UpdatePanelLayouts();
        }

        private void UpdatePanelLayouts()
        {
            if (panelList == null)
                return;

            foreach (var p in panelList)
            {
                var i  = p.PatternIndex;
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i), false);
                var sx = GetPixelForNote(Song.GetPatternLength(i), false);

                p.Visible = sx > 0;
                p.Move(px, 0, Math.Max(sx, 0), height);
            }
        }

        public void UpdateLayout()
        {
            seekGeometry = new float[]
            {
                -height / 2, 1,
                0, height - 2,
                height / 2, 1 
            };

            if (panelList == null || panelList.Count != Song.Length)
                CreateOrRecreatePanels();
            else
                UpdatePanelLayouts();
        }

        public void SetScroll(int x)
        {
            ScrollX = x;
        }

        private int GetPixelForNote(int note, bool scroll = true)
        {
            return Sequencer.GetPixelForNote(note, scroll);
        }

        private int GetNoteForPixel(int x, bool scroll = true)
        {
            return Sequencer.GetNoteForPixel(x, scroll);
        }

        private bool IsValidTimeOnlySelection()
        {
            return Sequencer.IsValidTimeOnlySelection();
        }

        private Color GetSeekBarColor()
        {
            return Sequencer.GetSeekBarColor();
        }

        private int GetPatternIndexForCoord(int x)
        {
            var noteIdx = GetNoteForPixel(x);

            if (noteIdx < 0 || noteIdx >= Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                return -1;

            return Song.PatternIndexFromAbsoluteNoteIndex(noteIdx);
        }

        private void EditPatternCustomSettings(Point pt, int patternIdx)
        {
            var dlg = new PropertyDialog(ParentWindow, CustomPatternTitle, new Point(left + pt.X, top + pt.Y), 300);
            var song = Song;
            var enabled = song.PatternHasCustomSettings(patternIdx);

            var patterns = new List<int> { patternIdx };

            if (IsValidTimeOnlySelection())
            {
                patterns = new List<int>(Sequencer.SelectedTimelineColumns);
                patterns.Sort();
            }

            var tempoProperties = new TempoProperties(dlg.Properties, song, patternIdx, patterns);

            dlg.Properties.AddCheckBox(CustomPatternLabel.Colon, song.PatternHasCustomSettings(patternIdx), CustomPatternTooltip); // 0
            tempoProperties.AddProperties();
            tempoProperties.EnableProperties(enabled);
            dlg.Properties.PropertyChanged += PatternCustomSettings_PropertyChanged;
            dlg.Properties.PropertiesUserData = tempoProperties;
            dlg.Properties.Build();

            dlg.ShowDialogAsync((r) =>
            {
                if (r == DialogResult.OK)
                {
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Song, song.Id);
                    tempoProperties.ApplyAsync(ParentWindow, dlg.Properties.GetPropertyValue<bool>(0), () =>
                    {
                        App.UndoRedoManager.EndTransaction();
                        UpdatePanelLayouts();
                        MarkDirty();
                        PatternModified?.Invoke();
                    });
                }
            });
        }

        private void PatternCustomSettings_PropertyChanged(PropertyPage props, int propIdx, int rowIdx, int colIdx, object value)
        {
            if (propIdx == 0)
            {
                var tempoProperties = props.PropertiesUserData as TempoProperties;
                tempoProperties.EnableProperties((bool)value);
            }
        }

        private bool IsPointOnSeekBar(int x)
        {
            return Math.Abs(SeekX - x) < height;
        }

        private void SetLoopPoint(int patternIdx)
        {
            App.UndoRedoManager.BeginTransaction(TransactionScope.Song, Song.Id);
            Song.SetLoopPoint(Song.LoopPoint == patternIdx ? -1 : patternIdx);
            App.UndoRedoManager.EndTransaction();
            MarkDirty();
        }

        private void ShowContextMenu(Point p, int patternIndex)
        {
            var isLoopPoint = App.SelectedSong.LoopPoint == patternIndex;

            App.ShowContextMenuAsync(new[]
            {
                new ContextMenuOption(isLoopPoint ? "MenuClearLoopPoint" : "MenuLoopPoint", isLoopPoint ?  ClearLoopPointLabel : SetLoopPointLabel, () => { SetLoopPoint(patternIndex); } ),
                new ContextMenuOption("MenuCustomPatternSettings", CustomPatternSettingsLabel, () => { EditPatternCustomSettings(p, patternIndex); } )
            });
        }

        protected void OnTouchDown(PointerEventArgs e)
        {
            // See if we are close enough to the yellow triangle.
            if (IsPointOnSeekBar(e.X))
            {
                DragSeekBar?.Invoke(this, e);
                return;
            }

            ColumnSelect?.Invoke(this, e);
        }

        public override void OnContainerTouchClickNotify(Control control, PointerEventArgs e)
        {
            App.SeekSong(GetNoteForPixel(e.X));
        }

        protected override void OnTouchLongPress(PointerEventArgs e)
        {
            if (!e.IsDoubleTapLongPress)
            {
                CancelDragSeekBar?.Invoke();
                //ShowContextMenu(e); // TODO: Wire this up again.
            }
        }

        public override void OnContainerPointerDownNotify(Control control, PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                OnTouchDown(e);
                return;
            }

            if (e.Left)
            {
                if (Settings.SetLoopPointShortcut.IsKeyDown(ParentWindow))
                {
                    if (control is SequencerTimelinePanel panel)
                        SetLoopPoint(panel.PatternIndex);
                }
                else
                {
                    DragSeekBar.Invoke(this, e);
                }
            }
            if (e.Right)
            {
                if (ModifierKeys.IsAltDown && Settings.AltZoomAllowed)
                {   
                    if (GetPatternIndexForCoord(e.X) >= 0)
                    {
                        StartAltZoom?.Invoke(this, e);
                        return;
                    }
                }

                e.DelayRightClick(); // Can have selection or context menu, need to wait.
            }
        }

        public override void OnContainerPointerUpNotify(Control control, PointerEventArgs e)
        {
            if (e.Right && !Sequencer.HasCaptureOperation)
            {
                if (control is SequencerTimelinePanel panel)
                    ShowContextMenu(e.Position, panel.PatternIndex);
            }
        }

        public override void OnContainerPointerDownDelayedNotify(Control control, PointerEventArgs e)
        {
            ColumnSelect?.Invoke(control, e);
        }

        protected override void OnRender(Graphics g)
        {
            base.OnRender(g);

            var c = g.DefaultCommandList;
            var f = g.ForegroundCommandList;

            // Seek bar
            c.PushTranslation(SeekX, 0);
            f.FillAndDrawGeometry(seekGeometry, GetSeekBarColor(), Theme.BlackColor, 1, true);
            c.PopTransform();
        }
    }
}