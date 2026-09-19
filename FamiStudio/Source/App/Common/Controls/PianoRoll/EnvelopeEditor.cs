using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace FamiStudio
{
    public class EnvelopeEditor : Control
    {
        const int DefaultEnvelopeSizeY = Platform.IsMobile ? 4 : 9;

        public enum TimelineHoverRegion
        {
            None,
            ResizeIcon,
            LoopRelease
        }

        private PianoRoll pianoRoll;
        private string noteTooltip = "";

        LocalizedString SetEnvelopeValueTooltip;
        LocalizedString PanTooltip;
        LocalizedString SamplesSelectedTooltip;
        LocalizedString ValuesSelectedTooltip;
        LocalizedString FramesSelectedTooltip;

        LocalizedString EditingArpeggioLabel;
        LocalizedString EditingInstrumentEnvelopeLabel;
        LocalizedString InstrumentNotSelectedLabel;
        LocalizedString ArpeggioOverriddenLabel;
        LocalizedString ArpeggioNotSelectedLabel;
        LocalizedString SelectedArpeggioWillBeHeardLabel;
        LocalizedString RelativeEffectScalingLabel;
        LocalizedString EnvelopeRelativeLabel;
        LocalizedString EnvelopeAbsoluteLabel;

        public EnvelopeEditor(PianoRoll pianoRoll)
        {
            this.pianoRoll = pianoRoll;
            Localization.Localize(this);
            ToolTip = $"<MouseLeft> {SetEnvelopeValueTooltip} - <MouseWheel><Drag> {PanTooltip}\n<MouseRight> {pianoRoll.PianoRollMoreOptionsTooltip}";
            supportsLongPress = true;
        }

        protected override void OnPointerEnter(EventArgs e)
        {
            base.OnPointerEnter(e);
            App.SetToolTip(tooltip);
        }

        private int GetPixelXForAbsoluteNoteIndex(int idx)
        {
            return pianoRoll.GetPixelXForAbsoluteNoteIndex(idx);
        }

        private bool IsEnvelopeValueSelected(int idx)
        {
            return pianoRoll.IsEnvelopeValueSelected(idx);
        }

        private void DrawSelectionRect(CommandList commandList, int height)
        {
            pianoRoll.DrawSelectionRect(commandList, height);
        }

        private bool IsOverResizeIcon(int x, int y, int timelineHeight, Envelope env)
        {
            if (!env.CanResize || y >= timelineHeight / 2)
                return false;

            var resizeX = GetPixelXForAbsoluteNoteIndex(env.Length) + pianoRoll.PianoSizeX;

            if (Platform.IsMobile)
                return x > resizeX;

            return x > resizeX && x <= resizeX + pianoRoll.TimelineEnvelopeResizeWidth;
        }

        public bool HandleTimelinePointerDown(int x, int y, bool left, bool right, int timelineHeight, bool capturePointer = true)
        {
            var env = pianoRoll.CurrentEditEnvelope;

            if (env == null)
                return false;

            if (left && IsOverResizeIcon(x, y, timelineHeight, env))
            {
                pianoRoll.StartEnvelopeResize(x, y, capturePointer);
                return true;
            }

            if (y >= timelineHeight / 2)
            {
                var rep = pianoRoll.EditRepeatEnvelope;
                var canLoop = env.CanLoop || (rep != null && rep.CanLoop);
                var canRelease = env.CanRelease || (rep != null && rep.CanRelease);

                if (left && canLoop)
                {
                    pianoRoll.StartEnvelopeLoopRelease(x, y, true, capturePointer);
                    return true;
                }

                if (right && canRelease && env.Loop >= 0)
                {
                    var length = Utils.RoundDown(pianoRoll.GetAbsoluteNoteIndexForPixelX(x - pianoRoll.PianoSizeX), env.ChunkLength);
                    if (length > env.Loop)
                    {
                        pianoRoll.StartEnvelopeLoopRelease(x, y, false, capturePointer);
                        return true;
                    }
                }
            }

            return false;
        }

        public TimelineHoverRegion GetTimelineHoverRegion(int x, int y, int timelineHeight, out bool canLoop, out bool canRelease, out bool hasLoopPoint)
        {
            canLoop = false;
            canRelease = false;
            hasLoopPoint = false;

            var env = pianoRoll.CurrentEditEnvelope;
            if (env == null)
                return TimelineHoverRegion.None;

            if (IsOverResizeIcon(x, y, timelineHeight, env))
                return TimelineHoverRegion.ResizeIcon;

            if (y >= timelineHeight / 2)
            {
                var rep = pianoRoll.EditRepeatEnvelope;
                canLoop = env.CanLoop || (rep != null && rep.CanLoop);
                canRelease = env.CanRelease || (rep != null && rep.CanRelease);
                hasLoopPoint = env.Loop >= 0;

                if (canLoop || canRelease)
                    return TimelineHoverRegion.LoopRelease;
            }

            return TimelineHoverRegion.None;
        }

        public void UpdateLayout()
        {
            Move(pianoRoll.PianoSizeX, pianoRoll.HeaderAndEffectSizeY, pianoRoll.Width - pianoRoll.PianoSizeX - pianoRoll.ScrollBarThickness, pianoRoll.Height - pianoRoll.HeaderAndEffectSizeY - pianoRoll.ScrollBarThickness);
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            base.OnPointerDown(e);

            var middle = e.Middle || (e.Left && ModifierKeys.IsAltDown && Settings.AltLeftForMiddle);
            if (middle)
            {
                var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.StartPan(p.X, p.Y);
                return;
            }

            var pos = pianoRoll.WindowToControl(ControlToWindow(e.Position));

            if (e.Left)
            {
                if (e.IsTouchEvent)
                {
                    if (pianoRoll.HandleEnvelopeGizmoPointerDown(pos.X, pos.Y, false))
                        return;

                    pianoRoll.HandleEnvelopeTouchClick(pos.X, pos.Y);
                    pianoRoll.StartMobilePan(pos.X, pos.Y);
                    return;
                }

                CapturePointer();

                if (!StartEnvelopeDraw(pos.X, pos.Y))
                    ReleasePointer();

                return;
            }

            if (e.Right)
                e.DelayRightClick(); // Need to wait and see if its a context menu click or not.
        }

        private bool StartEnvelopeDraw(int x, int y)
        {
            var env = pianoRoll.CurrentEditEnvelope;
            if (env == null || env.Length <= 0)
                return false;

            var noteIdx = pianoRoll.GetAbsoluteNoteIndexForPixelX(x - pianoRoll.PianoSizeX);

            if (IsEnvelopeValueSelected(noteIdx))
            {
                pianoRoll.SetMobileHighlightedNote(noteIdx);
                pianoRoll.StartChangeEnvelopeValue(x, y, false);
            }
            else
            {
                pianoRoll.StartDrawEnvelope(x, y, false);
            }

            return true;
        }

        protected override void OnPointerDownDelayed(PointerEventArgs e)
        {
            base.OnPointerDownDelayed(e);

            if (e.Right)
            {
                CapturePointer();

                var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.StartTimelineSelection(p.X, p.Y);
            }
        }

        protected override void OnTouchFling(PointerEventArgs e)
        {
            var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
            pianoRoll.HandleTouchFling(p.X, p.Y, e.FlingVelocityX, e.FlingVelocityY);
        }

        protected override void OnTouchScaleBegin(PointerEventArgs e)
        {
            base.OnTouchScaleBegin(e);
            var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
            pianoRoll.HandleTouchScaleBegin(p.X, p.Y, false);
        }

        protected override void OnTouchScale(PointerEventArgs e)
        {
            var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
            pianoRoll.HandleTouchScale(p.X, p.Y, e.TouchScale);
        }

        protected override void OnTouchScaleEnd(PointerEventArgs e)
        {
            var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
            pianoRoll.HandleTouchScaleEnd(p.X, p.Y);
        }

        protected override void OnPointerMove(PointerEventArgs e)
        {
            base.OnPointerMove(e);
            var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));

            if (pianoRoll.IsTimelineColumnSelectionCapture || pianoRoll.IsChangingEnvelopeValue || pianoRoll.IsDrawingEnvelope)
            {
                pianoRoll.UpdateTimelineCapture(p.X, p.Y);
            }

            if (pianoRoll.IsChangingEnvelopeValue ||
                (pianoRoll.GetEnvelopeValueForCoord(p.X, p.Y, out int hoverIdx, out _) && IsEnvelopeValueSelected(hoverIdx)))
            {
                Cursor = Cursors.SizeNS;
            }
            else
            {
                Cursor = Cursors.Default;
            }

            UpdateNoteTooltip(e);
        }

        public void UpdateNoteTooltip(PointerEventArgs e)
        {
            var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
            var newNoteTooltip = "";

            if (pianoRoll.GetEnvelopeValueForCoord(p.X, p.Y, out int idx, out sbyte value))
            {
                newNoteTooltip = $"{idx:D3} : {value}";
            }

            if (pianoRoll.LegacySelectMode ? pianoRoll.IsSelectionValid() : pianoRoll.IsSelectCapture)
            {
                if (newNoteTooltip.Length > 0)
                    newNoteTooltip += " ";

                var numValuesSelected = pianoRoll.LegacySelectMode
                    ? (pianoRoll.SelectionMaxX - pianoRoll.SelectionMinX + 1)
                    : (pianoRoll.CaptureMarqueeMaxX - pianoRoll.CaptureMarqueeMinX + 1);

                switch (pianoRoll.EditEnvelopeType)
                {
                    case EnvelopeType.FdsWaveform:
                    case EnvelopeType.N163Waveform:
                        newNoteTooltip += $"({SamplesSelectedTooltip.Format(numValuesSelected)})";
                        break;
                    case EnvelopeType.FdsModulation:
                        newNoteTooltip += $"({ValuesSelectedTooltip.Format(numValuesSelected)})";
                        break;
                    default:
                        newNoteTooltip += $"({FramesSelectedTooltip.Format(numValuesSelected)})";
                        break;
                }
            }

            if (noteTooltip != newNoteTooltip)
            {
                noteTooltip = newNoteTooltip;
                MarkDirty();
            }
        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            base.OnPointerUp(e);

            if (e.Right && !pianoRoll.TimelineCaptureThresholdMet)
            {
                var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.HandleContextMenuEnvelope(p.X, p.Y);
            }

            if (pianoRoll.IsTimelineColumnSelectionCapture || pianoRoll.IsChangingEnvelopeValue || pianoRoll.IsDrawingEnvelope)
            {
                var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.EndTimelineCapture(p.X, p.Y);
            }

            UpdateNoteTooltip(e);
        }

        protected override void OnRender(Graphics g)
        {
            var b = g.BackgroundCommandList;
            var c = g.DefaultCommandList;
            var f = g.ForegroundCommandList;

            // TODO: Some of these should probably be properties here instead of referencing pianoRoll every time.
            var env            = pianoRoll.CurrentEditEnvelope;
            var editInstrument = pianoRoll.EditInstrument;
            var editArpeggio   = pianoRoll.EditArpeggio;
            var editEnvelope   = pianoRoll.EditEnvelopeType;

            var envelopeValueZoom  = pianoRoll.EnvelopeValueZoom;
            var envelopeValueSizeY = pianoRoll.EnvelopeValueSizeY;
            var virtualSizeY       = pianoRoll.VirtualSizeY;
            var scrollY            = pianoRoll.ViewScrollY;
            var noteSizeX          = pianoRoll.NoteSizeX;

            var isEnvelope = pianoRoll.IsEditingInstrument;
            var isArpeggio = pianoRoll.IsEditingArpeggio;

            var legacySelectMode = pianoRoll.LegacySelectMode;
            var highlightNoteAbsIndex = pianoRoll.HighlightNoteAbsoluteIndex;
            var selectionBgVisibleColor = pianoRoll.SelectionBgVisibleColor;
            var selectionHighlightColor = pianoRoll.SelectionHighlightColor;

            var fontSmallCharSizeX = pianoRoll.FontSmallCharSizeX;
            var effectValuePosTextOffsetY = pianoRoll.EffectValuePosTextOffsetY;
            var effectValueNegTextOffsetY = pianoRoll.EffectValueNegTextOffsetY;

            var resampled = isEnvelope && 
                           (editInstrument.IsN163 && editEnvelope == EnvelopeType.N163Waveform && editInstrument.N163ResampleWaveData != null && editInstrument.N163WavePreset == WavePresetType.Resample ||
                            editInstrument.IsFds  && editEnvelope == EnvelopeType.FdsWaveform  && editInstrument.FdsResampleWaveData  != null && editInstrument.FdsWavePreset  == WavePresetType.Resample);
            var spacing = editEnvelope == EnvelopeType.DutyCycle || editEnvelope == EnvelopeType.S5BMixer ? 4 : (editEnvelope == EnvelopeType.Arpeggio ? 12 : 16);
            var color = pianoRoll.IsEditingInstrument ? pianoRoll.EditInstrument.Color : pianoRoll.EditArpeggio.Color;
            var brush = Color.FromArgb(resampled ? 100 : 255, color);

            Envelope.GetMinMaxValueForType(editInstrument, editEnvelope, out int envTypeMinValue, out int envTypeMaxValue);

            // Draw the envelope value backgrounds.
            int maxValue = 128 / (int)envelopeValueZoom;
            int midValue =  64 / (int)envelopeValueZoom;

            var lastRectangleValue = int.MinValue;
            var lastRectangleY     = -1.0f;
            var oddRectangle       = false;

            var maxX = GetPixelXForAbsoluteNoteIndex(env.Length);
            var maxi = (Platform.IsDesktop ? maxValue : envTypeMaxValue - envTypeMinValue) + 1;

            // Background rectangles + labels
            for (int i = 0; i <= maxi; i++)
            {
                var value = Platform.IsMobile ? i + envTypeMinValue : i - midValue;
                var y = (virtualSizeY - envelopeValueSizeY * i) - scrollY;
                var drawLabel = i == maxi - 1;

                if ((value % spacing) == 0 || i == 0 || i == maxi)
                {
                    if (lastRectangleValue >= envTypeMinValue && lastRectangleValue < envTypeMaxValue)
                    {
                        b.FillRectangle(0, lastRectangleY, maxX, y, oddRectangle ? Theme.DarkGreyColor5 : Theme.DarkGreyColor4);
                        oddRectangle = !oddRectangle;
                    }

                    lastRectangleValue = value;
                    lastRectangleY = y;
                    drawLabel |= value >= envTypeMinValue - 1 && value <= envTypeMaxValue + 1;
                }

                if (drawLabel)
                    b.DrawText(value.ToString(), Fonts.FontSmall, maxX + 4 * DpiScaling.Window, y - envelopeValueSizeY, Theme.LightGreyColor1, TextFlags.MiddleLeft, 0, envelopeValueSizeY);
            }

            if (Platform.IsDesktop)
            {
                var maxRowI = envTypeMaxValue + midValue;
                var maxRowYBottom = virtualSizeY - envelopeValueSizeY * maxRowI - scrollY;
                var maxRowYTop = maxRowYBottom - envelopeValueSizeY;
                b.FillRectangle(0, maxRowYTop, maxX, maxRowYBottom, Theme.DarkGreyColor4);
            }

            // Horizontal lines
            for (int i = 0; i <= maxi; i++)
            {
                var value = Platform.IsMobile ? i + envTypeMinValue : i - midValue;
                var y = (virtualSizeY - envelopeValueSizeY * i) - scrollY;

                if (i != maxi && value >= envTypeMinValue && value <= envTypeMaxValue + 1)
                    b.DrawLine(0, y, GetPixelXForAbsoluteNoteIndex(env.Length), y, Theme.DarkGreyColor1, (value % spacing) == 0 ? 3 : 1);
            }

            DrawSelectionRect(b, Height);

            // Draw the vertical bars.
            for (int i = 0; i < env.Length; i++)
            {
                int x = GetPixelXForAbsoluteNoteIndex(i);
                if (i != 0) b.DrawLine(x, 0, x, Height, Theme.DarkGreyColor1, env.ChunkLength > 1 && i % env.ChunkLength == 0 ? 3 : 1);
            }

            if (env.Loop >= 0)
                b.DrawLine(GetPixelXForAbsoluteNoteIndex(env.Loop), 0, GetPixelXForAbsoluteNoteIndex(env.Loop), Height, Theme.BlackColor);
            if (env.Release >= 0)
                b.DrawLine(GetPixelXForAbsoluteNoteIndex(env.Release), 0, GetPixelXForAbsoluteNoteIndex(env.Release), Height, Theme.BlackColor);
            if (env.Length > 0)
                b.DrawLine(GetPixelXForAbsoluteNoteIndex(env.Length), 0, GetPixelXForAbsoluteNoteIndex(env.Length), Height, Theme.BlackColor);

            if ((isEnvelope || isArpeggio) && pianoRoll.CanDisplayEnvelopePlayhead)
            {
                var seekFrame = App.GetEnvelopeFrame(editInstrument, editArpeggio, editEnvelope, isArpeggio);
                if (seekFrame >= 0)
                {
                    var seekX = GetPixelXForAbsoluteNoteIndex(seekFrame);
                    c.DrawLine(seekX, 0, seekX, Height, pianoRoll.SeekBarColor, 3);
                }
            }

            var highlightRect = RectangleF.Empty;
            var center = editEnvelope == EnvelopeType.FdsWaveform ? 32 : 0;
            var bias = Platform.IsMobile ? -envTypeMinValue : midValue;

            if (editEnvelope == EnvelopeType.Arpeggio)
            {
                for (int i = 0; i < env.Length; i++)
                {
                    var selected = IsEnvelopeValueSelected(i);
                    var highlighted = Platform.IsMobile && highlightNoteAbsIndex == i;

                    float x0 = GetPixelXForAbsoluteNoteIndex(i + 0);
                    float x1 = GetPixelXForAbsoluteNoteIndex(i + 1);
                    float y = (virtualSizeY - envelopeValueSizeY * (env.Values[i] + bias)) - scrollY;

                    c.FillRectangle(x0, y - envelopeValueSizeY, x1, y, brush);

                    if (!highlighted)
                        c.DrawRectangle(x0, y - envelopeValueSizeY, x1, y, selected ? Theme.LightGreyColor1 : Theme.BlackColor, selected ? 3 : 1, selected, selected);
                    else
                        highlightRect = new RectangleF(x0, y - envelopeValueSizeY, x1 - x0, envelopeValueSizeY);

                    var label = Envelope.GetDisplayValue(editInstrument, editEnvelope, env.Values[i]);
                    if (label.Length * fontSmallCharSizeX + 2 < noteSizeX)
                        f.DrawText(label, Fonts.FontSmall, x0, y - envelopeValueSizeY - effectValuePosTextOffsetY, Theme.LightGreyColor1, TextFlags.Center, noteSizeX);
                }
            }
            else
            {
                for (int i = 0; i < env.Length; i++)
                {
                    int val = env.Values[i];

                    float y0, y1, ty;
                    if (val >= center)
                    {
                        y0 = (virtualSizeY - envelopeValueSizeY * (val + bias + 1)) - scrollY;
                        y1 = (virtualSizeY - envelopeValueSizeY * (bias + center) - scrollY);
                        ty = y0;
                    }
                    else
                    {
                        y1 = (virtualSizeY - envelopeValueSizeY * (val + bias)) - scrollY;
                        y0 = (virtualSizeY - envelopeValueSizeY * (bias + center + 1) - scrollY);
                        ty = y1;
                    }

                    var x0 = GetPixelXForAbsoluteNoteIndex(i + 0);
                    var x1 = GetPixelXForAbsoluteNoteIndex(i + 1);
                    var selected = IsEnvelopeValueSelected(i);
                    var highlighted = Platform.IsMobile && highlightNoteAbsIndex == i;

                    c.FillRectangle(x0, y0, x1, y1, brush);

                    if (selected && !legacySelectMode)
                        c.FillRectangle(x0, y0, x1, y1, selectionHighlightColor);

                    if (!highlighted)
                        c.DrawRectangle(x0, y0, x1, y1, selected ? legacySelectMode ? Theme.LightGreyColor2 : Theme.WhiteColor : Theme.BlackColor, selected ? 3 : 1, selected, selected);
                    else
                        highlightRect = new RectangleF(x0, y0, x1 - x0, y1 - y0);

                    var label = Envelope.GetDisplayValue(editInstrument, editEnvelope, val);
                    if (label.Length * fontSmallCharSizeX + 2 < noteSizeX)
                    {
                        var drawOutside = Math.Abs(y1 - y0) < (DefaultEnvelopeSizeY * DpiScaling.Window * 2);
                        var textBrush = drawOutside ? Theme.LightGreyColor1 : Theme.BlackColor;
                        var offset = drawOutside != val < center ? -effectValuePosTextOffsetY : effectValueNegTextOffsetY;

                        f.DrawText(label, Fonts.FontSmall, x0, ty + offset, textBrush, TextFlags.Center, noteSizeX);
                    }
                }
            }

            if (!highlightRect.IsEmpty)
                c.DrawRectangle(highlightRect, Theme.WhiteColor, legacySelectMode ? 3 : 6, true, true);

            // Drawing the N163/FDS waveform on top. 
            if (resampled)
            {
                var isN163     = editInstrument.IsN163;

                var waveSize   = isN163 ? editInstrument.N163WaveSize : 64;
                var wavePeriod = isN163 ? editInstrument.N163ResampleWavePeriod : editInstrument.FdsResampleWavePeriod;
                var waveOffset = isN163 ? editInstrument.N163ResampleWaveOffset : editInstrument.FdsResampleWaveOffset;
                var waveData   = isN163 ? editInstrument.N163ResampleWaveData   : editInstrument.FdsResampleWaveData;

                var numSamplesPerEnvelopeValue  = wavePeriod / (float)waveSize;
                var numVerticesPerColumn = (int)(noteSizeX * 0.5f);

                Debug.Assert(numVerticesPerColumn >= 1);

                var line = new List<float>(width);
                var prevSampleIndex = -1;
                var prevX = 0.0f;
                var prevY = 0.0f;

                // Start at -1 to always draw the first little bit in the first 1/2 of the first value.
                for (var i = -1; i < env.Length; i++)
                {
                    var x0 = GetPixelXForAbsoluteNoteIndex(i + 0);
                    var x1 = GetPixelXForAbsoluteNoteIndex(i + 1);

                    for (var j = 0; j < numVerticesPerColumn; j++)
                    {
                        var sampleIndex = (int)Math.Floor(waveOffset + i * numSamplesPerEnvelopeValue + (j * numSamplesPerEnvelopeValue / numVerticesPerColumn));
                        if (sampleIndex >= 0 && sampleIndex != prevSampleIndex)
                        {
                            if (sampleIndex >= waveData.Length)
                            {
                                i = env.Length;
                                break;
                            }

                            var sample = waveData[sampleIndex];
                            var val = Utils.Lerp(envTypeMinValue, envTypeMaxValue + 1, (sample + 32768.0f) / 65535.0f);

                            var x = Utils.Lerp(x0, x1, j / (float)numVerticesPerColumn) + noteSizeX * 0.5f;
                            var y = (virtualSizeY - envelopeValueSizeY * (val + bias)) - scrollY;

                            // Clip line at end.
                            if (x >= maxX)
                            {
                                var ratio = (maxX - prevX) / (x - prevX);
                                x = Utils.Lerp(prevX, x, ratio);
                                y = Utils.Lerp(prevY, y, ratio);
                                i = env.Length;
                            }

                            line.Add(x);
                            line.Add(y);

                            prevSampleIndex = sampleIndex;
                            prevX = x;
                            prevY = y;
                        }
                    }
                }

                c.DrawLine(line, Theme.LightGreyColor2, 1, true);
            }

            if (isEnvelope)
            {
                string envelopeString = EnvelopeType.LocalizedNames[editEnvelope];

                if (editEnvelope == EnvelopeType.Pitch)
                    envelopeString = (editInstrument.Envelopes[editEnvelope].Relative ? EnvelopeRelativeLabel : EnvelopeAbsoluteLabel) + " " + envelopeString;

                f.DrawText(EditingInstrumentEnvelopeLabel.Format(editInstrument.Name, envelopeString), Fonts.FontVeryLarge, pianoRoll.BigTextPosX, pianoRoll.BigTextPosY, Theme.LightGreyColor1);

                var textY = pianoRoll.BigTextPosY + Fonts.FontVeryLarge.LineHeight;

                if (App.SelectedInstrument != null && App.SelectedInstrument != editInstrument)
                {
                    f.DrawText(InstrumentNotSelectedLabel.Format(App.SelectedInstrument.Name), Fonts.FontMedium, pianoRoll.BigTextPosX, textY, Theme.LightRedColor);
                    textY += Fonts.FontMedium.LineHeight;
                }
                else if (editEnvelope == EnvelopeType.Arpeggio && App.SelectedArpeggio != null)
                {
                    f.DrawText(ArpeggioOverriddenLabel.Format(App.SelectedArpeggio.Name), Fonts.FontMedium, pianoRoll.BigTextPosX, textY, Theme.LightRedColor);
                    textY += Fonts.FontMedium.LineHeight;
                }

                if (pianoRoll.RelativeEffectScaling && pianoRoll.IsSelectionValid())
                {
                    c.DrawText(RelativeEffectScalingLabel, Fonts.FontMedium, pianoRoll.BigTextPosX, textY, Theme.LightRedColor);
                }
            }
            else
            {
                f.DrawText(EditingArpeggioLabel.Format(editArpeggio.Name), Fonts.FontVeryLarge, pianoRoll.BigTextPosX, pianoRoll.BigTextPosY, Theme.LightGreyColor1);

                if (App.SelectedArpeggio != editArpeggio)
                {
                    f.DrawText(App.SelectedArpeggio == null ?
                        $"{ArpeggioNotSelectedLabel}" :
                        $"{ArpeggioNotSelectedLabel} {SelectedArpeggioWillBeHeardLabel.Format(App.SelectedArpeggio.Name)}", Fonts.FontMedium, pianoRoll.BigTextPosX, pianoRoll.BigTextPosY + Fonts.FontVeryLarge.LineHeight, Theme.LightRedColor);
                }
            }

            var gizmos = pianoRoll.GetEnvelopeGizmos();
            if (gizmos != null)
            {
                foreach (var gz in gizmos)
                {
                    var lineColor = pianoRoll.IsGizmoHighlighted(gz, 0) ? Color.White : Color.Black;

                    if (gz.FillImage != null)
                        f.DrawTextureAtlas(gz.FillImage, gz.Rect.X, gz.Rect.Y, gz.Rect.Width / (float)gz.Image.ElementSize.Width, color);
                    f.DrawTextureAtlas(gz.Image, gz.Rect.X, gz.Rect.Y, gz.Rect.Width / (float)gz.Image.ElementSize.Width, lineColor);
                }
            }

            if (!string.IsNullOrEmpty(noteTooltip))
            {
                var textWidth = Width - pianoRoll.TooltipTextPosX;
                if (textWidth > 0)
                    f.DrawText(noteTooltip, Fonts.FontLarge, 0, Height - pianoRoll.TooltipTextPosY, Theme.LightGreyColor1, TextFlags.Right, textWidth);
            }
        }
    }
}