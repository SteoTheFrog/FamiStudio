using System;
using System.Collections.Generic;

namespace FamiStudio
{
    public class ChannelName : Container
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

        private bool hovered;
        private int nameIndex;
        private int channelIconPosX;
        private int channelIconPosY;
        private int channelNamePosX;
        private float iconScale = Platform.IsMobile ? DpiScaling.ScaleForWindowFloat(0.25f) : 1.0f;

        public delegate void EmptyDelegate();

        public EmptyDelegate Clicked;
        public EmptyDelegate ChannelIcon;
        public EmptyDelegate ChannelIconDoubleClicked;
        public EmptyDelegate ForceDisplayClicked;
        public EmptyDelegate ForceDisplayDoubleClicked;
        public EmptyDelegate PointerEntered;

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

        public bool Hovered 
        { 
            get { return hovered; }
            set 
            { 
                hovered = value; 
                MarkDirty();
            }
        }

        public ChannelName(int idx)
        {
            Localization.Localize(this);
            nameIndex = idx;
        }

        private Button CreateImageButton(int x, string image)
        {
            var button = new Button(image, null)
            {
                Transparent = true,
                ImageScale = iconScale,
            };

            AddControl(button);
            button.AutoSizeToImage();
            button.Move(x, Utils.DivideAndRoundUp(height - button.Height, 2));
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
                menu.Add(new ContextMenuOption("MenuMute", ToggleMuteLabel, () => { App.ToggleChannelActive(nameIndex); }));
                menu.Add(new ContextMenuOption("MenuSolo", ToggleSoloLabel, () => { App.ToggleChannelSolo(nameIndex); }));
            }

            // Likewise, only show force display if we click its button or the main control.
            if (sender == forceDisplayButton || sender == this)
                menu.Add(new ContextMenuOption("MenuForceDisplay", ForceDisplayLabel, () => { App.ToggleChannelForceDisplay(nameIndex); }));

            if (menu.Count > 0)
                App.ShowContextMenuAsync(menu.ToArray());

            MarkDirty();
        }

        protected override void OnAddedToContainer()
        {
            channelIconPosX = DpiScaling.ScaleForWindow(DefaultChannelIconPosX);
            channelIconPosY = DpiScaling.ScaleForWindow(DefaultChannelIconPosY);
            channelNamePosX = DpiScaling.ScaleForWindow(DefaultChannelNamePosX);

            var type = App.SelectedSong.Channels[nameIndex].Type;
            var channelShortcut = nameIndex >= 0 && nameIndex < Settings.ActiveChannelShortcuts.Length ? $" {Settings.ActiveChannelShortcuts[nameIndex].TooltipString}" : "";
            var displayShortcut = nameIndex >= 0 && nameIndex < Settings.DisplayChannelShortcuts.Length ? $" {Settings.DisplayChannelShortcuts[nameIndex].TooltipString}" : "";

            tooltip = $"<MouseLeft> {MakeActiveTooltip}{channelShortcut}";

            channelIconButton = CreateImageButton(channelIconPosX, ChannelType.Icons[type]);
            channelIconButton.ToolTip = $"<MouseLeft> {MuteChannelTooltip} - <MouseLeft><MouseLeft> {SoloChannelTooltip}";
            channelIconButton.ImageEvent += ToggleMuteButton_ImageEvent;
            channelIconButton.Click += ToggleMute_Click;
            channelIconButton.MouseDoubleClick += ToggleMute_DoubleClick;
            channelIconButton.TouchDoubleClick += ToggleMute_DoubleClick;

            forceDisplayButton = CreateImageButton(channelIconPosX + channelIconButton.Width + 2, "GhostSmall");
            forceDisplayButton.ToolTip = $"<MouseLeft> {ForceDisplayTooltip}\n<MouseLeft><MouseLeft> {ForceDisplayAllChannelsTooltip}{displayShortcut}";
            forceDisplayButton.DimmedEvent += ForceDisplayButton_DimmedEvent;
            forceDisplayButton.Click += ForceDisplay_Click;
            forceDisplayButton.MouseDoubleClick += ForceDisplay_DoubleClick;
            forceDisplayButton.TouchDoubleClick += ForceDisplay_DoubleClick;
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            if (e.Left)
            {
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
            var channel = App.SelectedSong.Channels[nameIndex];
            var showExp = App.SequencerShowExpansionIcons;
            var opacity = (App.ChannelMask & (1L << nameIndex)) != 0 ? 255 : 50;

            tint = Theme.LightGreyColor1.Transparent(opacity);

            return showExp ? ExpansionType.Icons[channel.Expansion] : ChannelType.Icons[channel.Type];
        }

        private bool ForceDisplayButton_DimmedEvent(Control sender, ref int dimming)
        {
            dimming = 50;
            return (App.ForceDisplayChannelMask & (1L << nameIndex)) == 0;
        }

        protected override void OnResize(EventArgs e)
        {
            var iconSize  = DpiScaling.ScaleForWindow(DefaultChannelIconSize);
            var ghostSize = DpiScaling.ScaleForWindow(DefaultGhostIconSize);

            channelIconButton?.Move(DpiScaling.ScaleForWindow(DefaultChannelIconPosX), DpiScaling.ScaleForWindow(DefaultChannelIconPosY));
            channelIconButton?.Resize(iconSize, iconSize);
            forceDisplayButton?.Move(width - DpiScaling.ScaleForWindow(DefaultGhostNoteOffsetX), height - DpiScaling.ScaleForWindow(DefaultGhostNoteOffsetY) - 1);
            forceDisplayButton?.Resize(ghostSize, ghostSize);
        }

        protected override void OnRender(Graphics g)
        {
            var c = g.DefaultCommandList;

            // Name
            var selected = App.SelectedChannelIndex == nameIndex;
            var font = selected ? Fonts.FontMediumBold : Fonts.FontMedium;
            var song = App.SelectedSong;
            var selectedSong = App.SelectedSong;
            var selectedInstrument = App.SelectedInstrument;

            // Dim unsupported channels if enabled in settings
            var dim = Settings.DimUnsupportedChannels && !selectedSong.Channels[nameIndex].SupportsInstrument(selectedInstrument, false);

            c.DrawText(song.Channels[nameIndex].LocalizedName, font, channelNamePosX, channelIconPosY, Theme.LightGreyColor2.Transparent(dim ? 80 : 255), TextFlags.MiddleLeft, 0, channelIconButton.Height);

            // Outline and background.
            c.FillAndDrawRectangle(0, 0, width, height, dim ? Theme.DarkGreyColor1 : Theme.DarkGreyColor2, Theme.BlackColor);

            // Hover
            if (Platform.IsDesktop && hovered)
                c.FillRectangle(0, 0, width, height, Theme.MediumGreyColor1.Transparent(dim ? 192 : 255));
            
            base.OnRender(g);
        }
    }
}