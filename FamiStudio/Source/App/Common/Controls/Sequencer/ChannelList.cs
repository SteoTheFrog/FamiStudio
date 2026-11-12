using System;
using System.Collections.Generic;

namespace FamiStudio
{
    public partial class Sequencer : Container
    {
        private class ChannelList : Container
        {
            private static Sequencer sequencer;
            private Container headerContainer;
            private Container namesContainer;
            private Button shyButton;
            private bool forceShyOff = false;
            private bool hideEmptyChannels = false;
            private int lastExpansionMask = -1;
            private float headerIconScale = Platform.IsMobile ? DpiScaling.ScaleForWindowFloat(0.5f)  : 1.0f;
            private ChannelListPanel[] channelNames;

            private Song Song => App?.SelectedSong;

            LocalizedString ShyModeTooltip;

            public ChannelList(Sequencer seq)
            {
                Localization.Localize(this);
                sequencer = seq;
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

                    channelNames = new ChannelListPanel[Song.Channels.Length];

                    for (var i = 0; i < channelNames.Length; i++)
                    {
                        var channelName = new ChannelListPanel(Song.Channels[i]);
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
                return sequencer.GetChannelToRowForIndex(idx);
            }

            private void SetHideEmptyChannels(bool hide)
            {
                sequencer.SetHideEmptyChannels(hide);
                hideEmptyChannels = hide;
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
                sequencer.ShyChanged?.Invoke();
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

            private class ChannelListPanel : Container
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

                private Channel channel;
                private int channelNameIndex;
                private int channelIconPosX;
                private int channelIconPosY;
                private int channelNamePosX;
                private int channelIconSize;
                private int ghostIconSize;
                private float iconScale = Platform.IsMobile ? DpiScaling.Window * 0.25f : 1.0f;

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

                public ChannelListPanel(Channel channel)
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
                    App.ToggleChannelActive(channelNameIndex);
                }

                private void ForceDisplay_Click(Control control)
                {
                    App.ToggleChannelForceDisplay(channelNameIndex);
                }

                private void ToggleMute_DoubleClick(Control control, PointerEventArgs e)
                {
                    App.ToggleChannelSolo(channelNameIndex, true);
                }

                private void ForceDisplay_DoubleClick(Control control, PointerEventArgs e)
                {
                    App.ToggleChannelForceDisplayAll(channelNameIndex, true);
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
                    var channelShortcut = channelNameIndex >= 0 && channelNameIndex < Settings.ActiveChannelShortcuts.Length ? $" {Settings.ActiveChannelShortcuts[channelNameIndex].TooltipString}" : "";
                    var displayShortcut = channelNameIndex >= 0 && channelNameIndex < Settings.DisplayChannelShortcuts.Length ? $" {Settings.DisplayChannelShortcuts[channelNameIndex].TooltipString}" : "";

                    UpdateRenderCoords();

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
                }

                protected override void OnPointerDown(PointerEventArgs e)
                {
                    if (e.Left)
                    {
                        App.SelectedChannelIndex = channelNameIndex;
                    }
                }

                protected override void OnPointerUp(PointerEventArgs e)
                {
                    if (e.Right)
                    {
                        ShowContextMenu(this);
                    }
                }

                private Color GetBackgroundColor()
                {
                    var hovered = sequencer.hoverPos.Y == App.Sequencer.GetChannelToRowForIndex(channelNameIndex);
                    var dim = Settings.DimUnsupportedChannels && !channel.SupportsInstrument(App.SelectedInstrument, false);

                    return hovered ? Theme.MediumGreyColor1.Transparent(dim ? 192 : 255) : dim ? Theme.DarkGreyColor1 : Theme.DarkGreyColor2;
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
                    var c = g.DefaultCommandList;
                    var selected = App.SelectedChannelIndex == channelNameIndex;

                    c.FillAndDrawRectangle(0, 0, width, height, GetBackgroundColor(), Theme.BlackColor);
                    c.DrawText(channel.LocalizedName, selected ? fonts.FontMediumBold : fonts.FontMedium, channelNamePosX, channelIconPosY, Theme.LightGreyColor2, TextFlags.MiddleLeft, 0, channelIconButton.Height);

                    base.OnRender(g);
                }
            }
        }
    }
}