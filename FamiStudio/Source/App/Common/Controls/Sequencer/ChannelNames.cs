using System;
using System.Collections.Generic;

namespace FamiStudio
{
    public class ChannelNames : Container
    {
        private Container headerContainer;
        private Container namesContainer;
        private Button shyButton;
        private bool forceShyOff = false;
        private bool hideEmptyChannels = false;
        private int hoverIndex = -1;
        private int lastExpansionMask = -1;
        //private int scrollBarThickness; // TODO: Check if we even need this. We likely won't have scrollbars in the channel name area itself.
        private float headerIconScale = Platform.IsMobile ? DpiScaling.ScaleForWindowFloat(0.5f)  : 1.0f;

        private ChannelName[] channelNames;

        private Sequencer Sequencer => App?.Sequencer;
        private Song Song => App?.SelectedSong;

        public delegate void EmptyDelegate();

        LocalizedString ShyModeTooltip;

        public ChannelNames()
        {
            Localization.Localize(this);
        }

        protected override void OnAddedToContainer()
        {
            headerContainer = new Container();
            namesContainer = new Container();

            AddControl(headerContainer);
            AddControl(namesContainer);

            shyButton = new Button("ShyOff")
            {
                Transparent = true,
                ImageScale = headerIconScale,
                ToolTip = $"<MouseLeft> {ShyModeTooltip}"
            };

            shyButton.ImageEvent += ShyButton_ImageEvent;
            shyButton.Click      += ShyButton_Clicked;

            headerContainer.AddControl(shyButton);
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

            // Outlines.
            c.DrawLine(0, 0, width, 0, Theme.BlackColor);
            c.DrawLine(width - 1, 0, width - 1, height, Theme.BlackColor);

            // Ensure we don't draw over the outlines.
            c.PushClipRegion(0, 1, width - 1, height - 1);

            // Background
            c.FillRectangle(0, 0, width, height, Theme.DarkGreyColor2);

            // Bottom line.
            c.DrawLine(0, height - 1, width, height - 1, Theme.BlackColor);

            base.OnRender(g);

            c.PopClipRegion();
            
            // Vertical line seperating with the toolbar
            if (Platform.IsMobile && IsLandscape)
                c.DrawLine(0, 0, 0, height, Theme.BlackColor);
        }
    }

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

            // Background fill.
            c.FillRectangle(0, 0, width, height, dim ? Theme.DarkGreyColor1 : Theme.DarkGreyColor2);

            // Hover
            if (Platform.IsDesktop && hovered)
                c.FillRectangle(0, 0, width, height, Theme.MediumGreyColor1.Transparent(dim ? 192 : 255));

            // Separator lines.
            c.DrawLine(0, 0, width - 1, 0, Theme.BlackColor);
            
            base.OnRender(g);
        }
    }
}