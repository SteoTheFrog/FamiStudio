using System;

namespace FamiStudio
{
    public class WaveEditor : Control
    {
        const float DefaultZoomWaveTime           = 0.25f;
        const int   DefaultWaveGeometrySampleSize = 2;
        const int   DefaultWaveDisplayPaddingY    = 8;

        private PianoRoll pianoRoll;

        private int waveDisplayPaddingY;
        private float[] sampleGeometry;

        private string noteTooltip = "";

        private DPCMSample EditSample => pianoRoll.EditSample;

        private Color SelectionBgVisibleColor => pianoRoll.SelectionBgVisibleColor;

        private bool IsSelectionValid => pianoRoll.IsSelectionValid();

        private int PianoSizeX           => pianoRoll.PianoSizeX;
        private int SelectionMinX        => pianoRoll.SelectionMinX;
        private int SelectionMaxX        => pianoRoll.SelectionMaxX;
        private int ViewScrollX          => pianoRoll.ViewScrollX;

        private float Zoom => pianoRoll.Zoom;

        private int WaveViewWidth => pianoRoll.Width - PianoSizeX;

        public int WaveDisplayPaddingY => waveDisplayPaddingY;
        public float[] SampleGeometry  => sampleGeometry;

        LocalizedString EditingDPCMSampleLabel;
        LocalizedString DPCMSourceDataLabel;
        LocalizedString DPCMProcessedDataLabel;
        LocalizedString DPCMPreviewPlaybackLabel;

        public WaveEditor(PianoRoll pianoRoll)
        {
            this.pianoRoll = pianoRoll;
            Localization.Localize(this);
        }

        public void UpdateLayout()
        {
            Move(pianoRoll.PianoSizeX, pianoRoll.HeaderAndEffectSizeY, pianoRoll.Width - pianoRoll.PianoSizeX - pianoRoll.ScrollBarThickness, pianoRoll.Height - pianoRoll.HeaderAndEffectSizeY - pianoRoll.ScrollBarThickness);
        }

        public void UpdateRenderCoords()
        {
            waveDisplayPaddingY = DpiScaling.ScaleForWindow(DefaultWaveDisplayPaddingY);

            var waveGeometrySampleSize = DpiScaling.ScaleForWindow(DefaultWaveGeometrySampleSize);
            sampleGeometry = new float[]
            {
                -waveGeometrySampleSize, -waveGeometrySampleSize,
                 waveGeometrySampleSize, -waveGeometrySampleSize,
                 waveGeometrySampleSize,  waveGeometrySampleSize,
                -waveGeometrySampleSize,  waveGeometrySampleSize
            };
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
                    pianoRoll.StartMobilePan(pos.X, pos.Y);
                    return;
                }

                pianoRoll.StartSelectWave(pos.X, pos.Y);
                return;
            }

            if (e.Right)
                e.DelayRightClick(); // Need to wait and see if its a context menu click or not.
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

            if (pianoRoll.IsTimelineColumnSelectionCapture)
            {
                var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.UpdateTimelineCapture(p.X, p.Y);
            }
        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            base.OnPointerUp(e);

            if (e.Right && !pianoRoll.TimelineCaptureThresholdMet)
            {
                var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.HandleContextMenuWave(p.X, p.Y);
            }

