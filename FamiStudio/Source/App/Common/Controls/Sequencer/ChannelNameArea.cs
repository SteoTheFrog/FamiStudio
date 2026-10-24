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
        private int hoverIndex = -1;
        private int selectedIndex = -1;
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
                    var idx = i; // Need to copy for channel index.

                    channelName.Clicked += () => ChannelName_Clicked(idx);
                    channelName.ChannelIcon += () => ChannelIcon_Clicked(idx);
                    channelName.ChannelIconDoubleClicked += () => ChannelIcon_DoubleClicked(idx);
                    channelName.ForceDisplayClicked += () => ForceDisplay_Clicked(idx);
                    channelName.ForceDisplayDoubleClicked += () => ForceDisplay_DoubleClicked(idx);

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
            SetSelectedChannel(App.SelectedChannelIndex);
            SetHover(hoverIndex);
        }

        private void ChannelName_Clicked(int idx)
        {
            SetSelectedChannel(idx);
            App.SelectedChannelIndex = idx;
        }

        private void ChannelIcon_Clicked(int idx)
        {
            App.ToggleChannelActive(idx);
        }

        private void ChannelIcon_DoubleClicked(int idx)
        {
            App.ToggleChannelSolo(idx, true);
        }

        private void ForceDisplay_Clicked(int idx)
        {
            App.ToggleChannelForceDisplay(idx);
        }

        private void ForceDisplay_DoubleClicked(int idx)
        {
            App.ToggleChannelForceDisplayAll(idx, true);
        }

        public void SetHover(int idx)
        {
            if (hoverIndex != idx)
            {
                SetChannelHover(hoverIndex, false);
                hoverIndex = idx;
                SetChannelHover(hoverIndex, true);
            }
        }

        public void SetSelectedChannel(int idx)
        {
            if (selectedIndex != idx)
            {
                SetChannelSelection(selectedIndex, false);
                selectedIndex = idx;
                SetChannelSelection(selectedIndex, true);
            }
        }

        private void SetChannelHover(int idx, bool hover)
        {
            if (idx >= 0 && idx < channelNames.Length)
                channelNames[idx].SetHovered(hover);
        }

        private void SetChannelSelection(int idx, bool select)
        {
            if (idx >= 0 && idx < channelNames.Length)
                channelNames[idx].SetSelected(select);
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