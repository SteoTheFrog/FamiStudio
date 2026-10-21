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
        private int lastExpansionMask = -1;
        private float headerIconScale = Platform.IsMobile ? DpiScaling.ScaleForWindowFloat(0.5f)  : 1.0f;

        private ChannelName[] channelNames;

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
            AddControl(headerContainer);

            shyButton = new Button("ShyOff");
            shyButton.Transparent = true;
            shyButton.ImageScale = headerIconScale;
            shyButton.ToolTip = $"<MouseLeft> {ShyModeTooltip}";
            shyButton.ImageEvent += ShyButton_ImageEvent;
            shyButton.Click += ShyButton_Clicked;
            headerContainer.AddControl(shyButton);

            namesContainer = new Container();
            AddControl(namesContainer);
        }

        private void ConditionalRecreateChannelNames()
        {
            var expMask = App.Project.ExpansionAudioMask;
            if (expMask != lastExpansionMask)
            {
                ConditionalRemoveChannelNames();

                channelNames = new ChannelName[Song.Channels.Length];

                for (var i = 0; i < channelNames.Length; i++)
                {
                    var channelName = new ChannelName(i);
                    var idx = i; // Need to copy for channel index.

                    channelName.Clicked += () => ChannelName_Clicked(idx);
                    channelName.ChannelIcon += () => ChannelIcon_Clicked(idx);
                    channelName.ChannelIconDoubleClicked += () => ChannelIcon_DoubleClicked(idx);
                    channelName.ForceDisplayClicked += () => ForceDisplay_Clicked(idx);
                    channelName.ForceDisplayDoubleClicked += () => ForceDisplay_DoubleClicked(idx);

                    channelNames[i] = channelName;
                    namesContainer.AddControl(channelName);

                    SetChannelHover(hoverIndex, true);
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
            ConditionalRecreateChannelNames();
        }

        public void Refresh()
        {
            ConditionalRecreateChannelNames();
        }

        private void ChannelName_Clicked(int idx)
        {
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

        private void SetChannelHover(int idx, bool hover)
        {
            if (idx >= 0 && idx < channelNames.Length)
                channelNames[idx].Hovered = hover;
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

            headerContainer.Move(0, 0);
            headerContainer.Resize(width, headerSizeY);

            shyButton.Move(Width - shyButton.Width, 0);
            shyButton.Resize(headerSizeY, headerSizeY);

            var maxRow = -1;

            for (var i = 0; i < channelNames.Length; i++)
            {
                var channelName = channelNames[i];
                var rowIdx = GetChannelToRowForIndex(i);

                if (rowIdx < 0)
                {
                    channelName.Visible = false;
                    channelName.Hovered = false;
                    continue;
                }

                maxRow = Math.Max(maxRow, rowIdx);

                channelName.Visible = true;

                channelName.Resize(Width, channelSizeY);
                channelName.Move(0, rowIdx * channelSizeY);
            }

            var rowCount = maxRow + 1;
            var namesHeight = rowCount * channelSizeY;

            namesContainer.Visible = rowCount > 0;
            namesContainer.Move(0, headerSizeY);
            namesContainer.Resize(width, namesHeight);
            Resize(width, namesHeight + headerSizeY + 1);
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

        protected override void OnRender(Graphics g)
        {
            var c = g.DefaultCommandList;

            // Background
            c.FillAndDrawRectangle(0, 0, width, height, Theme.DarkGreyColor1, Theme.BlackColor);

            // Ensure we don't draw over the outlines.
            c.PushClipRegion(1, 1, width - 1, height - 1);

            // Bottom line.
            c.DrawLine(0, height - 1, width, height - 1, Theme.BlackColor);

            base.OnRender(g);

            c.PopClipRegion();
            
            // Vertical line seperating with the toolbar
            if (Platform.IsMobile && IsLandscape)
                c.DrawLine(0, 0, 0, height, Theme.BlackColor);
        }
    }
}