            if (pianoRoll.IsTimelineColumnSelectionCapture)
            {
                var p = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.EndTimelineCapture(p.X, p.Y);
            }
        }

        private void ForEachWaveTimecode(Graphics g, Action<float, float, int, int> function)
        {
            var textSize  = g.MeasureString("99.999", Fonts.FontMedium);
            var waveWidth = WaveViewWidth;
            var numLabels = Math.Floor(waveWidth / textSize);

            var minVisibleWaveTime = pianoRoll.GetWaveTimeForPixel(0);
            var maxVisibleWaveTime = pianoRoll.GetWaveTimeForPixel(WaveViewWidth);

            for (int i = 2; i >= 0; i--)
            {
                var divTime = Math.Pow(10.0, -i - 1);

                var minLabel = (int)Math.Floor  (minVisibleWaveTime / divTime);
                var maxLabel = (int)Math.Ceiling(maxVisibleWaveTime / divTime);

                if (i == 0 || numLabels > (maxLabel - minLabel))
                {
                    for (var t = minLabel; t <= maxLabel; t++)
                    {
                        var time = t * divTime;
                        var x = pianoRoll.GetPixelForWaveTime((float)time, ViewScrollX);

                        function((float)time, x, i, t);
                    }

                    break;
                }
            }
        }

        private void RenderWave(CommandList c, float minVisibleWaveTime, float maxVisibleWaveTime, short[] data, float rate, Color color, bool isSource, bool drawSamples)
        {
            var viewWidth     = WaveViewWidth;
            var viewHeight    = Height;
            var halfHeight    = viewHeight / 2;
            var halfHeightPad = halfHeight - waveDisplayPaddingY;
            var viewTime      = DefaultZoomWaveTime / Zoom;

            var unclampedMinVisibleSample = (int)Math.Floor  (minVisibleWaveTime * rate);
            var unclampedMaxVisibleSample = (int)Math.Ceiling(maxVisibleWaveTime * rate);
            var unclampedNumVisibleSample = unclampedMaxVisibleSample - unclampedMinVisibleSample;

            if (unclampedNumVisibleSample > 0 && unclampedMaxVisibleSample > 0 && unclampedMinVisibleSample < data.Length)
            {
                var sampleSkip = 1;
                while (unclampedNumVisibleSample / (sampleSkip * 2) > viewWidth)
                    sampleSkip *= 2;

                var minVisibleSample = Utils.RoundDownAndClamp(unclampedMinVisibleSample,     sampleSkip, 0);
                var maxVisibleSample = Utils.RoundUpAndClamp  (unclampedMaxVisibleSample + 1, sampleSkip, data.Length);
                var numVisibleSample = Utils.DivideAndRoundUp (maxVisibleSample - minVisibleSample, sampleSkip);

                if (numVisibleSample > 0)
                {
                    var points = new float[numVisibleSample * 2];
                    var indices = isSource && drawSamples ? new int[numVisibleSample] : null;
                    var scaleX = 1.0f / (rate * viewTime) * viewWidth;
                    var biasX = (float)-ViewScrollX;

                    for (int i = minVisibleSample, j = 0; i < maxVisibleSample; i += sampleSkip, j++)
                    {
                        points[j * 2 + 0] = i * scaleX + biasX;
                        points[j * 2 + 1] = halfHeight + data[i] / (float)short.MinValue * halfHeightPad;
                        if (indices != null) indices[j] = i;
                    }

                    c.DrawGeometry(points, color, 1, true, false);

                    if (drawSamples)
                    {
                        var selectionValid = IsSelectionValid;

                        for (int i = 0; i < points.Length / 2; i++)
                        {
                            var selected = isSource && selectionValid && indices[i] >= SelectionMinX && indices[i] <= SelectionMaxX;
                            var sampleScale = selected ? 1.5f : 1.0f;

                            c.PushTransform(points[i * 2 + 0], points[i * 2 + 1], sampleScale, sampleScale);
                            c.FillGeometry(sampleGeometry, selected ? Theme.WhiteColor : color);
                            c.PopTransform();
                        }
                    }
                }
            }
        }

        private void RenderDmc(CommandList c, float minVisibleWaveTime, float maxVisibleWaveTime, byte[] data, float rate, float baseTime, Color color, bool isSource, bool drawSamples, int dmcInitialValue)
        {
            var viewWidth     = WaveViewWidth;
            var viewHeight    = Height;
            var halfHeight    = viewHeight / 2;
            var halfHeightPad = halfHeight - waveDisplayPaddingY;
            var viewTime      = DefaultZoomWaveTime / Zoom;

            var unclampedMinVisibleSample = (int)Math.Floor  ((minVisibleWaveTime - baseTime) * rate);
            var unclampedMaxVisibleSample = (int)Math.Ceiling((maxVisibleWaveTime - baseTime) * rate);
            var unclampedNumVisibleSample = unclampedMaxVisibleSample - unclampedMinVisibleSample;

            if (unclampedNumVisibleSample > 0 && unclampedMaxVisibleSample > 0 && unclampedMinVisibleSample < data.Length * 8)
            {
                var sampleSkip = 1;
                while (unclampedNumVisibleSample / (sampleSkip * 2) > viewWidth)
                    sampleSkip *= 2;

                var minVisibleSample = Utils.RoundDownAndClamp(unclampedMinVisibleSample,     sampleSkip, 0);
                var maxVisibleSample = Utils.RoundUpAndClamp  (unclampedMaxVisibleSample + 1, sampleSkip, data.Length * 8);

                // Align to bytes.
                minVisibleSample = Utils.RoundDownAndClamp(minVisibleSample, 8, 0);
                maxVisibleSample = Utils.RoundUpAndClamp  (maxVisibleSample, 8, data.Length * 8);

                var numVisibleSample = Utils.DivideAndRoundUp(maxVisibleSample - minVisibleSample + 1, sampleSkip);

                if (numVisibleSample > 0)
                {
                    var points = new float[numVisibleSample * 2];
                    var indices = isSource && drawSamples ? new int[numVisibleSample] : null;
                    var scaleX = 1.0f / (rate * viewTime) * viewWidth;
                    var biasX = pianoRoll.GetPixelForWaveTime(baseTime, ViewScrollX);

                    var dpcmCounter = dmcInitialValue;

                    for (int i = 0; i < minVisibleSample; i++)
                    {
                        var bit = (i >> 3);
                        var mask = (1 << (i & 7));

                        if ((data[bit] & mask) != 0)
                            dpcmCounter = Math.Min(dpcmCounter + 1, 63);
                        else
                            dpcmCounter = Math.Max(dpcmCounter - 1, 0);
                    }

                    for (int i = minVisibleSample, j = 0; i <= maxVisibleSample; i++)
                    {
                        if ((i & (sampleSkip - 1)) == 0)
                        {
                            points[j * 2 + 0] = i * scaleX + biasX;
                            points[j * 2 + 1] = (-(dpcmCounter - 32) / 64.0f) * 2.0f * halfHeightPad + halfHeight; // DPCMTODO : Is that centered correctly? Also negative value?
                            if (indices != null) indices[j] = i - 1;
                            j++;
                        }

                        if (i < maxVisibleSample)
                        {
                            var bit = (i >> 3);
                            var mask = (1 << (i & 7));

                            if ((data[bit] & mask) != 0)
                                dpcmCounter = Math.Min(dpcmCounter + 1, 63);
                            else
                                dpcmCounter = Math.Max(dpcmCounter - 1, 0);
                        }
                    }

                    c.DrawGeometry(points, color, 1, true, false);

                    if (drawSamples)
                    {
                        var selectionValid = IsSelectionValid;

                        for (int i = 0; i < points.GetLength(0) / 2; i++)
                        {
                            var selected = isSource && selectionValid && indices[i] >= SelectionMinX && indices[i] <= SelectionMaxX;
                            var sampleScale = selected ? 1.5f : 1.0f;

                            c.PushTransform(points[i * 2 + 0], points[i * 2 + 1], sampleScale, sampleScale);
                            c.FillGeometry(sampleGeometry, selected ? Theme.WhiteColor : color);
                            c.PopTransform();
                        }
                    }
                }
            }
        }

        protected override void OnRender(Graphics g)
        {
            base.OnRender(g);

            var b = g.BackgroundCommandList;
            var c = g.DefaultCommandList;
            var f = g.ForegroundCommandList;

            b.PushClipRegion(0, 0, Width, Height);

            var minVisibleWaveTime = pianoRoll.GetWaveTimeForPixel(0);
            var maxVisibleWaveTime = pianoRoll.GetWaveTimeForPixel(WaveViewWidth);

            // Source data range.
            b.FillRectangle(
                pianoRoll.GetPixelForWaveTime(0, ViewScrollX), 0,
                pianoRoll.GetPixelForWaveTime(EditSample.SourceDuration, ViewScrollX), Height, Theme.DarkGreyColor4);

            // Horizontal center line
            var centerY = Height * 0.5f;
            b.DrawLine(0, centerY, Width, centerY, Theme.BlackColor);

            // Top/bottom dash lines (limits);
            var topY    = waveDisplayPaddingY;
            var bottomY = Height - waveDisplayPaddingY;
            b.DrawLine(0, topY,    Width, topY,    Theme.DarkGreyColor1, 1, false, true);
            b.DrawLine(0, bottomY, Width, bottomY, Theme.DarkGreyColor1, 1, false, true);

            // Vertical lines (1.0, 0.1, 0.01 seconds)
            ForEachWaveTimecode(g, (time, x, level, idx) =>
            {
                var modSeconds = Utils.IntegerPow(10, level + 1);
                var modTenths  = Utils.IntegerPow(10, level);

                var brush = Theme.DarkGreyColor1;
                var dash = true;

                if ((idx % modSeconds) == 0)
                {
                    dash = false;
                    brush = Theme.BlackColor;
                }
                else if ((idx % modTenths) == 0)
                {
                    dash = false;
                    brush = Theme.DarkGreyColor1;
                }

                b.DrawLine(x, 0, x, Height, brush, 1, false, dash);
            });

            // Selection rectangle
            if (IsSelectionValid)
            {
                b.FillRectangle(
                    pianoRoll.GetPixelForWaveTime(pianoRoll.GetWaveTimeForSample(SelectionMinX, true),  ViewScrollX), 0,
                    pianoRoll.GetPixelForWaveTime(pianoRoll.GetWaveTimeForSample(SelectionMaxX, false), ViewScrollX), Height, SelectionBgVisibleColor);
            }

            // TODO: Make this a constants.
            bool showSamples = Zoom > 32.0f;

            // Source waveform
            if (EditSample.SourceDataIsWav)
            {
                RenderWave(c, minVisibleWaveTime, maxVisibleWaveTime, EditSample.SourceWavData.Samples, EditSample.SourceSampleRate, Theme.LightGreyColor1, true, showSamples);
            }
            else
            {
                RenderDmc(c, minVisibleWaveTime, maxVisibleWaveTime, EditSample.SourceDmcData.Data, EditSample.SourceSampleRate, 0.0f, Theme.LightGreyColor1, true, showSamples, EditSample.DmcInitialValueDiv2);
            }

            // Processed waveform
            RenderDmc(c, minVisibleWaveTime, maxVisibleWaveTime, EditSample.ProcessedData, EditSample.ProcessedSampleRate, EditSample.ProcessedStartTime, EditSample.Color, false, showSamples, EditSample.GetVolumeScaleDmcInitialValueDiv2());

            // Play position
            var playPosition = App.PreviewDPCMWavPosition;

            if (playPosition >= 0 && App.PreviewDPCMSampleId == EditSample.Id)
            {
                var playTime = playPosition / (float)App.PreviewDPCMSampleRate;
                if (!App.PreviewDPCMIsSource)
                    playTime += EditSample.ProcessedStartTime;
                var seekX = pianoRoll.GetPixelForWaveTime(playTime, ViewScrollX);
                c.DrawLine(seekX, 0, seekX, Height, App.PreviewDPCMIsSource ? Theme.LightGreyColor1 : EditSample.Color, 3);
            }

            // Title + source/processed info.
            var textY = pianoRoll.BigTextPosY;
            f.DrawText(EditingDPCMSampleLabel.Format(EditSample.Name), Fonts.FontVeryLarge, pianoRoll.BigTextPosX, textY, Theme.LightGreyColor1);
            textY += Fonts.FontVeryLarge.LineHeight;
            f.DrawText(DPCMSourceDataLabel.Format(EditSample.SourceDataIsWav ? "WAV" : "DMC", EditSample.SourceSampleRate, EditSample.SourceDataSize, (int)(EditSample.SourceDuration * 1000)), Fonts.FontMedium, pianoRoll.BigTextPosX, textY, Theme.LightGreyColor1);
            textY += Fonts.FontMedium.LineHeight;
            f.DrawText(DPCMProcessedDataLabel.Format(DPCMSampleRate.GetString(false, App.PalPlayback, true, true, EditSample.SampleRate), EditSample.ProcessedData.Length, (int)(EditSample.ProcessedDuration * 1000)), Fonts.FontMedium, pianoRoll.BigTextPosX, textY, Theme.LightGreyColor1);
            textY += Fonts.FontMedium.LineHeight;
            f.DrawText(DPCMPreviewPlaybackLabel.Format(DPCMSampleRate.GetString(false, App.PalPlayback, true, true, EditSample.PreviewRate), (int)(EditSample.GetPlaybackDuration(App.PalPlayback) * 1000)), Fonts.FontMedium, pianoRoll.BigTextPosX, textY, Theme.LightGreyColor1);

            if (!string.IsNullOrEmpty(noteTooltip))
            {
                f.DrawText(noteTooltip, Fonts.FontLarge, 0, Height - pianoRoll.TooltipTextPosY, Theme.LightGreyColor1, TextFlags.Right, Width - pianoRoll.TooltipTextPosX);
            }

            b.PopClipRegion();
        }
    }
}
