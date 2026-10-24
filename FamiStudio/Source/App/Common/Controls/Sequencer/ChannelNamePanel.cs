using System;
using System.Collections.Generic;

namespace FamiStudio
{
    public class ChannelNamePanel : Container
    {
        const int DefaultChannelIconPosX  = 2;
        const int DefaultChannelIconPosY  = 3;
        const int DefaultChannelIconSize  = 16;
        const int DefaultChannelNamePosX  = 21;
        const int DefaultGhostIconSize    = 12;
        const int DefaultGhostNoteOffsetX = 16;
        const int DefaultGhostNoteOffsetY = Platform.IsMobile ? 16 : 15;

        private Button channelIconButton;
        private Button forceDisplayButton;
        private Label channelNameLabel;

        private Channel channel;
        private Color backgroundColor;
        private int channelNameIndex;
        private int channelIconPosX;
        private int channelIconPosY;
        private int channelNamePosX;
        private int channelIconSize;
        private int ghostIconSize;
        private float iconScale = Platform.IsMobile ? DpiScaling.Window * 0.25f : 1.0f;

        public delegate void EmptyDelegate();
        public delegate void SelectionDelegate(int idx);

        public EmptyDelegate Clicked;
        public EmptyDelegate ChannelIcon;
        public EmptyDelegate ChannelIconDoubleClicked;
        public EmptyDelegate ForceDisplayClicked;
        public EmptyDelegate ForceDisplayDoubleClicked;
        public EmptyDelegate PointerEntered;
        public SelectionDelegate SelectionChanged;

        // Buttons
        LocalizedString MuteChannelTooltip;
        LocalizedString SoloChannelTooltip;
        LocalizedString ForceDisplayTooltip;
        LocalizedString ForceDisplayAllChannelsTooltip;
        LocalizedString MakeActiveTooltip;

        // Context Menus
        LocalizedString ToggleMuteLabel;
        LocalizedString ToggleSoloLabel;
        LocalizedString ForceDisplayLabel;

        public ChannelNamePanel(Channel channel)
        {
            Localization.Localize(this);
            channelNameIndex = channel.Index;
            clipRegion = false;
            this.channel = channel;
        }

        private Button CreateImageButton(int x, int y, string image)
        {
            var button = new Button(image, null)
            {
                Transparent = true,
                ImageScale = iconScale,
            };

            AddControl(button);
            button.AutoSizeToImage();
            button.Move(x, y);
            button.RightClick += ShowContextMenu;
            button.ClickOnMouseUp = true;
            button.SupportsDoubleClick = true;

            return button;
        }

        private void ToggleMute_Click(Control control)
        {
            ChannelIcon?.Invoke();
            control.MarkDirty();
        }

        private void ForceDisplay_Click(Control control)
        {
            ForceDisplayClicked?.Invoke();
            control.MarkDirty();
        }

        private void ToggleMute_DoubleClick(Control control, PointerEventArgs e)
        {
            ChannelIconDoubleClicked?.Invoke();
            control.MarkDirty();
        }

        private void ForceDisplay_DoubleClick(Control control, PointerEventArgs e)
        {
            ForceDisplayDoubleClicked?.Invoke();
            control.MarkDirty();
        }

        private void ShowContextMenu(Control sender)
        {
            var menu = new List<ContextMenuOption>();

            // Only show toggle mute / solo if we clicked the mute button or main control.
            if (sender == channelIconButton || sender == this)
            {
                menu.Add(new ContextMenuOption("MenuMute", ToggleMuteLabel, () => { App.ToggleChannelActive(channelNameIndex); }));
                menu.Add(new ContextMenuOption("MenuSolo", ToggleSoloLabel, () => { App.ToggleChannelSolo(channelNameIndex); }));
            }

            // Likewise, only show force display if we click its button or the main control.
            if (sender == forceDisplayButton || sender == this)
                menu.Add(new ContextMenuOption("MenuForceDisplay", ForceDisplayLabel, () => { App.ToggleChannelForceDisplay(channelNameIndex); }));

            if (menu.Count > 0)
                App.ShowContextMenuAsync(menu.ToArray());

            MarkDirty();
        }

        public void SetHovered(bool hover)
        {
            UpdateBackground(hover);
            MarkDirty();
        }

        public void SetSelected(bool select)
        {
            channelNameLabel.Font = select ? Fonts.FontMediumBold : Fonts.FontMedium;
            MarkDirty();
        }

        private void UpdateBackground(bool hover)
        {
            var dim = Settings.DimUnsupportedChannels && !channel.SupportsInstrument(App.SelectedInstrument, false);
            backgroundColor = hover ? Theme.MediumGreyColor1.Transparent(dim ? 192 : 255) : dim ? Theme.DarkGreyColor1 : Theme.DarkGreyColor2;
        }

