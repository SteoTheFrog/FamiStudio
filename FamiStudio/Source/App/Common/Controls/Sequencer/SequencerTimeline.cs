using System;

namespace FamiStudio
{
    public class SequencerTimeline : Control
    {
        private const int DefaultBarTextPosY    = 2;
        private const int DefaultHeaderIconPosX = 3;
        private const int DefaultHeaderIconPosY = 3;

        private Sequencer sequencer;
        private TextureAtlasRef bmpLoopPoint;
        private int barTextPosY    = DpiScaling.ScaleForWindow(DefaultBarTextPosY);
        private int headerIconPosX = DpiScaling.ScaleForWindow(DefaultHeaderIconPosX);
        private int headerIconPosY = DpiScaling.ScaleForWindow(DefaultHeaderIconPosY);
        private float[] seekGeometry;

        private Song Song => App?.SelectedSong;
        
        private Color SelectedPatternInvisibleColor => sequencer.SelectedPatternInvisibleColor;
        private Color SelectedPatternVisibleColor   => sequencer.SelectedPatternVisibleColor;
        private PatternLocation SelectionMax => sequencer.SelectionMax;
        private PatternLocation SelectionMin => sequencer.SelectionMin;
        private bool HasCaptureOperation => sequencer.HasCaptureOperation;
        private int HeaderSizeY   => sequencer.HeaderSizeY;
        private int HoverPattern  => sequencer.HoverPattern;
        private int SeekX         => sequencer.SeekX;
        private float BitmapScale => sequencer.BitmapScale;

        public delegate void PatternDelegate(int idx);
        public delegate void PointerDelegate(Control c, PointerEventArgs e);
        public delegate void EmptyDelegate();
        public event PointerDelegate ColumnSelect;
        public event PointerDelegate DragSeekBar;
        public event PatternDelegate HoverPatternChanged;
        public event PatternDelegate LoopPointToggled;
        public event PointerDelegate StartPanning; 
        public event EmptyDelegate CancelDragSeekBar;
        public event EmptyDelegate PatternModified;

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

        public SequencerTimeline(Sequencer sequencer)
        {
            Localization.Localize(this);
            this.sequencer = sequencer;
            supportsLongPress = true;
        }

        protected override void OnAddedToContainer()
        {
            var g = ParentWindow.Graphics;
            
            bmpLoopPoint = g.GetTextureAtlasRef("LoopSmallFill");

            tooltip = $"<MouseLeft> {SeekTooltip} - <MouseRight> {MoreOptionsTooltip} - <MouseRight><Drag> {SelectColumnTooltip}\n<L><MouseLeft> {SetLoopPointTooltip} - <MouseWheel><Drag> {PanTooltip}";

            seekGeometry = new float[]
            {
                -HeaderSizeY / 2, 1,
                0, HeaderSizeY - 2,
                HeaderSizeY / 2, 1 
            };
        }

        private int GetPixelForNote(int note, bool scroll = true)
        {
            return sequencer.GetPixelForNote(note, scroll);
        }

        private int GetNoteForPixel(int x, bool scroll = true)
        {
            return sequencer.GetNoteForPixel(x, scroll);
        }

        private bool IsValidTimeOnlySelection()
        {
            return sequencer.IsValidTimeOnlySelection();
        }

        private Color GetSeekBarColor()
        {
            return sequencer.GetSeekBarColor();
        }

        private int MaxVisiblePattern()
        {
            return sequencer.MaxVisiblePattern();
        }

