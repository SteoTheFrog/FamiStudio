using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FamiStudio
{
    public class PatternPanel : Control
    {
        private PatternBitmapCache bitmapCache;
        private PatternLocation location;
        private int iconSize;
        private int iconX;
        private int iconY;
        private int patternHeaderSizeY;
        private int patternNamePosX;
        private float bitmapScale = 1.0f;

        TextureAtlasRef bmpMenuInstance;

        private Sequencer Sequencer => App.Sequencer;
        private Song Song => App.SelectedSong;

        private PatternLocation MarqueeMin => Sequencer.MarqueeMin;
        private PatternLocation MarqueeMax => Sequencer.MarqueeMax;

        public PatternLocation Location => location;

        public PatternPanel(PatternBitmapCache cache, PatternLocation loc)
        {
            bitmapCache = cache;
            location = loc;
            supportsDoubleClick = true;
            supportsLongPress = true;
        }

        protected override void OnAddedToContainer()
        {
            bmpMenuInstance = Graphics.GetTextureAtlasRef("MenuInstance");
        }

        private bool IsPatternHighlighted(PatternLocation location)
        {
            if (Sequencer.IsPatternSelected(location))
                return true;

            if (MarqueeMin.IsValid && MarqueeMax.IsValid)
            {
                return location.ChannelIndex >= MarqueeMin.ChannelIndex && location.ChannelIndex <= MarqueeMax.ChannelIndex &&
                       location.PatternIndex >= MarqueeMin.PatternIndex && location.PatternIndex <= MarqueeMax.PatternIndex;
            }

            return false;
        }

        public void UpdateLayout(int channelSizeY, int headerSizeY, int namePosX)
        {
            patternHeaderSizeY = headerSizeY;
            height = channelSizeY;
            patternNamePosX = namePosX;

            iconSize = bmpMenuInstance.ElementSize.Width;
            iconX = width / 2 - iconSize / 2;
            iconY = (height + patternHeaderSizeY) / 2 - iconSize / 2;
            
            MarkDirty(); 
        }

        protected override void OnRender(Graphics g)
        { 
            var pattern = Song.GetPatternInstance(location);
            if (pattern != null)
            {
                var c = g.DefaultCommandList;
                var f = g.ForegroundCommandList;

                var patternCacheSizeY = height - patternHeaderSizeY - 1;
                var patternLen = Song.GetPatternLength(location.PatternIndex);
                var noteLen = Song.UsesFamiTrackerTempo ? 1 : Song.GetPatternNoteLength(location.PatternIndex);

                if (Platform.IsMobile && Sequencer.HighlightLocation == location)
                {
                    f.FillAndDrawRectangle(0, 0, width, height, Color.FromArgb(75, Theme.WhiteColor), Theme.BlueColor, 3, true, true);
                }

                var bmp = bitmapCache.GetOrAddPattern(pattern, patternLen, noteLen, out var u0, out var v0, out var u1, out var v1);
                var isSelected = IsPatternHighlighted(location);
                var font = isSelected ? Fonts.FontSmallBold : Fonts.FontSmall;

                c.FillRectangleGradient(1, 1, width, patternHeaderSizeY, pattern.Color, pattern.Color.Scaled(0.8f), true, patternHeaderSizeY);
                c.FillRectangle(1, patternHeaderSizeY, width, height, Color.FromArgb(75, pattern.Color));
                c.DrawLine(0, patternHeaderSizeY, width, patternHeaderSizeY, Theme.BlackColor);
                c.DrawTexture(bmp, 1.0f, 1.0f + patternHeaderSizeY, width - 1, patternCacheSizeY, u0, v0, u1, v1);

                if (isSelected)
                {
                    c.FillRectangle(0, 0, width, height, Color.FromArgb(48, Theme.WhiteColor));
                    c.DrawRectangle(0, 0, width, height, Theme.WhiteColor, 3, true, true);
                    c.DrawLine(0, patternHeaderSizeY, width, patternHeaderSizeY, Theme.WhiteColor, 3, true, true);
                    c.DrawText(pattern.Name, font, patternNamePosX + 1, 1, Theme.BlackColor, TextFlags.Left | TextFlags.Middle | TextFlags.Clip, width - patternNamePosX, patternHeaderSizeY + 1);
                }

                c.DrawText(pattern.Name, font, patternNamePosX, 0, isSelected ? Theme.WhiteColor : Theme.BlackColor, TextFlags.Left | TextFlags.Middle | TextFlags.Clip, width - patternNamePosX, patternHeaderSizeY + 1);

                if (!Sequencer.HasCaptureOperation && Sequencer.IsPatternSelected(location) && Song.IsPatternShared(Song.GetPatternInstance(location)))
                {
                    // TODO: The icon seems too big for the smallest channelSizeY.
                    f.DrawTextureAtlas(bmpMenuInstance, iconX + 1, iconY + 1, bitmapScale, Theme.BlackColor);
                    f.DrawTextureAtlas(bmpMenuInstance, iconX, iconY, bitmapScale, Theme.WhiteColor);
                }
            }
        }
    }
}