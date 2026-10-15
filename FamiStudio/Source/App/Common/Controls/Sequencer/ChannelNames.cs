using System;
using System.Collections.Generic;

namespace FamiStudio
{
    public class ChannelNames : Container
    {
        //private readonly Sequencer sequencer;
        private Container headerContainer;
        private Container namesContainer;
        private Button shyButton;
        private bool forceShyOff = false;
        private bool hideEmptyChannels = false;
        private int scrollBarThickness; // TODO: Check if we even need this. We likely won't have scrollbars in the channel name area itself.
        private int visibleChannelCount;
        private float headerIconScale = Platform.IsMobile ? DpiScaling.ScaleForWindowFloat(0.5f)  : 1.0f;

        private ChannelName[] channelNames;

        private Song Song => App?.SelectedSong;
        private Sequencer Sequencer => App?.Sequencer;
        private int ChannelSizeY => Sequencer.ChannelSizeY;
        private int HeaderSizeY => Sequencer.HeaderSizeY;

        LocalizedString ShyModeTooltip;

        public delegate void HoverChannelDelegate(int idx);
        public event HoverChannelDelegate HoverChannelChanged;

        public ChannelNames(Sequencer sequencer)
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

        private void RecreateChannelNames()
        {
            if (channelNames != null)
            {
                foreach (var c in channelNames)
                    namesContainer.RemoveControl(c);
            }

            channelNames = new ChannelName[Song.Channels.Length];

            for (var i = 0; i < channelNames.Length; i++)
            {
                var channelName = new ChannelName(i);

                channelName.IconClicked += (idx) => { App.ToggleChannelActive(idx); SetHover(idx); };
                channelName.Clicked += (idx) => { App.SelectedChannelIndex = idx; SetHover(idx); };
                channelName.SoloToggled += (idx) => App.ToggleChannelSolo(idx, true);
                channelName.ForceDisplayClicked += (idx) => { App.ToggleChannelForceDisplay(idx); SetHover(idx); };
                channelName.ForceDisplaySoloToggled += (idx) => App.ToggleChannelForceDisplayAll(idx, true);
                channelName.PointerEnteredName += HoverChanged;

                channelNames[i] = channelName;
                namesContainer.AddControl(channelName);
            }
        }

        public void Reset()
        {
        }

        private void HoverChanged(int idx)
        {
            HoverChannelChanged?.Invoke(idx);
        }

        public void SetHover(int idx)
        {
            foreach (var c in channelNames)
                c.Hovered = c.NameIndex == idx;
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
            Sequencer.LayoutChanged();
        }

        public void UpdateLayout()
        {
            headerContainer.Move(0, 0);
            headerContainer.Resize(Width, HeaderSizeY);

            shyButton.Move(Width - shyButton.Width, 0);
            shyButton.Resize(HeaderSizeY, HeaderSizeY);

            RecreateChannelNames();

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

                channelName.Resize(Width, ChannelSizeY);
                channelName.Move(0, rowIdx * ChannelSizeY);
            }

            var rowCount = maxRow + 1;

            namesContainer.Visible = rowCount > 0;
            namesContainer.Move(0, HeaderSizeY);
            namesContainer.Resize(width, rowCount * ChannelSizeY);

            visibleChannelCount = rowCount;
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

        public void SetScroll(int scrollY)
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

        private void ContainerEnterPointerDown(Control control)
        {
            if (control == headerContainer || control.IsInContainer(headerContainer))
                HoverChanged(-1);
        }

        public override void OnContainerPointerEnterNotify(Control control, EventArgs e)
        {
            ContainerEnterPointerDown(control);
        }