        private int MinVisiblePattern()
        {
            return sequencer.MinVisiblePattern();
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

            var minPattern = patternIdx;
            var maxPattern = patternIdx;

            if (IsValidTimeOnlySelection())
            {
                minPattern = SelectionMin.PatternIndex;
                maxPattern = SelectionMax.PatternIndex;
            }

            var tempoProperties = new TempoProperties(dlg.Properties, song, patternIdx, minPattern, maxPattern);

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

        private void ShowContextMenu(PointerEventArgs e)
        {
            var patternIdx = GetPatternIndexForCoord(e.X);
            if (patternIdx >= 0)
            {
                var isLoopPoint = Song.LoopPoint == patternIdx;

                App.ShowContextMenuAsync(new[]
                {
                    new ContextMenuOption(isLoopPoint ? "MenuClearLoopPoint" :  "MenuLoopPoint", isLoopPoint ?  ClearLoopPointLabel : SetLoopPointLabel, () => { LoopPointToggled?.Invoke(patternIdx); } ),
                    new ContextMenuOption("MenuCustomPatternSettings", CustomPatternSettingsLabel, () => { EditPatternCustomSettings(new Point(e.X, e.Y), patternIdx); } )
                });
            }
        }

        private bool IsPointOnSeekBar(int x)
        {
            return Math.Abs(SeekX - x) < HeaderSizeY;
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

        protected override void OnTouchClick(PointerEventArgs e)
        {
            App.SeekSong(GetNoteForPixel(e.X));
        }

        protected override void OnTouchLongPress(PointerEventArgs e)
        {
            if (!e.IsDoubleTapLongPress)
            {
                CancelDragSeekBar?.Invoke();
                ShowContextMenu(e);
            }
        }

        protected override void OnPointerEnter(EventArgs e)
        {
            App.SetToolTip(tooltip);
        }

        protected override void OnPointerMove(PointerEventArgs e)
        {
            var x = GetPatternIndexForCoord(e.X);

            if (HoverPattern != x)
                HoverPatternChanged.Invoke(x);
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                OnTouchDown(e);
                return;
            }

            if (e.Middle)
            {
                StartPanning?.Invoke(this, e);
                return;
            }

            if (e.Left)
            {
                if (Settings.SetLoopPointShortcut.IsKeyDown(ParentWindow))
                {
                    var patternIdx = GetPatternIndexForCoord(e.X);
                    if (patternIdx >= 0)
                        LoopPointToggled?.Invoke(patternIdx);
                }
                else
                {
                    DragSeekBar.Invoke(this, e);
                }
            }
            else if (e.Right)
            {
                e.DelayRightClick(); // Can have selection or context menu, need to wait.
            }
        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            // If we have a capture operation, we likely had a delayed right click.
            if (e.Right && !HasCaptureOperation)
            {
                ShowContextMenu(e);
            }
        }

        protected override void OnPointerDownDelayed(PointerEventArgs e)
        {
            ColumnSelect?.Invoke(this, e);
        }

        protected override void OnRender(Graphics g)
        {
            var b = g.BackgroundCommandList;
            var c = g.DefaultCommandList;

            c.DrawLine(0, HeaderSizeY, width, HeaderSizeY, Theme.BlackColor);

            // Header Background
            for (int i = MinVisiblePattern(); i < MaxVisiblePattern(); i++)
            {
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                var sx = GetPixelForNote(Song.GetPatternLength(i), false);
                var color = i == HoverPattern ? Theme.MediumGreyColor1 : ((i & 1) == 0 ? Theme.DarkGreyColor4 : Theme.DarkGreyColor2);
                b.FillRectangle(px, 0, px + sx, HeaderSizeY, color);
            }

            // Header selection
            if (IsValidTimeOnlySelection())
            {
                c.FillRectangle(
                    GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Math.Min(SelectionMin.PatternIndex + 0, Song.Length))), 0,
                    GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(Math.Min(SelectionMax.PatternIndex + 1, Song.Length))), HeaderSizeY,
                    sequencer.IsActiveControl ? SelectedPatternVisibleColor : SelectedPatternInvisibleColor);
            }

            // Top line.
            c.DrawLine(0, 0, width, 0, Theme.BlackColor);
            //c.DrawLine(0, height - 1, width, height - 1, Theme.BlackColor);

            // Vertical lines 
            for (int i = Math.Max(1, MinVisiblePattern()); i <= MaxVisiblePattern(); i++)
            {
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                b.DrawLine(px, 0, px, HeaderSizeY, Theme.BlackColor);
            }

            // Pattern names.
            for (int i = MinVisiblePattern(); i < MaxVisiblePattern(); i++)
            {
                var patternLen = Song.GetPatternLength(i);
                var px = GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                var sx = GetPixelForNote(patternLen, false);

                if (sx > 0)
                {
                    c.PushTranslation(px, 0);

                    var text = (i + 1).ToString();
                    if (Song.PatternHasCustomSettings(i))
                        text += "*";
                    c.DrawText(text, Fonts.FontMedium, 0, barTextPosY, Theme.LightGreyColor1, TextFlags.Center | TextFlags.Clip, sx);

                    if (i == Song.LoopPoint)
                        c.DrawTextureAtlas(bmpLoopPoint, headerIconPosX, headerIconPosY, BitmapScale, Theme.LightGreyColor1);

                    c.PopTransform();
                }
            }

            // Seek bar
            c.PushTranslation(SeekX, 0);
            c.FillAndDrawGeometry(seekGeometry, GetSeekBarColor(), Theme.BlackColor, 1, true);
            c.PopTransform();
        }
    }
}