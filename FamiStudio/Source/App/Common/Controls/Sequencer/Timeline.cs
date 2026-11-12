using System;
using System.Collections.Generic;

namespace FamiStudio
{
    public partial class Sequencer : Container
    {
        private class Timeline : Container
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

            public delegate void PatternDelegate(int idx);
            public delegate void PointerDelegate(Control c, PointerEventArgs e);
            public delegate void EmptyDelegate();

            public event EmptyDelegate PatternModified;
            public event PointerDelegate ColumnSelect;
            public event PointerDelegate StartDragSeekBar;
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

            public Timeline(Sequencer seq)
            {   
                Localization.Localize(this);
                supportsLongPress = true;
                clipRegion = false;
                sequencer = seq;
            }

            protected override void OnAddedToContainer()
            {
                var g = ParentWindow.Graphics;
                
                bmpLoopPoint = g.GetTextureAtlasRef("LoopSmallFill");

                tooltip = $"<MouseLeft> {SeekTooltip} - <MouseRight> {MoreOptionsTooltip} - <MouseRight><Drag> {SelectColumnTooltip}\n<L><MouseLeft> {SetLoopPointTooltip} - <MouseWheel><Drag> {PanTooltip}";
            }

            public void UpdateLayout()
            {
                seekGeometry = new float[]
                {
                    -height / 2, 1,
                    0, height - 2,
                    height / 2, 1 
                };
            }

            private int GetPatternIndexForCoord(int x)
            {
                var noteIdx = sequencer.GetNoteForPixel(x);

                if (noteIdx < 0 || noteIdx >= Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                    return -1;

                return Song.PatternIndexFromAbsoluteNoteIndex(noteIdx);
            }

            private void SetLoopPoint(int patternIdx)
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Song, Song.Id);
                Song.SetLoopPoint(Song.LoopPoint == patternIdx ? -1 : patternIdx);
                App.UndoRedoManager.EndTransaction();
                MarkDirty();
            }

            private void EditPatternCustomSettings(Point pt, int patternIdx)
            {
                var dlg = new PropertyDialog(ParentWindow, CustomPatternTitle, new Point(left + pt.X, top + pt.Y), 300);
                var song = Song;
                var enabled = song.PatternHasCustomSettings(patternIdx);

                var patterns = new List<int> { patternIdx };

                if (sequencer.IsValidTimelineSelection())
                {
                    patterns = new List<int>(sequencer.selectedTimelineColumns);
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
                            App.Sequencer.LayoutChanged();
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
                        new ContextMenuOption(isLoopPoint ? "MenuClearLoopPoint" :  "MenuLoopPoint", isLoopPoint ?  ClearLoopPointLabel : SetLoopPointLabel, () => { SetLoopPoint(patternIdx); } ),
                        new ContextMenuOption("MenuCustomPatternSettings", CustomPatternSettingsLabel, () => { EditPatternCustomSettings(new Point(e.X, e.Y), patternIdx); } )
                    });
                }
            }

            private bool IsPointOnSeekBar(int x)
            {
                return Math.Abs(sequencer.SeekX - x) < height;
            }

            protected void OnTouchDown(PointerEventArgs e)
            {
                sequencer.SetFlingVelocity(0, 0);

                // See if we are close enough to the yellow triangle.
                if (IsPointOnSeekBar(e.X))
                {
                    StartDragSeekBar?.Invoke(this, e);
                    return;
                }

                //ColumnSelect?.Invoke(this, e);
            }

            protected override void OnTouchClick(PointerEventArgs e)
            {
                if (sequencer.captureOperation != null)
                    App.SeekSong(sequencer.GetNoteForPixel(e.X));
            }

            protected override void OnTouchLongPress(PointerEventArgs e)
            {
                if (!e.IsDoubleTapLongPress)
                {
                    if (sequencer.captureOperation == DragSeekBar)
                        sequencer.AbortCaptureOperation();

                    Platform.VibrateClick();
                    ColumnSelect?.Invoke(this, e);
                    //ShowContextMenu(e);
                }
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
                    if (Settings.SetLoopPointShortcut.IsKeyDown(ParentWindow))
                    {
                        var patternIdx = GetPatternIndexForCoord(e.X);
                        if (patternIdx >= 0)
                            SetLoopPoint(patternIdx);
                    }
                    else
                    {
                        StartDragSeekBar.Invoke(this, e);
                    }
                }
                else if (e.Right)
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

            protected override void OnPointerUp(PointerEventArgs e)
            {
                if ((e.Right || e.IsLongPress) && !sequencer.captureThresholdMet)
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

                // We want to clip the top of the pattern area so that timeline renders on top.
                // This is for selection highlight reasons so the outer rectangle won't drawn over.
                c.PushClipRegion(0, 0, width, height + 1);

                // Header Background
                for (int i = sequencer.MinVisiblePattern(); i < sequencer.MaxVisiblePattern(); i++)
                {
                    var px = sequencer.GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                    var sx = sequencer.GetPixelForNote(Song.GetPatternLength(i), false);
                    var color = i == sequencer.hoverPos.X ? Theme.MediumGreyColor1 : ((i & 1) == 0 ? Theme.DarkGreyColor4 : Theme.DarkGreyColor2);
                    b.FillRectangle(px, 0, px + sx, height, color);

                    // Header selection.
                    if (sequencer.IsTimelineColumnHighlighted(i))
                    {
                        c.FillRectangle(px, 0, px + sx, height, Color.FromArgb(48, Theme.WhiteColor));
                        c.DrawRectangle(px, 0, px + sx, height, Theme.WhiteColor, 3, true, true);
                    }
                }

                // Top and bottom lines.
                c.DrawLine(0, 0, width, 0, Theme.BlackColor);
                c.DrawLine(0, height, width, height, Theme.BlackColor);

                // Vertical lines 
                for (int i = Math.Max(1, sequencer.MinVisiblePattern()); i <= sequencer.MaxVisiblePattern(); i++)
                {
                    var px = sequencer.GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                    b.DrawLine(px, 0, px, height, Theme.BlackColor);
                }

                // Pattern names.
                for (int i = sequencer.MinVisiblePattern(); i < sequencer.MaxVisiblePattern(); i++)
                {
                    var patternLen = Song.GetPatternLength(i);
                    var px = sequencer.GetPixelForNote(Song.GetPatternStartAbsoluteNoteIndex(i));
                    var sx = sequencer.GetPixelForNote(patternLen, false);

                    if (sx > 0)
                    {
                        c.PushTranslation(px, 0);

                        var text = (i + 1).ToString();
                        if (Song.PatternHasCustomSettings(i))
                            text += "*";
                        c.DrawText(text, Fonts.FontMedium, 0, barTextPosY, sequencer.IsTimelineColumnHighlighted(i) ? Theme.WhiteColor : Theme.LightGreyColor1, TextFlags.Center | TextFlags.Clip, sx);

                        if (i == Song.LoopPoint)
                            c.DrawTextureAtlas(bmpLoopPoint, headerIconPosX, headerIconPosY, sequencer.bitmapScale, Theme.LightGreyColor1);

                        c.PopTransform();
                    }
                }

                // Seek bar
                c.PushTranslation(sequencer.SeekX, 0);
                c.FillAndDrawGeometry(seekGeometry, sequencer.GetSeekBarColor(), Theme.BlackColor, 1, true);
                c.PopTransform();
                c.PopClipRegion();
            }
        }
    }
}