        public override void OnContainerPointerDownNotify(Control control, PointerEventArgs e)
        {
            ContainerEnterPointerDown(control);
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
            var bottomHeight = visibleChannelCount > 0 ? height - 1 : HeaderSizeY;
            c.DrawLine(0, bottomHeight, width, bottomHeight, Theme.BlackColor);

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

        private Button toggleMuteButton;
        private Button forceDisplayButton;

        private bool hovered;
        private int nameIndex;
        private int channelIconPosX;
        private int channelIconPosY;
        private int channelNamePosX;
        private float iconScale = Platform.IsMobile ? DpiScaling.ScaleForWindowFloat(0.25f) : 1.0f;

        public delegate void ChannelDelegate(int channelIdx);

        public ChannelDelegate Clicked;
        public ChannelDelegate IconClicked;
        public ChannelDelegate SoloToggled;
        public ChannelDelegate ForceDisplayClicked;
        public ChannelDelegate ForceDisplaySoloToggled;
        public ChannelDelegate PointerEnteredName;

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
    
        public int NameIndex => nameIndex;

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
            button.Move(x, Utils.DivideAndRoundUp(Height - button.Height, 2));
            button.RightClick += ShowContextMenu;
            button.MouseDoubleClick += HandleButtonDoubleClick;
            button.TouchDoubleClick += HandleButtonDoubleClick;
            button.ClickOnMouseUp = true;
            button.SupportsDoubleClick = true;

            return button;
        }

        private void HandleButtonDoubleClick(Control control, PointerEventArgs e)
        {
            if (control == toggleMuteButton)
                SoloToggled?.Invoke(nameIndex);
            else if (control == forceDisplayButton)
                ForceDisplaySoloToggled?.Invoke(nameIndex);

            MarkDirty();
        }

        private void ShowContextMenu(Control sender)
        {
            var menu = new List<ContextMenuOption>();

            // Only show toggle mute / solo if we clicked the mute button or main control.
            if (sender == toggleMuteButton || sender == this)
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

            toggleMuteButton = CreateImageButton(channelIconPosX, ChannelType.Icons[type]);
            forceDisplayButton = CreateImageButton(channelIconPosX + toggleMuteButton.Width + 2, "GhostSmall");

            toggleMuteButton.ToolTip = $"<MouseLeft> {MuteChannelTooltip} - <MouseLeft><MouseLeft> {SoloChannelTooltip}";
            forceDisplayButton.ToolTip = $"<MouseLeft> {ForceDisplayTooltip}\n<MouseLeft><MouseLeft> {ForceDisplayAllChannelsTooltip}{displayShortcut}";

            toggleMuteButton.ImageEvent += ToggleMuteButton_ImageEvent;
            forceDisplayButton.DimmedEvent += ForceDisplayButton_DimmedEvent;

            toggleMuteButton.Click += (c) => { IconClicked?.Invoke(nameIndex); };
            forceDisplayButton.Click += (c) => { ForceDisplayClicked?.Invoke(nameIndex); };
        }

        public override void OnContainerPointerDownNotify(Control control, PointerEventArgs e) 
        {
            // Ensure the hover index updates (in case we exited a context menu).
            PointerEnteredName?.Invoke(nameIndex);
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            if (e.Left)
            {
                Clicked?.Invoke(nameIndex);
            }

            // Ensure the hover index updates (in case we exited a context menu).
            PointerEnteredName?.Invoke(nameIndex);
        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            if (e.Right)
            {
                ShowContextMenu(this);
            }
        }

        protected override void OnPointerEnter(EventArgs e)
        {
            PointerEnteredName?.Invoke(nameIndex);
            MarkDirty();
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

            toggleMuteButton?.Move(DpiScaling.ScaleForWindow(DefaultChannelIconPosX), DpiScaling.ScaleForWindow(DefaultChannelIconPosY));
            toggleMuteButton?.Resize(iconSize, iconSize);
            forceDisplayButton?.Move(Width - DpiScaling.ScaleForWindow(DefaultGhostNoteOffsetX), Height - DpiScaling.ScaleForWindow(DefaultGhostNoteOffsetY) - 1);
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

            c.DrawText(song.Channels[nameIndex].LocalizedName, font, channelNamePosX, channelIconPosY, Theme.LightGreyColor2.Transparent(dim ? 80 : 255), TextFlags.MiddleLeft, 0, toggleMuteButton.Height);

            // Background fill.
            c.FillRectangle(0, 0, width, height, dim ? Theme.DarkGreyColor1 : Theme.DarkGreyColor2);

            // Hover
            if (Platform.IsDesktop && hovered)
                c.FillRectangle(0, 0, width, height, Theme.MediumGreyColor1.Transparent(dim ? 192 : 255));

            // Separator lines.
            c.DrawLine(0, 0, width - 1, 0, Theme.BlackColor);
            //c.DrawLine(0, height - 1, width, height - 1, Theme.BlackColor);
            
            base.OnRender(g);
        }
    }
}