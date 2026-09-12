using System;
using System.Diagnostics;
using System.Drawing;

namespace FamiStudio
{
    public class PanelContainer : Container
    {
        private Color colorTop;
        private Color colorBottom;
        private bool  selected;

        private float blinkTimer;

        public PanelContainer(Color color)
        {
            Color = color;
            clipRegion = false;
        }

        public Color Color
        {
            get { return colorTop; }
            set 
            {
                colorTop = value;
                colorBottom = Color.FromArgb(200, value);
                MarkDirty();
            }
        }

        public bool Selected
        {
            get { return selected; }
            set
            {
                if (selected != value)
                {
                    selected = value;
                    MarkDirty();
                }
            }
        }

        public void Blink()
        {
            blinkTimer = 2.0f;
            SetTickEnabled(true);
            MarkDirty();
        }

        public override void Tick(float delta)
        {
            if (blinkTimer != 0.0f)
            {
                blinkTimer = MathF.Max(0.0f, blinkTimer - delta);
                if (blinkTimer == 0.0f)
                    SetTickEnabled(false);
                MarkDirty();
            }
        }

        protected override void OnRender(Graphics g)
        {
            var actualColorBottom = colorBottom;
            var c = g.DefaultCommandList;

            if (blinkTimer != 0.0f)
            {
                actualColorBottom = Theme.Darken(colorTop, (int)(MathF.Sin(blinkTimer * MathF.PI * 8.0f) * 16 + 16));
                actualColorBottom = Color.FromArgb(200, actualColorBottom);
            }

            if (selected)
            {
                var borderSize = DpiScaling.ScaleForWindow(5);
                c.DrawRectangle(0, 0, width, height, Theme.BlackColor);
                c.FillRectangleGradient(0, 0, width, borderSize, Theme.Lighten(colorTop, 75), colorTop, true, borderSize);
                c.FillRectangleGradient(0, borderSize, width, height - borderSize - 1, colorTop, actualColorBottom, true, height - borderSize * 2);
                c.FillRectangleGradient(0, height - borderSize - 1, width, height, actualColorBottom, Theme.Darken(actualColorBottom, 75), true, borderSize);
            }
            else
            {
                c.FillAndDrawRectangleGradient(0, 0, width, height, selected ? Theme.Lighten(colorTop) : colorTop, selected ? Theme.Darken(actualColorBottom) : actualColorBottom, Theme.BlackColor, true, height);
            }

            base.OnRender(g);
        }
    }
}
