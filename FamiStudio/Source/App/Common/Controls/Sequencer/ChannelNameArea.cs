using System;

namespace FamiStudio
{
    public class ChannelNameArea : Container
    {
        private Container headerContainer;
        private Container namesContainer;
        private Button shyButton;
        private bool forceShyOff = false;
        private bool hideEmptyChannels = false;
        private int lastExpansionMask = -1;
        private float headerIconScale = Platform.IsMobile ? DpiScaling.ScaleForWindowFloat(0.5f)  : 1.0f;

        private ChannelNamePanel[] channelNames;

        private Sequencer Sequencer => App?.Sequencer;
        private Song Song => App?.SelectedSong;

        LocalizedString ShyModeTooltip;

        public ChannelNameArea()
        {
            Localization.Localize(this);
        }

        protected override void OnAddedToContainer()
        {
            headerContainer = new Container();
            headerContainer.SetupClipRegion(false);
            headerContainer.Rendering += HeaderContainer_Rendering;
            AddControl(headerContainer);

            shyButton = new Button("ShyOff");
            shyButton.Transparent = true;
            shyButton.ImageScale = headerIconScale;
            shyButton.ToolTip = $"<MouseLeft> {ShyModeTooltip}";
            shyButton.ImageEvent += ShyButton_ImageEvent;
            shyButton.Click += ShyButton_Clicked;
            headerContainer.AddControl(shyButton);

            namesContainer = new Container();
            namesContainer.Rendering += NamesContainer_Rendering;
            AddControl(namesContainer);
        }

        private void ConditionalRecreateChannelNames()
        {
            var expMask = App.Project.ExpansionAudioMask;
            if (expMask != lastExpansionMask)
            {
                ConditionalRemoveChannelNames();

                channelNames = new ChannelNamePanel[Song.Channels.Length];

                for (var i = 0; i < channelNames.Length; i++)
                {
                    var channelName = new ChannelNamePanel(Song.Channels[i]);
                    channelNames[i] = channelName;
                    namesContainer.AddControl(channelName);
                }
            }

            lastExpansionMask = expMask;
        }

        private void ConditionalRemoveChannelNames()
        {
            if (channelNames != null)
            {
                foreach (var c in channelNames)
                    namesContainer.RemoveControl(c);
            }
        }

        public void Reset()
        {
            Refresh();
        }

        public void Refresh()
        {
            ConditionalRecreateChannelNames();
        }

        private int GetChannelToRowForIndex(int idx)
        {
            return Sequencer.GetChannelToRowForIndex(idx);
        }

        private void SetHideEmptyChannels(bool hide)
        {
            Sequencer.SetHideEmptyChannels(hide);
            hideEmptyChannels = hide;
        }

        private void LayoutChanged()
        {
            window.RefreshLayout();
        }

        public void UpdateLayout(int headerSizeY, int channelSizeY)
        {
            if (Song == null)
                return;

            headerContainer.Move(0, 0, width, headerSizeY);
            shyButton.Move(width - headerSizeY, 0, headerSizeY, headerSizeY);

            var rowCount = 0;

            for (var i = 0; i < channelNames.Length; i++)
            {
                var channelName = channelNames[i];
                var rowIdx = GetChannelToRowForIndex(i);

                if (rowIdx < 0)
                {
                    channelName.Visible = false;
                    continue;
                }

                rowCount = Math.Max(rowCount, rowIdx + 1);

                channelName.Move(0, rowIdx * channelSizeY, width, channelSizeY);
                channelName.Visible = true;
            }

            namesContainer.Visible = rowCount > 0;
            namesContainer.Move(0, headerSizeY, width, height - headerSizeY);
        }

        private string ShyButton_ImageEvent(Control sender, ref Color tint)
        {
            tint = Theme.LightGreyColor2;
            return hideEmptyChannels && !forceShyOff ? "ShyOn" : "ShyOff";
        }

        private void ShyButton_Clicked(Control sender)
        {
            SetHideEmptyChannels(!hideEmptyChannels);
            LayoutChanged();
            SetTickEnabled(hideEmptyChannels && !namesContainer.Visible);
        }

        public void SetScrollY(int scrollY)
        {
            if (namesContainer.ScrollY != scrollY)
            {
                namesContainer.ScrollY = scrollY;
                MarkDirty();
            }
        }

        public override void Tick(float delta)
        {
            UpdateShyIcon();
        }

        private void UpdateShyIcon()
        {
            if (hideEmptyChannels && !namesContainer.Visible)
            {
                SetAndMarkDirty(ref forceShyOff, Utils.Frac(Platform.TimeSeconds()) < 0.25f);
            }
            else
            {
                forceShyOff = false;
            }
        }

        private void HeaderContainer_Rendering(Graphics g)
        {
            g.ForegroundCommandList.DrawRectangle(0, 0, headerContainer.Width - 1, headerContainer.Height, Theme.BlackColor);
        }

        private void NamesContainer_Rendering(Graphics g)
        {
            g.ForegroundCommandList.DrawRectangle(0, 0, namesContainer.Width - 1, namesContainer.Height - 1, Theme.BlackColor);
        }
    }
}