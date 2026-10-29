using System;

namespace FamiStudio
{
    public class SequencerTimelinePanel : Container
    {
        private const int DefaultHeaderIconPosX = 3;
        private const int DefaultHeaderIconPosY = 3;

        private static readonly int headerIconPosX = DpiScaling.ScaleForWindow(DefaultHeaderIconPosX);
        private static readonly int headerIconPosY = DpiScaling.ScaleForWindow(DefaultHeaderIconPosY);

        private Color backgroundColor;
        private readonly int patternIndex;

        private TextureAtlasRef bmpLoopPoint;
        
        public int PatternIndex => patternIndex;

        public SequencerTimelinePanel(int idx)
        {
            Localization.Localize(this);
            patternIndex = idx;
            clipRegion = false;
        }

        protected override void OnAddedToContainer()
        {
            backgroundColor = patternIndex % 2 == 0 ? Theme.DarkGreyColor4 : Theme.DarkGreyColor2;
            bmpLoopPoint = Graphics.GetTextureAtlasRef("LoopSmallFill");
        }

        private bool IsPatternHighlighted()
        {
            if (App.Sequencer.IsValidTimeOnlySelection())
            {
                var sequencer = App.Sequencer;
                
                if (sequencer.SelectedTimelineColumns.Contains(patternIndex))
                    return true;

                if (sequencer.IsColumnSelectionInProgress && sequencer.MarqueeMin.IsValid && sequencer.MarqueeMax.IsValid)
                    return patternIndex >= sequencer.MarqueeMin.PatternIndex && patternIndex <= sequencer.MarqueeMax.PatternIndex;
            }

            return false;
        }

        protected override void OnRender(Graphics g)
        {
            var c = g.DefaultCommandList;
            var hovered = App.Sequencer.HoverPosition.X == patternIndex;
            var color = hovered ? Theme.MediumGreyColor1 : backgroundColor;
            var text = (patternIndex + 1).ToString() + (App.SelectedSong.PatternHasCustomSettings(patternIndex) ? "*" : "");
            var highlight = IsPatternHighlighted();

            if (highlight)
                c.FillAndDrawRectangle(0, 0, width, height, Theme.Lighten(color), Theme.WhiteColor, 3, true, true);
            else
                c.FillAndDrawRectangle(0, 0, width, height, color, Theme.BlackColor);

            c.DrawText(text, fonts.FontMedium, 0, 0, Theme.LightGreyColor2, TextFlags.MiddleCenter, width, height);

            if (patternIndex == App.SelectedSong.LoopPoint)
                c.DrawTextureAtlas(bmpLoopPoint, headerIconPosX, headerIconPosY, App.Sequencer.BitmapScale, Theme.LightGreyColor1);

            base.OnRender(g);
        }
    }
}