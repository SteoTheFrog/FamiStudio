using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FamiStudio
{
    public class EffectPanel : Container
    {
        public enum PanelMode
        {
            None,
            Notes,
            Wave
        }

        private PianoRoll pianoRoll;
        private PanelMode panelMode = PanelMode.None;

        private Button fullScreenButton;
        private Button showEffectPanelButton;
        private Button snapModeButton;

        private int hoverEffectIndex = -1;

        LocalizedString RelativeEffectScalingLabel;

        private Song Song => App?.SelectedSong;

        public EffectPanel(PianoRoll pianoRoll)
        {
            this.pianoRoll = pianoRoll;
            Localization.Localize(this);
            supportsDoubleClick = true;
            SetupClipRegion(false);
        }

        protected override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            fullScreenButton = new Button("Maximize") { Transparent = true };
            fullScreenButton.ImageEvent += FullScreenButton_ImageEvent;
            fullScreenButton.Click      += (s) => pianoRoll.ToggleMaximize();

            showEffectPanelButton = new Button("CollapsedSmall") { Transparent = true };
            showEffectPanelButton.ImageEvent += ShowEffectPanelButton_ImageEvent;
            showEffectPanelButton.Click      += (s) => pianoRoll.ToggleEffectPanel();

            snapModeButton = new Button("Snap") { Transparent = true, Font = Fonts.FontSmall };
            snapModeButton.ImageEvent += SnapModeButton_ImageEvent;
            snapModeButton.TextEvent  += SnapModeButton_TextEvent;
            snapModeButton.Click      += (s) => pianoRoll.ToggleSnap();
            snapModeButton.RightClick += (s) => pianoRoll.ShowSnapResolutionContextMenu();
            snapModeButton.TextAlign  = Button.TextPosition.Left;

            AddControl(fullScreenButton);
            AddControl(showEffectPanelButton);
            AddControl(snapModeButton);
        }

        private string FullScreenButton_ImageEvent(Control sender, ref Color tint)
        {
            tint = pianoRoll.IsMaximized ? Theme.LightGreyColor1 : Theme.MediumGreyColor1;
            return "Maximize";
        }

        private string ShowEffectPanelButton_ImageEvent(Control sender, ref Color tint)
        {
            tint = Theme.LightGreyColor1;
            return pianoRoll.ShowEffectsPanel ? "ExpandedSmall" : "CollapsedSmall";
        }

        private string SnapModeButton_ImageEvent(Control sender, ref Color tint)
        {
            tint = App.IsRecording ? Theme.DarkRedColor : (pianoRoll.SnapEnabled ? Theme.LightGreyColor1 : Theme.MediumGreyColor1);
            return pianoRoll.SnapEnabled || App.IsRecording ? "Snap" : "SnapOff";
        }

        private string SnapModeButton_TextEvent(Control sender)
        {
            return SnapResolutionType.Names[pianoRoll.SnapResolution];
        }

        private void UpdateButtonLayoutAndVisibility()
        {
            var isChannelEffects = panelMode == PanelMode.Notes && !pianoRoll.HasRepeatEnvelope();

            fullScreenButton.Visible = pianoRoll.PianoVisible && !Platform.IsMobile && !pianoRoll.IsVideoRecording;
            if (fullScreenButton.Visible)
            {
                var r = pianoRoll.GetMaximizeButtonRect();
                fullScreenButton.Move(r.X, r.Y);
                fullScreenButton.Resize(r.Width, r.Height);
            }

            showEffectPanelButton.Visible = pianoRoll.PianoVisible && panelMode != PanelMode.None && !(panelMode == PanelMode.Wave && Platform.IsMobile);
            if (showEffectPanelButton.Visible)
            {
                var r = pianoRoll.GetToggleEffectPanelButtonRect();
                showEffectPanelButton.ImageScale = pianoRoll.BitmapScale;
                showEffectPanelButton.Move(r.X, r.Y);
                showEffectPanelButton.Resize(r.Width, r.Height);
            }

            snapModeButton.Visible = pianoRoll.PianoVisible && isChannelEffects && pianoRoll.SnapAllowed && !Platform.IsMobile;
            if (snapModeButton.Visible)
            {
                var snapRect = pianoRoll.GetSnapButtonRect();
                var resRect  = pianoRoll.GetSnapResolutionRect();
                snapModeButton.Move(resRect.X, snapRect.Y);
                snapModeButton.Resize(snapRect.Right - resRect.X, snapRect.Height);
            }
        }

        public void SetPanelMode(PanelMode mode)
        {
            if (panelMode != mode)
            {
                panelMode = mode;
                MarkDirty();
            }
        }

        public override bool HitTest(int winX, int winY)
        {
            if (!base.HitTest(winX, winY))
                return false;

            var p = WindowToControl(winX, winY);
            if (p.X > pianoRoll.PianoSizeX && p.Y < pianoRoll.HeaderSizeY)
                return false;

            return true;
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            base.OnPointerDown(e);

            var middle = e.Middle || (e.Left && ModifierKeys.IsAltDown && Settings.AltLeftForMiddle);
            if (middle && e.Y > pianoRoll.HeaderSizeY && e.X > pianoRoll.PianoSizeX)
            {
                pianoRoll.StartPan(e.X, e.Y);
                return;
            }

            if (e.IsTouchEvent)
            {
                if (pianoRoll.IsPointInTopLeftCorner(e.X, e.Y))
                {
                    pianoRoll.ToggleEffectPanel();
                    return;
                }

                var isChannelEffectsTouch = panelMode == PanelMode.Notes && !pianoRoll.HasRepeatEnvelope();

                if (isChannelEffectsTouch && pianoRoll.HandleTouchDownNoteEffectsGizmos(e.X, e.Y))
                    return;

                if (panelMode == PanelMode.Notes && pianoRoll.HasRepeatEnvelope() && pianoRoll.HandleTouchDownEnvelopeEffectsGizmos(e.X, e.Y))
                    return;

                if (panelMode == PanelMode.Wave && pianoRoll.HandleTouchDownDPCMVolumeEnvelope(e.X, e.Y))
                    return;

                pianoRoll.StartMobilePan(e.X, e.Y);
                return;
            }

            if (panelMode == PanelMode.Wave && pianoRoll.HandleMouseDownDPCMVolumeEnvelope(e))
                return;

            var isChannelEffects = panelMode == PanelMode.Notes && !pianoRoll.HasRepeatEnvelope();

            if (isChannelEffects && e.Left)
            {
                var effectIdx = pianoRoll.GetEffectIndexForPosition(e.X, e.Y, pianoRoll.SupportedEffects.Length);
                if (effectIdx >= 0)
                {
                    pianoRoll.SelectedEffectIdx = pianoRoll.SupportedEffects[effectIdx];
                    MarkDirty();
                    return;
                }
            }

            if (isChannelEffects && pianoRoll.SelectedEffectIdx >= 0 && pianoRoll.IsPointInEffectPanel(e.X, e.Y) &&
                pianoRoll.GetEffectNoteForCoord(e.X, e.Y, out var location))
            {
                if (e.Left)
                {
                    var slide = Settings.SlideNoteShortcut.IsKeyDown(ParentWindow);

                    if (slide && pianoRoll.SelectedEffectIdx == Note.EffectVolume)
                        pianoRoll.StartDragVolumeSlide(e.X, e.Y, location);
                    else if (ModifierKeys.IsShiftDown)
                        pianoRoll.ClearEffectValue(location);
                    else
                        pianoRoll.StartChangeEffectValue(e.X, e.Y, location);

                    return;
                }

                if (e.Right)
                {
                    e.DelayRightClick(); // Wait to see if its a context menu or selection.
                    return;
                }
            }

        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            base.OnPointerUp(e);

            if (e.Right && panelMode == PanelMode.Notes && !pianoRoll.HasRepeatEnvelope() && !pianoRoll.IsCapturingPointer)
            {
                pianoRoll.HandleContextMenuEffectPanel(e.X, e.Y);
            }
        }

        protected override void OnPointerDownDelayed(PointerEventArgs e)
        {
            base.OnPointerDownDelayed(e);

            if (e.Right && panelMode == PanelMode.Notes && !pianoRoll.HasRepeatEnvelope() &&
                pianoRoll.SelectedEffectIdx >= 0 && pianoRoll.IsPointInEffectPanel(e.X, e.Y) &&
                pianoRoll.GetEffectNoteForCoord(e.X, e.Y, out _))
            {
                pianoRoll.StartSelection(e.X, e.Y);
            }
        }

        protected override void OnMouseDoubleClick(PointerEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (e.Left && panelMode == PanelMode.Notes && !pianoRoll.HasRepeatEnvelope() &&
                pianoRoll.SelectedEffectIdx >= 0 && pianoRoll.IsPointInEffectPanel(e.X, e.Y) &&
                pianoRoll.GetEffectNoteForCoord(e.X, e.Y, out var location))
            {
                pianoRoll.ClearEffectValue(location);
            }
        }

        protected override void OnTouchClick(PointerEventArgs e)
        {
            base.OnTouchClick(e);

            if (panelMode == PanelMode.Notes && !pianoRoll.HasRepeatEnvelope())
                pianoRoll.HandleTouchClickEffectPanel(e.X, e.Y);
            else if (panelMode == PanelMode.Notes)
                pianoRoll.HandleTouchClickEnvelopeEffectPanel(e.X, e.Y);
        }

        public void UpdateLayout()
        {
            Move(0, 0, pianoRoll.Width, pianoRoll.HeaderAndEffectSizeY);
        }

        private void RenderEffectList(CommandList c)
        {
            var pianoSizeX = pianoRoll.PianoSizeX;
            var headerSizeY = pianoRoll.HeaderSizeY;
            var headerAndEffectSizeY = pianoRoll.HeaderAndEffectSizeY;

            c.FillRectangle(0, 0, pianoSizeX, headerAndEffectSizeY, Theme.DarkGreyColor4);
            c.DrawLine(pianoSizeX - 1, 0, pianoSizeX - 1, headerAndEffectSizeY, Theme.BlackColor);

            if (Platform.IsDesktop && pianoRoll.IsMaximized)
                c.DrawLine(0, 0, pianoSizeX, 0, Color.Black);

            // Effect icons
            if (panelMode == PanelMode.Notes && !pianoRoll.HasRepeatEnvelope())
            {
                if (pianoRoll.ShowEffectsPanel)
                {
                    c.PushTranslation(0, headerSizeY);

                    int effectButtonY = 0;
                    var supportedEffects = pianoRoll.SupportedEffects;

                    for (int i = 0; i < supportedEffects.Length; i++)
                    {
                        var effectIdx = supportedEffects[i];

                        if (Platform.IsMobile && effectIdx != pianoRoll.SelectedEffectIdx)
                            continue;

                        c.PushTranslation(0, effectButtonY);
                        if (hoverEffectIndex == i)
                            c.FillRectangle(0, 0, pianoSizeX, pianoRoll.EffectButtonSizeY, Theme.MediumGreyColor1);
                        c.DrawLine(0, -1, pianoSizeX, -1, Theme.BlackColor);
                        c.DrawTextureAtlas(pianoRoll.BmpEffects[effectIdx], pianoRoll.EffectIconPosX, pianoRoll.EffectIconPosY, pianoRoll.EffectBitmapScale, Theme.LightGreyColor1);
                        c.DrawText(EffectType.LocalizedNames[effectIdx], pianoRoll.SelectedEffectIdx == effectIdx ? Fonts.FontSmallBold : Fonts.FontSmall, pianoRoll.EffectNamePosX, 0, Theme.LightGreyColor2, TextFlags.Middle, 0, pianoRoll.EffectButtonSizeY);
                        c.PopTransform();

                        effectButtonY += pianoRoll.EffectButtonSizeY;
                    }

                    c.PushTranslation(0, effectButtonY);
                    c.DrawLine(0, -1, pianoSizeX, -1, Theme.BlackColor);
                    c.PopTransform();
                    c.PopTransform();
                }
            }
            else if (panelMode == PanelMode.Wave || (panelMode == PanelMode.Notes && pianoRoll.HasRepeatEnvelope()))
            {
                if (pianoRoll.ShowEffectsPanel)
                {
                    c.PushTranslation(0, headerSizeY);
                    c.DrawLine(0, -1, pianoSizeX, -1, Theme.BlackColor);

                    var bmp  = panelMode == PanelMode.Wave ? pianoRoll.BmpEffects[Note.EffectVolume] : pianoRoll.BmpEffectRepeat;
                    var text = panelMode == PanelMode.Wave ? EffectType.LocalizedNames[Note.EffectVolume] : EnvelopeType.LocalizedNames[EnvelopeType.WaveformRepeat];

                    c.DrawTextureAtlas(bmp, pianoRoll.EffectIconPosX, pianoRoll.EffectIconPosY, pianoRoll.EffectBitmapScale, Theme.LightGreyColor1);
                    c.DrawText(text, Fonts.FontSmallBold, pianoRoll.EffectNamePosX, 0, Theme.LightGreyColor2, TextFlags.Middle, 0, pianoRoll.EffectButtonSizeY);
                    c.PushTranslation(0, pianoRoll.EffectButtonSizeY);
                    c.DrawLine(0, -1, pianoSizeX, -1, Theme.BlackColor);
                    c.PopTransform();
                    c.PopTransform();
                }
            }

            c.DrawLine(0, headerAndEffectSizeY - 1, pianoSizeX, headerAndEffectSizeY - 1, Theme.BlackColor);
        }

        private void RenderChannelEffectPanel(CommandList c, int effectPanelSizeY, int minVisiblePattern, int maxVisiblePattern)
        {
            var song = Song;
            var channel = song.Channels[pianoRoll.EditChannel];
            var selectedEffectIdx = pianoRoll.SelectedEffectIdx;

            var minLocation = new NoteLocation(minVisiblePattern, 0);
            var maxLocation = new NoteLocation(maxVisiblePattern, 0);

            var singleFrameSlides = new HashSet<NoteLocation>();

            // Draw the effects current value rectangles. Not all effects need this.
            if (selectedEffectIdx >= 0 && Note.EffectWantsPreviousValue(selectedEffectIdx))
            {
                var lastFrame = -1;
                var lastValue = channel.GetCachedLastValidEffectValue(minVisiblePattern - 1, selectedEffectIdx, out var lastValueLocation);
                var minValue = Note.GetEffectMinValue(song, channel, selectedEffectIdx);
                var maxValue = Note.GetEffectMaxValue(song, channel, selectedEffectIdx);
                var exp = pianoRoll.GetEffectValueExponent(maxValue);

                // Special case for volume, since it can have slides.
                if (selectedEffectIdx == Note.EffectVolume)
                {
                    var lastSlide = channel.GetCachedLastValidEffectValue(minVisiblePattern - 1, Note.EffectVolumeSlide, out var lastSlideLocation);

                    // If the last slide is before the last volume, ignore.
                    if (lastSlideLocation.IsValid && lastSlideLocation < lastValueLocation || !lastSlideLocation.IsValid)
                        lastSlide = -1;

                    var lastSlideDuration = lastSlide >= 0 ? channel.GetVolumeSlideDuration(lastSlideLocation) : -1;

                    lastFrame = lastValueLocation.IsValid ? lastValueLocation.ToAbsoluteNoteIndex(song) : -1;

                    var filter = Note.GetFilterForEffect(Note.EffectVolume) | Note.GetFilterForEffect(Note.EffectVolumeSlide);

                    for (var it = channel.GetSparseNoteIterator(minLocation, maxLocation, filter); !it.Done; it.Next())
                    {
                        var note = it.Note;
                        var location = it.Location;

                        Debug.Assert(note.HasVolume || note.HasVolumeSlide);

                        if (note.HasValidEffectValue(Note.EffectVolume))
                        {
                            c.PushTranslation(pianoRoll.GetPixelXForAbsoluteNoteIndex(location.ToAbsoluteNoteIndex(song)), 0);

                            var frame = location.ToAbsoluteNoteIndex(song);

                            if (lastSlide >= 0)
                            {
                                var X0 = pianoRoll.GetPixelXForAbsoluteNoteIndex(lastFrame < 0 ? -1000000 : lastFrame - frame, false);
                                var X1 = pianoRoll.GetPixelXForAbsoluteNoteIndex(-frame + lastFrame + lastSlideDuration, false);
                                var sizeY0 = pianoRoll.GetPixelYForEffectValue(lastValue, 0, Note.VolumeMax);
                                var sizeY1 = pianoRoll.GetPixelYForEffectValue(lastSlide, 0, Note.VolumeMax);

                                var points = new float[4 * 2]
                                {
                                    X0, effectPanelSizeY - sizeY0,
                                    X0, effectPanelSizeY,
                                    X1, effectPanelSizeY,
                                    X1, effectPanelSizeY - sizeY1
                                };

                                c.FillGeometry(points, Theme.DarkGreyColor5);

                                if ((frame - lastFrame) == 1 && lastSlide < lastValue)
                                    singleFrameSlides.Add(NoteLocation.FromAbsoluteNoteIndex(song, lastFrame));

                                if ((frame - lastFrame) > lastSlideDuration)
                                    c.FillRectangle(X1, effectPanelSizeY - sizeY1, 0, effectPanelSizeY, Theme.DarkGreyColor5);
                            }
                            else
                            {
                                var sizeY = pianoRoll.GetPixelYForEffectValue(lastValue, 0, Note.VolumeMax);
                                c.FillRectangle(pianoRoll.GetPixelXForAbsoluteNoteIndex(lastFrame < 0 ? -1000000 : lastFrame - frame, false), effectPanelSizeY - sizeY, 0, effectPanelSizeY, Theme.DarkGreyColor5);
                            }

                            lastSlide = note.HasVolumeSlide ? note.VolumeSlideTarget : -1;
                            lastValue = note.Volume;
                            lastFrame = frame;

                            if (lastSlide >= 0)
                                lastSlideDuration = channel.GetVolumeSlideDuration(location);

                            c.PopTransform();
                        }
                    }

                    c.PushTranslation(pianoRoll.GetPixelXForAbsoluteNoteIndex(Math.Max(lastFrame, 0)), 0);

                    if (lastSlide >= 0)
                    {
                        var location = NoteLocation.FromAbsoluteNoteIndex(song, lastFrame);

                        var X0 = 0;
                        var X1 = pianoRoll.GetPixelXForAbsoluteNoteIndex(lastSlideDuration, false);
                        var sizeY0 = pianoRoll.GetPixelYForEffectValue(lastValue, 0, Note.VolumeMax);
                        var sizeY1 = pianoRoll.GetPixelYForEffectValue(lastSlide, 0, Note.VolumeMax);

                        var points = new float[4 * 2]
                        {
                            X0, effectPanelSizeY - sizeY0,
                            X0, effectPanelSizeY,
                            X1, effectPanelSizeY,
                            X1, effectPanelSizeY - sizeY1
                        };

                        c.FillGeometry(points, Theme.DarkGreyColor5);

                        if (lastSlideDuration == 1 && lastSlide < lastValue)
                            singleFrameSlides.Add(location);

                        var endLocation = location.Advance(song, lastSlideDuration);
                        if (endLocation.IsInSong(song))
                        {
                            var lastNote = channel.GetNoteAt(endLocation);
                            if (lastNote == null || !lastNote.HasVolume)
                                c.FillRectangle(X1, effectPanelSizeY - sizeY1, pianoRoll.GetPixelXForAbsoluteNoteIndex(1000000, false), effectPanelSizeY, Theme.DarkGreyColor5);
                        }
                    }
                    else
                    {
                        var lastSizeY = pianoRoll.GetPixelYForEffectValue(lastValue, 0, Note.VolumeMax);
                        c.FillRectangle(0, effectPanelSizeY - lastSizeY, pianoRoll.GetPixelXForAbsoluteNoteIndex(1000000, false), effectPanelSizeY, Theme.DarkGreyColor5);
                    }

                    c.PopTransform();
                }
                else
                {
                    for (var it = channel.GetSparseNoteIterator(minLocation, maxLocation, Note.GetFilterForEffect(selectedEffectIdx)); !it.Done; it.Next())
                    {
                        var note = it.Note;
                        var location = it.Location;

                        if (note.HasValidEffectValue(selectedEffectIdx))
                        {
                            c.PushTranslation(pianoRoll.GetPixelXForAbsoluteNoteIndex(location.ToAbsoluteNoteIndex(song)), 0);

                            var frame = location.ToAbsoluteNoteIndex(song);
                            var sizeY = pianoRoll.GetPixelYForEffectValue(lastValue, minValue, maxValue, exp);
                            c.FillRectangle(pianoRoll.GetPixelXForAbsoluteNoteIndex(lastFrame < 0 ? -1000000 : lastFrame - frame, false), effectPanelSizeY - sizeY, 0, effectPanelSizeY, Theme.DarkGreyColor5);
                            lastValue = note.GetEffectValue(selectedEffectIdx);
                            lastFrame = frame;

                            c.PopTransform();
                        }
                    }

                    var lastSizeY = pianoRoll.GetPixelYForEffectValue(lastValue, minValue, maxValue, exp);
                    c.PushTranslation(Math.Max(0, pianoRoll.GetPixelXForAbsoluteNoteIndex(lastFrame)), 0);
                    c.FillRectangle(0, effectPanelSizeY - lastSizeY, Width, effectPanelSizeY, Theme.DarkGreyColor5);
                    c.PopTransform();
                }
            }

            pianoRoll.DrawSelectionRect(c, effectPanelSizeY, true);

            var highlightLocation = NoteLocation.Invalid;

            if (Platform.IsMobile || pianoRoll.HighlightNoteAbsoluteIndex >= 0 && pianoRoll.IsChangingEffectValue)
            {
                highlightLocation = NoteLocation.FromAbsoluteNoteIndex(song, pianoRoll.SnapEnabled && pianoRoll.SnapEffectEnabled ? pianoRoll.SnapNote(pianoRoll.HighlightNoteAbsoluteIndex) : pianoRoll.HighlightNoteAbsoluteIndex);
            }
            else if (Platform.IsDesktop && !pianoRoll.IsCapturingPointer)
            {
                var pt = ScreenToControl(CursorPosition);
                pianoRoll.GetEffectNoteForCoord(pt.X, pt.Y, out highlightLocation);
            }

            // Draw the actual effect bars.
            for (var it = channel.GetSparseNoteIterator(minLocation, maxLocation, NoteFilter.All); !it.Done; it.Next())
            {
                var note = it.Note;
                var location = it.Location;

                if (selectedEffectIdx >= 0 && note.HasValidEffectValue(selectedEffectIdx))
                {
                    var effectValue = note.GetEffectValue(selectedEffectIdx);
                    var minValue = Note.GetEffectMinValue(song, channel, selectedEffectIdx);
                    var maxValue = Note.GetEffectMaxValue(song, channel, selectedEffectIdx);
                    var exp = pianoRoll.GetEffectValueExponent(maxValue);
                    var sizeY = pianoRoll.GetPixelYForEffectValue(effectValue, minValue, maxValue, exp);
                    var noteSizeX = pianoRoll.NoteSizeX;

                    c.PushTranslation(pianoRoll.GetPixelXForAbsoluteNoteIndex(location.ToAbsoluteNoteIndex(song)), 0);

                    if (!Note.EffectWantsPreviousValue(selectedEffectIdx))
                        c.FillRectangle(0, 0, noteSizeX, effectPanelSizeY, Theme.DarkGreyColor5);

                    var highlighted = location == highlightLocation;
                    var selected = pianoRoll.IsEffectFrameSelected(location.ToAbsoluteNoteIndex(song));
                    var barColor = singleFrameSlides.Contains(location) ? pianoRoll.VolumeSlideBarFillColor : Theme.LightGreyColor1;

                    if (selected && !pianoRoll.LegacySelectMode)
                        barColor = Theme.WhiteColor;

                    c.FillRectangle(0, effectPanelSizeY - sizeY, noteSizeX, effectPanelSizeY, barColor);

                    if (highlighted || selected)
                    {
                        var outlineColor = highlighted || (selected && !pianoRoll.LegacySelectMode) ? Theme.WhiteColor : Theme.BlackColor;
                        c.DrawRectangle(0, effectPanelSizeY - sizeY, noteSizeX, effectPanelSizeY, outlineColor, 3, true, true);
                    }
                    else
                    {
                        c.DrawRectangle(0, effectPanelSizeY - sizeY, noteSizeX, effectPanelSizeY, Theme.BlackColor);
                    }

                    var text = effectValue.ToString();
                    if (text.Length * pianoRoll.FontSmallCharSizeX + 2 < noteSizeX)
                    {
                        if (sizeY < effectPanelSizeY / 2)
                            c.DrawText(text, Fonts.FontSmall, 0, effectPanelSizeY - sizeY - pianoRoll.EffectValuePosTextOffsetY, Theme.LightGreyColor1, TextFlags.Center, noteSizeX);
                        else
                            c.DrawText(text, Fonts.FontSmall, 0, effectPanelSizeY - sizeY + pianoRoll.EffectValueNegTextOffsetY, Theme.BlackColor, TextFlags.Center, noteSizeX);
                    }

                    c.PopTransform();
                }
            }

            // Thick vertical bars
            for (int p = minVisiblePattern; p < maxVisiblePattern; p++)
            {
                int x = pianoRoll.GetPixelXForAbsoluteNoteIndex(Song.GetPatternStartAbsoluteNoteIndex(p));
                if (p != 0) c.DrawLine(x, 0, x, Height, Theme.BlackColor, 3);
            }

            int maxX = pianoRoll.GetPixelXForAbsoluteNoteIndex(Song.GetPatternStartAbsoluteNoteIndex(maxVisiblePattern));
            c.DrawLine(maxX, 0, maxX, Height, Theme.BlackColor, 3);

            int seekX = pianoRoll.GetPixelXForAbsoluteNoteIndex((int)pianoRoll.GetSeekFrameToDraw());
            c.DrawLine(seekX, 0, seekX, effectPanelSizeY, pianoRoll.SeekBarColor, 3);

            var gizmos = pianoRoll.GetEffectGizmos(out _, out _);
            if (gizmos != null)
            {
                foreach (var g in gizmos)
                {
                    var lineColor = pianoRoll.IsGizmoHighlighted(g, pianoRoll.HeaderSizeY) ? Color.White : Color.Black;

                    if (g.FillImage != null)
                        c.DrawTextureAtlas(g.FillImage, g.Rect.X, g.Rect.Y, g.Rect.Width / (float)g.Image.ElementSize.Width, Theme.LightGreyColor1);
                    c.DrawTextureAtlas(g.Image, g.Rect.X, g.Rect.Y, g.Rect.Width / (float)g.Image.ElementSize.Width, lineColor);
                }
            }

            if (pianoRoll.RelativeEffectScaling && pianoRoll.IsSelectionValid())
            {
                c.DrawText(RelativeEffectScalingLabel, Fonts.FontSmall, pianoRoll.EffectPanelTextPosX, pianoRoll.EffectPanelTextPosY, Theme.LightRedColor);
            }
        }

        private void RenderEnvelopeRepeatPanel(CommandList c, int effectPanelSizeY)
        {
            var env = pianoRoll.EditEnvelope;
            var rep = pianoRoll.EditRepeatEnvelope;
            var editInstrument = pianoRoll.EditInstrument;

            if (pianoRoll.IsSelectionValid())
            {
                c.FillRectangle(
                    pianoRoll.GetPixelXForAbsoluteNoteIndex(pianoRoll.SelectionMinX + 0) + 1, 0,
                    pianoRoll.GetPixelXForAbsoluteNoteIndex(pianoRoll.SelectionMaxX + 1), Height, pianoRoll.IsActiveControl ? pianoRoll.SelectionBgVisibleColor : pianoRoll.SelectionBgInvisibleColor);
            }

            var highlightIndex = -1;

            if ((Platform.IsMobile && pianoRoll.HighlightRepeatEnvelope) || pianoRoll.IsChangingEnvelopeRepeatValue)
            {
                highlightIndex = pianoRoll.HighlightNoteAbsoluteIndex;
            }
            else if (Platform.IsDesktop && !pianoRoll.IsCapturingPointer)
            {
                var pt = ScreenToControl(CursorPosition);
                if (pianoRoll.IsPointInEffectPanel(pt.X, pt.Y))
                {
                    pianoRoll.GetEnvelopeValueForCoord(pt.X, pt.Y, out highlightIndex, out _);
                    if (highlightIndex >= 0)
                        highlightIndex /= env.ChunkLength;
                }
            }

            Debug.Assert(env.Length % rep.Length == 0);
            Debug.Assert(env.ChunkCount == rep.Length);

            var ratio = env.Length / rep.Length;

            Envelope.GetMinMaxValueForType(editInstrument, EnvelopeType.WaveformRepeat, out var minRepeat, out var maxRepeat);

            for (var i = 0; i < rep.Length; i++)
            {
                var x0 = pianoRoll.GetPixelXForAbsoluteNoteIndex((i + 0) * ratio);
                var x1 = pianoRoll.GetPixelXForAbsoluteNoteIndex((i + 1) * ratio);
                var sizeX = x1 - x0;
                var val = rep.Values[i];
                var sizeY = pianoRoll.GetPixelYForEffectValue(val, minRepeat, maxRepeat);

                c.PushTranslation(x0, 0);

                var selected = pianoRoll.IsEnvelopeRepeatValueSelected(i);
                var highlighted = i == highlightIndex;

                c.FillRectangle(0, effectPanelSizeY - sizeY, sizeX, effectPanelSizeY, editInstrument.Color);

                if (highlighted || selected)
                    c.DrawRectangle(0, effectPanelSizeY - sizeY, sizeX, effectPanelSizeY, selected ? Theme.LightGreyColor1 : Theme.WhiteColor, 3, true, true);
                else
                    c.DrawRectangle(0, effectPanelSizeY - sizeY, sizeX, effectPanelSizeY, Theme.BlackColor);

                var text = val.ToString();
                if (text.Length * pianoRoll.FontSmallCharSizeX + 2 < sizeX)
                {
                    if (sizeY < effectPanelSizeY / 2)
                        c.DrawText(text, Fonts.FontSmall, 0, effectPanelSizeY - sizeY - pianoRoll.EffectValuePosTextOffsetY, Theme.LightGreyColor1, TextFlags.Center, sizeX);
                    else
                        c.DrawText(text, Fonts.FontSmall, 0, effectPanelSizeY - sizeY + pianoRoll.EffectValueNegTextOffsetY, Theme.BlackColor, TextFlags.Center, sizeX);
                }

                c.PopTransform();
                c.DrawLine(x1, 0, x1, effectPanelSizeY, Theme.BlackColor, 3);
            }

            var gizmos = pianoRoll.GetEnvelopeEffectsGizmos();
            if (gizmos != null)
            {
                foreach (var g in gizmos)
                {
                    if (g.FillImage != null)
                        c.DrawTextureAtlas(g.FillImage, g.Rect.X, g.Rect.Y, g.Rect.Width / (float)g.Image.ElementSize.Width, editInstrument.Color);
                    c.DrawTextureAtlas(g.Image, g.Rect.X, g.Rect.Y, g.Rect.Width / (float)g.Image.ElementSize.Width, Color.White);
                }
            }

            var seekFrame = App.GetEnvelopeFrame(editInstrument, pianoRoll.EditArpeggio, pianoRoll.EditEnvelopeType);
            if (seekFrame >= 0)
            {
                var seekX = pianoRoll.GetPixelXForAbsoluteNoteIndex(seekFrame);
                c.DrawLine(seekX, 0, seekX, Height, pianoRoll.SeekBarColor, 3);
            }
        }

        private void RenderWaveVolumeEnvelopePanel(CommandList c, int effectPanelSizeY)
        {
            var waveEditor = pianoRoll.WaveEditor;
            var editSample = pianoRoll.EditSample;

            var halfPanelSizeY = effectPanelSizeY * 0.5f;
            var envelopePoints = new PointF[4];

            // Volume envelope
            for (int i = 0; i < 4; i++)
            {
                var x = pianoRoll.GetPixelForWaveTime(editSample.VolumeEnvelope[i + 0].sample / editSample.SourceSampleRate, pianoRoll.ViewScrollX);
                var y = halfPanelSizeY + (editSample.VolumeEnvelope[i + 0].volume - 1.0f) * -(halfPanelSizeY - waveEditor.WaveDisplayPaddingY);

                envelopePoints[i] = new PointF(x, y);
            }

            // Filled part.
            for (int i = 0; i < 3; i++)
            {
                var points = new float[4 * 2]
                {
                    envelopePoints[i + 1].X, envelopePoints[i + 1].Y,
                    envelopePoints[i + 0].X, envelopePoints[i + 0].Y,
                    envelopePoints[i + 0].X, effectPanelSizeY,
                    envelopePoints[i + 1].X, effectPanelSizeY
                };

                c.FillGeometry(points, Theme.DarkGreyColor4);
            }

            // Horizontal center line
            c.DrawLine(0, halfPanelSizeY, Width, halfPanelSizeY, Theme.BlackColor);

            // Top/bottom dash lines (limits);
            var topY    = waveEditor.WaveDisplayPaddingY;
            var bottomY = effectPanelSizeY - waveEditor.WaveDisplayPaddingY;
            c.DrawLine(0, topY,    Width, topY, Theme.DarkGreyColor1, 1, false, true);
            c.DrawLine(0, bottomY, Width, bottomY, Theme.DarkGreyColor1, 1, false, true);

            // Envelope line
            for (int i = 0; i < 3; i++)
            {
                c.DrawLine(
                    envelopePoints[i + 0].X,
                    envelopePoints[i + 0].Y,
                    envelopePoints[i + 1].X,
                    envelopePoints[i + 1].Y,
                    Theme.LightGreyColor1, 1, true);
            }

            // Envelope vertices.
            for (int i = 0; i < 4; i++)
            {
                c.PushTransform(
                    envelopePoints[i + 0].X,
                    envelopePoints[i + 0].Y,
                    1.0f, 1.0f);
                c.FillGeometry(waveEditor.SampleGeometry, Theme.LightGreyColor1);
                c.PopTransform();
            }

            // Selection rectangle
            if (pianoRoll.IsSelectionValid())
            {
                c.FillRectangle(
                    pianoRoll.GetPixelForWaveTime(pianoRoll.GetWaveTimeForSample(pianoRoll.SelectionMinX, true),  pianoRoll.ViewScrollX), 0,
                    pianoRoll.GetPixelForWaveTime(pianoRoll.GetWaveTimeForSample(pianoRoll.SelectionMaxX, false), pianoRoll.ViewScrollX), Height, pianoRoll.SelectionBgVisibleColor);
            }
        }

        private void RenderEffectPanel(CommandList c)
        {
            if (panelMode == PanelMode.None || !pianoRoll.ShowEffectsPanel)
                return;

            var pianoSizeX = pianoRoll.PianoSizeX;
            var headerSizeY = pianoRoll.HeaderSizeY;
            var scrollBarThickness = pianoRoll.ScrollBarThickness;
            var effectPanelSizeY = pianoRoll.EffectPanelSizeY;

            c.PushTranslation(pianoSizeX, headerSizeY);
            c.PushClipRegion(0, 0, Width - pianoSizeX - scrollBarThickness, effectPanelSizeY);

            if (panelMode == PanelMode.Notes && !pianoRoll.HasRepeatEnvelope())
            {
                var minVisibleNoteIdx = Math.Max(pianoRoll.GetAbsoluteNoteIndexForPixelX(0), 0);
                var maxVisibleNoteIdx = Math.Min(pianoRoll.GetAbsoluteNoteIndexForPixelX(Width) + 1, Song.GetPatternStartAbsoluteNoteIndex(Song.Length));
                var minVisiblePattern = Utils.Clamp(Song.PatternIndexFromAbsoluteNoteIndex(minVisibleNoteIdx), 0, Song.Length);
                var maxVisiblePattern = Utils.Clamp(Song.PatternIndexFromAbsoluteNoteIndex(maxVisibleNoteIdx) + 1, 0, Song.Length);

                RenderChannelEffectPanel(c, effectPanelSizeY, minVisiblePattern, maxVisiblePattern);
            }
            else if (panelMode == PanelMode.Notes)
            {
                RenderEnvelopeRepeatPanel(c, effectPanelSizeY);
            }
            else if (panelMode == PanelMode.Wave)
            {
                RenderWaveVolumeEnvelopePanel(c, effectPanelSizeY);
            }

            c.DrawLine(0, effectPanelSizeY - 1, Width, effectPanelSizeY - 1, Theme.BlackColor);
            c.PopClipRegion();
            c.PopTransform();
        }

        protected override void OnRender(Graphics g)
        {
            UpdateButtonLayoutAndVisibility();

            base.OnRender(g);

            var c = g.DefaultCommandList;

            if (pianoRoll.PianoVisible)
                RenderEffectList(c);

            RenderEffectPanel(c);
        }
    }
}