        private void UpdateRenderCoords()
        {
            channelIconPosX = DpiScaling.ScaleForWindow(DefaultChannelIconPosX);
            channelIconPosY = DpiScaling.ScaleForWindow(DefaultChannelIconPosY);
            channelNamePosX = DpiScaling.ScaleForWindow(DefaultChannelNamePosX);
            channelIconSize = DpiScaling.ScaleForWindow(DefaultChannelIconSize);
            ghostIconSize   = DpiScaling.ScaleForWindow(DefaultGhostIconSize);

            channelIconButton?.Move(DpiScaling.ScaleForWindow(DefaultChannelIconPosX), DpiScaling.ScaleForWindow(DefaultChannelIconPosY));
            channelIconButton?.Resize(channelIconSize, channelIconSize);
            forceDisplayButton?.Move(width - DpiScaling.ScaleForWindow(DefaultGhostNoteOffsetX), height - DpiScaling.ScaleForWindow(DefaultGhostNoteOffsetY) - 1);
            forceDisplayButton?.Resize(ghostIconSize, ghostIconSize);
        }

        protected override void OnAddedToContainer()
        {
            //c.DrawText(song.Channels[nameIndex].LocalizedName, font, channelNamePosX, channelIconPosY, Theme.LightGreyColor2.Transparent(dim ? 80 : 255), TextFlags.MiddleLeft, 0, channelIconButton.Height);

            var channelShortcut = channelNameIndex >= 0 && channelNameIndex < Settings.ActiveChannelShortcuts.Length ? $" {Settings.ActiveChannelShortcuts[channelNameIndex].TooltipString}" : "";
            var displayShortcut = channelNameIndex >= 0 && channelNameIndex < Settings.DisplayChannelShortcuts.Length ? $" {Settings.DisplayChannelShortcuts[channelNameIndex].TooltipString}" : "";

            UpdateRenderCoords();

            channelNameLabel = new Label(channel.LocalizedName, false);
            channelNameLabel.PointerDown += (s, e) => OnPointerDown(e);
            channelNameLabel.Move(channelNamePosX, 0);
            AddControl(channelNameLabel);

            tooltip = $"<MouseLeft> {MakeActiveTooltip}{channelShortcut}";

            channelIconButton = CreateImageButton(channelIconPosX, channelIconPosY, ChannelType.Icons[channel.Type]);
            channelIconButton.ToolTip = $"<MouseLeft> {MuteChannelTooltip} - <MouseLeft><MouseLeft> {SoloChannelTooltip}";
            channelIconButton.ImageEvent += ToggleMuteButton_ImageEvent;
            channelIconButton.Click += ToggleMute_Click;
            channelIconButton.MouseDoubleClick += ToggleMute_DoubleClick;
            channelIconButton.TouchDoubleClick += ToggleMute_DoubleClick;

            forceDisplayButton = CreateImageButton(width - ghostIconSize, height - ghostIconSize, "GhostSmall");
            forceDisplayButton.ToolTip = $"<MouseLeft> {ForceDisplayTooltip}\n<MouseLeft><MouseLeft> {ForceDisplayAllChannelsTooltip}{displayShortcut}";
            forceDisplayButton.DimmedEvent += ForceDisplayButton_DimmedEvent;
            forceDisplayButton.Click += ForceDisplay_Click;
            forceDisplayButton.MouseDoubleClick += ForceDisplay_DoubleClick;
            forceDisplayButton.TouchDoubleClick += ForceDisplay_DoubleClick;

            UpdateBackground(false);
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            if (e.Left)
            {
                SelectionChanged?.Invoke(channelNameIndex);
                Clicked?.Invoke();
            }
        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            if (e.Right)
            {
                ShowContextMenu(this);
            }
        }

        private string ToggleMuteButton_ImageEvent(Control sender, ref Color tint)
        {
            var showExp = App.SequencerShowExpansionIcons;
            var opacity = (App.ChannelMask & (1L << channelNameIndex)) != 0 ? 255 : 50;

            tint = Theme.LightGreyColor1.Transparent(opacity);

            return showExp ? ExpansionType.Icons[channel.Expansion] : ChannelType.Icons[channel.Type];
        }

        private bool ForceDisplayButton_DimmedEvent(Control sender, ref int dimming)
        {
            dimming = 50;
            return (App.ForceDisplayChannelMask & (1L << channelNameIndex)) == 0;
        }

        protected override void OnResize(EventArgs e)
        {
            UpdateRenderCoords();
        }

        protected override void OnRender(Graphics g)
        {
            g.DefaultCommandList.FillAndDrawRectangle(0, 0, width, height, backgroundColor, Theme.BlackColor);
            base.OnRender(g);
        }
    }
}