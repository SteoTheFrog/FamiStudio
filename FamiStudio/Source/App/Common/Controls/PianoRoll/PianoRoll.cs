using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;

namespace FamiStudio
{
    // This and the Sequencer are the only 2 "Uber-Control" left, where everything is in one class with 
    // no sub-widgets whatsoever. Both of these need a full rewrite. This is for historical reason, when
    // the app started, we didnt have a proper widget system, we sort-of do now. Also, all the touch and
    // mouse input would need to be unified as much as possible.
    //
    // The piano roll would need to be broken down into a timeline, a note area, a piano and things like
    // the envelope editor/DPCM editor need to be pulled out and made into their own editors.
    public class PianoRoll : Container
    {
        const float MinZoomFamiStudio       = 1.0f / 32.0f;
        const float MinZoomOther            = 1.0f / 8.0f;
        const float MaxZoom                 = 16.0f;
        const float MinZoomY                = 0.25f;
        const float MaxZoomY                = 4.0f;
        const float MaxWaveZoom             = 256.0f;
        const float DefaultChannelZoom      = MinZoomOther;
        const float DefaultZoomWaveTime     = 0.25f;
        const float ScrollSpeedFactor       = Platform.IsMobile ? 2.0f : 1.0f;

        const int NumOctaves = 8;
        internal const int NumNotes   = NumOctaves * 12;

        const int DefaultHeaderSizeY               = 17;
        const int DefaultEffectPanelSizeY          = 176;
        const int DefaultEffectButtonSizeY         = 18;
        const int DefaultNoteSizeX                 = 16;
        const int DefaultNoteSizeY                 = 12;
        const int DefaultNoteAttackSizeX           = 3;
        const int DefaultReleaseNoteSizeY          = 8;
        const int DefaultEnvelopeSizeY             = Platform.IsMobile ? 4 : 9;
        const int DefaultPianoSizeX                = 94;
        const int DefaultPianoSizeXMobile          = 44;
        const int DefaultWhiteKeySizeY             = 20;
        const int DefaultBlackKeySizeX             = 56;
        const int DefaultBlackKeySizeXMobile       = 20;
        const int DefaultBlackKeySizeY             = 14;
        const int DefaultSnapIconPosX              = 3;
        const int DefaultSnapIconPosY              = 3;
        const int DefaultSnapIconDpcmPosX          = 0;
        const int DefaultSnapIconDpcmPosY          = 0;
        const int DefaultEffectIconPosX            = 2;
        const int DefaultEffectIconPosY            = 2;
        const int DefaultEffectNamePosX            = 17;
        const int DefaultEffectValuePosTextOffsetY = 13;
        const int DefaultEffectValueNegTextOffsetY = 2;
        const int DefaultBigTextPosX               = 10;
        const int DefaultBigTextPosY               = 10;
        const int DefaultTooltipTextPosX           = 10;
        const int DefaultTooltipTextPosY           = 30;
        const int DefaultEffectPanelTextPosX       = 10;
        const int DefaultEffectPanelTextPosY       = 10;
        const int DefaultDPCMTextPosX              = 2;
        const int DefaultAttackIconPosX            = 1;
        const int DefaultScrollBarThickness1       = 10;
        const int DefaultScrollBarThickness2       = 16;
        const int DefaultMinScrollBarLength        = 64;
        const int DefaultNoteResizeMargin          = 8;
        const int DefaultMinPixelDistForLines      = 5;
        const int DefaultGizmoSize                 = 20;

        int headerSizeY;
        int headerAndEffectSizeY;
        int effectPanelSizeY;
        int effectButtonSizeY;
        int noteAttackSizeX;
        int releaseNoteSizeY;
        int whiteKeySizeY;
        int pianoSizeX;
        int blackKeySizeY;
        int blackKeySizeX;
        int effectIconPosX;
        int effectIconPosY;
        int headerIconsPosX;
        int headerIconsPosY;
        int effectNamePosX;
        int effectValuePosTextOffsetY;
        int effectValueNegTextOffsetY;
        int bigTextPosX;
        int bigTextPosY;
        int tooltipTextPosX;
        int tooltipTextPosY;
        int effectPanelTextPosX;
        int effectPanelTextPosY;
        int dpcmTextPosX;
        int octaveSizeY;
        int virtualSizeY;
        int scrollBarThickness;
        int minScrollBarLength;
        int attackIconPosX;
        int scrollMargin;
        int noteResizeMargin;
        int geometryNoteSizeY;
        int fontSmallCharSizeX;
        int minPixelDistForLines;
        int gizmoSize;
        float minZoom;
        float maxZoom;
        float envelopeValueSizeY;
        float noteSizeX;
        int noteSizeY;

        float effectBitmapScale = 1.0f;
        float bitmapScale = 1.0f;

        internal enum EditionMode
        {
            Channel,
            Envelope,
            DPCM,
            DPCMMapping,
            Arpeggio,
            VideoRecording
        };

        Color selectionBgVisibleColor      = Color.FromArgb( 64, Theme.LightGreyColor1);
        Color selectionBgInvisibleColor    = Color.FromArgb( 16, Theme.LightGreyColor1);
        Color selectionHighlightColor      = Color.FromArgb(128, Theme.WhiteColor);
        Color attackColor                  = Color.FromArgb(128, Theme.BlackColor);
        Color attackBrushForceDisplayColor = Color.FromArgb( 64, Theme.BlackColor);
        Color volumeSlideBarFillColor      = Color.FromArgb( 64, Theme.LightGreyColor1);

        TextureAtlasRef bmpExpandedSmall;
        TextureAtlasRef bmpSnap;
        TextureAtlasRef bmpGizmoResizeLeftRight;
        TextureAtlasRef bmpGizmoResizeUpDown;
        TextureAtlasRef bmpGizmoResizeFill;
        TextureAtlasRef bmpEffectFrame;
        TextureAtlasRef bmpEffectRepeat;
        TextureAtlasRef[] bmpEffects;
        float[][] stopNoteGeometry        = new float[2][]; // [1] is used to draw arps.
        float[][] stopReleaseNoteGeometry = new float[2][]; // [1] is used to draw arps.
        float[][] releaseNoteGeometry     = new float[2][]; // [1] is used to draw arps.
        float[]   slideNoteGeometry;
        float[]   mobileEraseGeometry;

        internal enum CaptureOperation
        {
            None,
            ResizeEnvelope,
            DragLoop,
            DragRelease,
            ChangeEffectValue,
            ChangeSelectionEffectValue,
            ChangeEnvelopeRepeatValue,
            DrawEnvelope,
            Select,
            SelectWave,
            CreateNote,
            CreateSlideNote,
            DeleteNotes,
            DragSlideNoteTarget,
            DragSlideNoteTargetGizmo,
            DragVolumeSlideTarget,
            DragVolumeSlideTargetGizmo,
            DragNote,
            DragSelection,
            AltZoom,
            DragSample,
            DragSeekBar,
            DragWaveVolumeEnvelope,
            ScrollBarX,
            ScrollBarY,
            ResizeNoteStart,
            ResizeSelectionNoteStart,
            ResizeNoteEnd,
            ResizeSelectionNoteEnd,
            MoveNoteRelease,
            MoveSelectionNoteRelease,
            ChangeEnvelopeValue,
            EditEffectSelection,
            MobileZoom,
            MobileZoomVertical,
            MobilePan
        }

        const int ThesholdNormal           = Platform.IsDesktop ? 5 : 50;
        const int ThesholdNormalMobileOnly = Platform.IsDesktop ? 0 : ThesholdNormal;
        const int ThesholdSmall            = Platform.IsDesktop ? 2 : 20;
        const int ThesholdSmallMobileOnly  = Platform.IsDesktop ? 0 : ThesholdSmall;

        static readonly int[] captureThresholds = new[]
        {
            0,                        // None
            0,                        // ResizeEnvelope
            0,                        // DragLoop
            0,                        // DragRelease
            0,                        // ChangeEffectValue
            0,                        // ChangeSelectionEffectValue
            0,                        // ChangeEnvelopeRepeatValue
            0,                        // DrawEnvelope
            ThesholdNormalMobileOnly, // Select
            ThesholdNormalMobileOnly, // SelectWave
            0,                        // CreateNote
            ThesholdNormal,           // CreateSlideNote
            ThesholdNormal,           // DeleteNotes
            ThesholdNormal,           // DragSlideNoteTarget
            ThesholdNormal,           // DragSlideNoteTargetGizmo
            0,                        // DragVolumeSlideTarget
            0,                        // DragVolumeSlideTargetGizmo
            ThesholdSmall,            // DragNote
            ThesholdSmall,            // DragSelection
            0,                        // AltZoom
            ThesholdNormal,           // DragSample
            0,                        // DragSeekBar
            0,                        // DragWaveVolumeEnvelope
            0,                        // ScrollBarX
            0,                        // ScrollBarY
            ThesholdSmall,            // ResizeNoteStart
            ThesholdSmall,            // ResizeSelectionNoteStart
            ThesholdSmall,            // ResizeNoteEnd
            ThesholdSmall,            // ResizeSelectionNoteEnd
            ThesholdSmallMobileOnly,  // MoveNoteRelease
            ThesholdSmallMobileOnly,  // MoveSelectionNoteRelease
            0,                        // ChangeEnvelopeValue
            ThesholdSmall,            // EditEffectSelection
            0,                        // MobileZoom
            0,                        // MobileZoomVertical
            0,                        // MobilePan
        };

        static readonly bool[] captureWantsRealTimeUpdate = new[]
        {
            false,             // None
            true,              // ResizeEnvelope
            false,             // DragLoop
            false,             // DragRelease
            false,             // ChangeEffectValue
            false,             // ChangeSelectionEffectValue
            false,             // ChangeEnvelopeRepeatValue
            true,              // DrawEnvelope
            true,              // Select
            true,              // SelectWave
            true,              // CreateNote
            true,              // CreateSlideNote
            Platform.IsMobile, // DeleteNotes
            true,              // DragSlideNoteTarget
            true,              // DragSlideNoteTargetGizmo
            false,             // DragVolumeSlideTarget
            false,             // DragVolumeSlideTargetGizmo
            true,              // DragNote
            true,              // DragSelection
            false,             // AltZoom
            true,              // DragSample
            true,              // DragSeekBar
            false,             // DragWaveVolumeEnvelope
            false,             // ScrollBarX
            false,             // ScrollBarY
            true,              // ResizeNoteStart 
            true,              // ResizeSelectionNoteStart
            true,              // ResizeNoteEnd
            true,              // ResizeSelectionNoteEnd
            false,             // MoveNoteRelease
            false,             // MoveSelectionNoteRelease
            false,             // ChangeEnvelopeValue
            true,              // EditEffectSelection
            false,             // MobileZoom
            false,             // MobileZoomVertical
            false,             // MobilePan
        };

        internal enum NoteAttackState
        {
            Attack,
            NoAttack,
            NoAttackError
        }

        // Controls.
        Piano piano;
        PianoRollTimeline timeline;
        EnvelopeEditor envelopeEditor;
        WaveEditor waveEditor;
        EffectPanel effectPanel;
        NoteArea noteArea;

        int captureNoteAbsoluteIdx = 0;
        int captureMouseAbsoluteIdx = 0;
        int captureNoteValue = 0;
        int mouseLastX = 0;
        int mouseLastY = 0;
        int captureMouseX = 0;
        int captureMouseY = 0;
        int captureScrollX = 0;
        int captureScrollY = 0;
        int captureSelectionMinX = -1;
        int captureSelectionMaxX = -1;
        int captureSelectionMinY = -1;
        int captureSelectionMaxY = -1;
        int captureOffsetX = 0;
        int captureOffsetY = 0;
        int selectionMinX = -1;
        int selectionMaxX = -1;
        int selectionMinY = -1;
        int selectionMaxY = -1;
        int captureMarqueeMinX = -1;
        int captureMarqueeMaxX = -1;
        int captureMarqueeMinY = -1;
        int captureMarqueeMaxY = -1;
        int dragSeekPosition = -1;
        int snapResolution = Settings.DefaultSnapResolution;
        int scrollX = 0;
        int scrollY = 0;
        int lastChannelScrollX = -1;
        int lastChannelScrollY = -1;
        float lastChannelZoom = -1;
        int selectedEffectIdx = Platform.IsMobile ? -1 : 0;
        int[] supportedEffects;
        bool captureThresholdMet = false;
        bool captureRealTimeUpdate = false;
        bool captureSelectionFromHeader = false;
        bool captureSelectionFromEffectPanel = false;
        bool captureEffectSelectionMove = false;
        bool panning = false;
        bool continuouslyFollowing = false;
        bool maximized = false;
        bool showEffectsPanel = false;
        bool snap = true;
        bool snapEffects = true;
        bool pianoVisible = true;
        bool canFling = false;
        bool relativeEffectScaling = false;
        sbyte captureEnvelopeValue = 0;
        float flingVelX = 0.0f;
        float flingVelY = 0.0f;
        float zoom = DefaultChannelZoom;
        float zoomY = Platform.IsMobile ? 0.8f : 1.0f;
        float pianoScaleX = 1.0f; // Only used by video export.
        float captureWaveTime = 0.0f;
        string noteTooltip = "";
        CaptureOperation captureOperation = CaptureOperation.None;
        EditionMode editMode = EditionMode.Channel;
        bool highlightRepeatEnvelope = false;
        int highlightNoteAbsIndex = -1;
        int highlightDPCMSample = -1;
        NoteLocation captureNoteLocation;
        DateTime lastNoteCreateTime = DateTime.Now;

        // Note dragging support.
        int dragFrameMin = -1;
        int dragFrameMax = -1;
        int dragLastNoteValue = -1;
        SortedList<int, Note> dragNotes = new SortedList<int, Note>();
        SortedList<int, int> dragEffects = new SortedList<int, int>();

        // 2D selection for note and effects area.
        bool legacySelectMode = Settings.UseLegacySelectionMode;
        HashSet<int> selectedNoteIndices = new HashSet<int>();
        HashSet<int> captureSelectedNoteIndices = new HashSet<int>();
        HashSet<int> selectedEffectIndices = new HashSet<int>();
        HashSet<int> captureSelectedEffectIndices = new HashSet<int>();

        // Envelope selections.
        HashSet<int> selectedEnvelopeIndices = new HashSet<int>();
        HashSet<int> captureSelectedEnvelopeIndices = new HashSet<int>();

        // Pattern edit mode.
        int editChannel = -1;

        // Envelope edit mode.
        Instrument editInstrument = null;
        int editEnvelope;
        int envelopeValueOffset = 0;
        float envelopeValueZoom = 1;

        // Arpeggio edit mode
        Arpeggio editArpeggio = null;

        // Remembering last paste-special settings
        bool lastPasteSpecialPasteMix = false;
        bool lastPasteSpecialPasteNotes = true;
        int  lastPasteSpecialPasteEffectMask = Note.EffectAllMask;

        // DPCM editing mode
        int volumeEnvelopeDragVertex = -1;
        DPCMSample editSample = null;

        // When dragging samples
        DPCMSampleMapping draggedSample;

        // Video stuff
        Song videoSong;
        int[] videoChannelTranspose;
        long videoForceDisplayChannelMask;

        // Hover
        int hoverNoteIndex = -1;
        int hoverNoteCount = 1;

        // Scale
        int scaleType = (int)ScaleType.Major;
        int rootNoteIdx = 0;

        internal enum ScaleType
        {
            Major,
            Minor,
            Dorian,
            Phrygian,
            Lydian,
            Mixolydian,
            Locrian,
            MelodicMinor,
            HarmonicMinor,
            DoubleHarmonic
        };

        internal enum GizmoAction
        {
            ResizeNote,
            MoveRelease,
            MoveSlide,
            ChangeEnvValue,
            ChangeEnvEffectValue,
            ChangeEffectValue,
            MoveVolumeSlideValue,
        };

        internal class Gizmo
        {
            public Rectangle Rect;
            public TextureAtlasRef FillImage = null;
            public TextureAtlasRef Image;
            public GizmoAction Action;
            public string GizmoText;
            public int OffsetX;
        };

        public bool SnapAllowed       { get => editMode == EditionMode.Channel; }
        public bool SnapEnabled       { get => SnapAllowed && snap; set { if (SnapAllowed) snap = value; MarkDirty(); } }
        public bool SnapEffectEnabled { get => SnapAllowed && snapEffects; set { if (SnapAllowed) snapEffects = value; MarkDirty(); } }
        public bool SnapTemporarelyDisabled => ModifierKeys.IsAltDown && !Settings.AltLeftForMiddle;

        public bool EffectPanelExpanded { get => showEffectsPanel; set => SetShowEffectPanel(value); }

        public bool CanDisplayEnvelopePlayhead
        {
            get
            {
                if (editMode == EditionMode.Envelope)
                {
                    if (App.SelectedInstrument != editInstrument)
                        return false;

                    // Instrument arp overriden.
                    if (editEnvelope == EnvelopeType.Arpeggio && App.SelectedArpeggio != null)
                        return false;

                    return CanEnvelopeDisplayFrame();
                }

                if (editMode == EditionMode.Arpeggio)
                    return App.SelectedArpeggio == editArpeggio;

                return false;
            }
        }

        public int  SnapResolution
        {
            get { Debug.Assert(editMode == EditionMode.Channel); return snapResolution; }
            set { Debug.Assert(editMode == EditionMode.Channel); snapResolution = value; MarkDirty(); }
        }

        public int SelectedEffect
        {
            get { Debug.Assert(editMode == EditionMode.Channel); return supportedEffects.Length > 0 ? selectedEffectIdx : -1; }
            set { Debug.Assert(editMode == EditionMode.Channel); selectedEffectIdx = value; MarkDirty(); }
        }

        public bool IsMaximized                => maximized;
        public bool PianoVisible               => pianoVisible;
        public bool LegacySelectMode           => legacySelectMode;
        public bool IsCapturingPointer         => captureOperation != CaptureOperation.None;

        internal EditionMode EditMode          => editMode;

        public bool IsEditingVideo             => editMode == EditionMode.VideoRecording;
        public bool IsEditingChannel           => editMode == EditionMode.Channel; 
        public bool IsEditingInstrument        => editMode == EditionMode.Envelope; 
        public bool IsEditingArpeggio          => editMode == EditionMode.Arpeggio;
        public bool IsEditingDPCMSample        => editMode == EditionMode.DPCM;
        public bool IsEditingDPCMSampleMapping => editMode == EditionMode.DPCMMapping;
        public bool DrawDpcmColorKeysOnPiano   => editMode == EditionMode.DPCMMapping || (editMode == EditionMode.Channel && Song.Channels[editChannel].Type == ChannelType.Dpcm);
        public bool ShowOctaveLabels           => editMode == EditionMode.Channel || editMode == EditionMode.DPCMMapping;

        public bool IsTimelineEnvelopeCapture        => captureOperation == CaptureOperation.ResizeEnvelope || captureOperation == CaptureOperation.DragLoop || captureOperation == CaptureOperation.DragRelease;
        public bool IsTimelineColumnSelectionCapture => captureOperation == CaptureOperation.Select || captureOperation == CaptureOperation.SelectWave;
        public bool IsTimelineSeekCapture            => captureOperation == CaptureOperation.DragSeekBar;
        public bool IsTimelinePanCapture             => panning;
        public bool TimelineCaptureThresholdMet      => (captureOperation == CaptureOperation.Select || captureOperation == CaptureOperation.SelectWave) && captureThresholdMet;

    
        public bool CanCopy         => IsActiveControl && HasSelectionContent() && (editMode == EditionMode.Channel || editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio);
        public bool CanCopyAsText   => IsActiveControl && IsSelectionValid() && (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio);
        public bool CanPaste        => IsActiveControl && (editMode == EditionMode.Channel ? ClipboardUtils.ContainsNotes && (!legacySelectMode || IsSelectionValid()) : (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio) && IsSelectionValid() && ClipboardUtils.ContainsEnvelope);
        public bool CanDelete       => IsActiveControl && HasSelectionContent() && (editMode == EditionMode.Channel || editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio || editMode == EditionMode.DPCM);
        public bool IsActiveControl => App != null && App.ActiveControl == this;

        public static int DefaultPianoKeyWidth => DefaultNoteSizeY;

        public Instrument EditInstrument   => editInstrument;
        public Arpeggio   EditArpeggio     => editArpeggio;
        public DPCMSample EditSample       => editSample;

        public Color SelectionBgVisibleColor => selectionBgVisibleColor;
        public Color SelectionHighlightColor => selectionHighlightColor;
        public Color SeekBarColor            => GetSeekBarColor();

        public int EditEnvelopeType            => editEnvelope;
        public int VirtualSizeY                => virtualSizeY;
        public int OctaveSizeY                 => octaveSizeY;
        public int ViewScrollY                 => scrollY;
        public int NoteSizeY                   => noteSizeY;
        public int WhiteKeySizeY               => whiteKeySizeY;
        public int BlackKeySizeX               => blackKeySizeX;
        public int BlackKeySizeY               => blackKeySizeY;
        public int PianoSizeX                  => pianoSizeX;
        public int HeaderAndEffectSizeY        => headerAndEffectSizeY;
        public int ScrollBarThickness          => scrollBarThickness;
        public int ViewScrollX                 => scrollX;
        public int EditChannel                 => editChannel;
        public int SelectionMinX               => selectionMinX;
        public int SelectionMaxX               => selectionMaxX;
        public int CaptureMarqueeMinX          => captureMarqueeMinX;
        public int CaptureMarqueeMaxX          => captureMarqueeMaxX;
        public bool IsSelectCapture            => captureOperation == CaptureOperation.Select;
        public int PianoWidth                  => pianoSizeX;
        public int HeaderSizeY                 => headerSizeY;
        public int HighlightNoteAbsoluteIndex  => highlightNoteAbsIndex;
        public int FontSmallCharSizeX          => fontSmallCharSizeX;
        public int EffectValuePosTextOffsetY   => effectValuePosTextOffsetY;
        public int EffectValueNegTextOffsetY   => effectValueNegTextOffsetY;
        public int TimelineEnvelopeResizeWidth => timeline.EnvelopeResizeWidth;

        public bool ShowEffectsPanel      => showEffectsPanel;
        public bool HighlightRepeatEnvelope => highlightRepeatEnvelope;
        public bool RelativeEffectScaling => relativeEffectScaling;
        public bool IsVideoRecording      => editMode == EditionMode.VideoRecording;
        public bool IsChangingEffectValue => captureOperation == CaptureOperation.ChangeEffectValue;
        public bool IsChangingEnvelopeRepeatValue => captureOperation == CaptureOperation.ChangeEnvelopeRepeatValue;

        public int SelectedEffectIdx { get => selectedEffectIdx; set => selectedEffectIdx = value; }
        public int[] SupportedEffects     => supportedEffects;
        public int EffectPanelSizeY       => effectPanelSizeY;
        public int EffectButtonSizeY      => effectButtonSizeY;
        public int EffectIconPosX         => effectIconPosX;
        public int EffectIconPosY         => effectIconPosY;
        public int EffectNamePosX         => effectNamePosX;
        public int EffectPanelTextPosX    => effectPanelTextPosX;
        public int EffectPanelTextPosY    => effectPanelTextPosY;

        public Color VolumeSlideBarFillColor   => volumeSlideBarFillColor;
        public Color SelectionBgInvisibleColor => selectionBgInvisibleColor;

        public float BitmapScale       => bitmapScale;
        public float EffectBitmapScale => effectBitmapScale;

        public TextureAtlasRef BmpEffectRepeat  => bmpEffectRepeat;
        public TextureAtlasRef[] BmpEffects     => bmpEffects;

        public WaveEditor WaveEditor => waveEditor;

        public float NoteSizeX          => noteSizeX;
        public float Zoom               => zoom;
        public float EnvelopeValueZoom  => envelopeValueZoom;
        public float EnvelopeValueSizeY => envelopeValueSizeY;


        public int HoverNoteIndex => hoverNoteIndex;
        public int HoverNoteCount => hoverNoteCount;

        public Envelope CurrentEditEnvelope => EditEnvelope;

        public LocalizedString PianoRollMoreOptionsTooltip => MoreOptionsTooltip;

        public int TooltipTextPosX => tooltipTextPosX;
        public int TooltipTextPosY => tooltipTextPosY;
        public int BigTextPosX     => bigTextPosX;
        public int BigTextPosY     => bigTextPosY;

        internal int RootNoteIdx                          => rootNoteIdx;
        internal int ScaleTypeIndex                        => scaleType;
        internal long VideoForceDisplayChannelMask         => videoForceDisplayChannelMask;
        internal int MouseLastX                            => mouseLastX;
        internal int MouseLastY                            => mouseLastY;
        internal float[] MobileEraseGeometry               => mobileEraseGeometry;
        internal TextureAtlasRef BmpEffectFrame            => bmpEffectFrame;
        internal int HighlightDPCMSample                   => highlightDPCMSample;
        internal DPCMSampleMapping DraggedSample           => draggedSample;
        internal int DpcmTextPosX                          => dpcmTextPosX;
        internal int ReleaseNoteSizeY                      => releaseNoteSizeY;
        internal float[] SlideNoteGeometry                 => slideNoteGeometry;
        internal int AttackIconPosX                        => attackIconPosX;
        internal int NoteAttackSizeX                       => noteAttackSizeX;
        internal Color AttackColor                         => attackColor;
        internal Color AttackBrushForceDisplayColor        => attackBrushForceDisplayColor;
        internal float[][] StopNoteGeometry                => stopNoteGeometry;
        internal float[][] StopReleaseNoteGeometry         => stopReleaseNoteGeometry;
        internal float[][] ReleaseNoteGeometry             => releaseNoteGeometry;
        internal int MinPixelDistForLines                  => minPixelDistForLines;
        internal int[] VideoChannelTranspose               => videoChannelTranspose;
        internal bool IsDeleteNotesCapture                 => captureOperation == CaptureOperation.DeleteNotes;
        internal CaptureOperation ActiveCaptureOperation   => captureOperation;
        internal bool IsDragSampleCapture                  => captureOperation == CaptureOperation.DragSample;
        internal bool IsNoCapture                          => captureOperation == CaptureOperation.None;
        internal string NoteTooltip                        => noteTooltip;
        internal void SetNoteTooltip(string s) => noteTooltip = s;

        internal void SetPianoHoverNote(int note, int noteIndex, int noteCount)
        {
            piano.HoverNote = note;
            SetAndMarkDirty(ref hoverNoteIndex, noteIndex);
            SetAndMarkDirty(ref hoverNoteCount, noteCount);
        }

        internal void ClearPianoHoverNote()
        {
            piano.HoverNote = -1;
            SetAndMarkDirty(ref hoverNoteIndex, -1);
        }

        internal int CaptureMouseAbsoluteIdx           => captureMouseAbsoluteIdx;
        internal int CaptureNoteAbsoluteIdx            => captureNoteAbsoluteIdx;
        internal NoteLocation CaptureNoteLocation      => captureNoteLocation;
        internal int CaptureNoteValue                  => captureNoteValue;
        internal int CaptureSelectionMinY              => captureSelectionMinY;
        internal int CaptureSelectionMaxY              => captureSelectionMaxY;
        internal bool CaptureThresholdMet              => captureThresholdMet;

        internal int SelectionMinXField { get => selectionMinX; set => selectionMinX = value; }
        internal int SelectionMaxXField { get => selectionMaxX; set => selectionMaxX = value; }
        internal int SelectionMinYField { get => selectionMinY; set => selectionMinY = value; }
        internal int SelectionMaxYField { get => selectionMaxY; set => selectionMaxY = value; }

        internal HashSet<int> SelectedNoteIndicesSet   => selectedNoteIndices;
        internal HashSet<int> SelectedEffectIndicesSet => selectedEffectIndices;

        internal SortedList<int, Note> DragNotes { get => dragNotes; set => dragNotes = value; }
        internal SortedList<int, int> DragEffects      => dragEffects;
        internal int DragFrameMin { get => dragFrameMin; set => dragFrameMin = value; }
        internal int DragFrameMax { get => dragFrameMax; set => dragFrameMax = value; }
        internal int DragLastNoteValue { get => dragLastNoteValue; set => dragLastNoteValue = value; }

        internal void RaisePatternChanged(Pattern pattern) => PatternChanged?.Invoke(pattern);

        internal void SetHighlightNoteAbsIndex(int idx) => highlightNoteAbsIndex = idx;
        internal void SetLastNoteCreateTime() => lastNoteCreateTime = DateTime.Now;

        public delegate void EmptyDelegate();
        public delegate void InstrumentEnvDelegate(Instrument instrument, int env);
        public delegate void PatternDelegate(Pattern pattern);
        public delegate void NoteDelegate(Note note);
        public delegate void DPCMMappingDelegate(int note);

        public event PatternDelegate       PatternChanged;
        public event EmptyDelegate         MaximizedChanged;
        public event EmptyDelegate         ManyPatternChanged;
        public event EmptyDelegate         DPCMSampleChanged;
        public event EmptyDelegate         NotesPasted;
        public event EmptyDelegate         ScrollChanged;
        public event NoteDelegate          NoteEyedropped;
        public event InstrumentEnvDelegate EnvelopeChanged;
        public event DPCMMappingDelegate   DPCMSampleMapped;
        public event DPCMMappingDelegate   DPCMSampleUnmapped;

        #region Localization

        // Piano Roll
        LocalizedString HoldFingersToDrawMessage;
        LocalizedString HoldFingersToEraseMessage;

        // DPCM mapping editor
        LocalizedString PitchLabel;
        LocalizedString TransposeSampleMessage;
        LocalizedString TransposeSampleTitle;
        LocalizedString NoDPCMSampleMessage;
        LocalizedString NoDPCMSampleTitle;

        // DPCM mapping assignment
        LocalizedString AssignDPCMSampleTitle;
        LocalizedString SelectSampleToAssignLabel;
        LocalizedString LoopLabel;

        // DPCM mapping properties
        LocalizedString SampleMappingTitle;
        LocalizedString OverrideDMCInitialValueLabel;
        LocalizedString DMCInitialValueDiv2Label;

        // Context menus
        LocalizedString DeleteSelectedNotesContext;
        LocalizedString DeleteNoteContext;
        LocalizedString ToggleNoteAttackContext;
        LocalizedString ToggleSelectedNoteAttackContext;
        LocalizedString ToggleSlideNoteContext;
        LocalizedString ToggleSelectedSlideNoteContext;
        LocalizedString ToggleReleaseContext;
        LocalizedString ToggleSelectedReleaseContext;
        LocalizedString MakeStopNoteContext;
        LocalizedString ReplaceAllInstrumentContext;
        LocalizedString ReplaceAllArpeggioContext;
        LocalizedString ReplaceSpecificInstrumentContext;
        LocalizedString ArpeggioNoneContext;
        LocalizedString MakeInstrumentCurrentContext;
        LocalizedString SetSnapContext;
        LocalizedString SelectNoteRangeContext;
        LocalizedString ClearSelectionContext;
        LocalizedString CopyEffectValuesAsEnvValuesContext;
        LocalizedString CopyEffectValuesAsTextContext;
        LocalizedString EnterEffectValueContext;
        LocalizedString ClearEffectValueContext;
        LocalizedString ClearSelectEffectValuesContext;
        LocalizedString ToggleVolumeSlideContext;
        LocalizedString AbsoluteEffectScalingContext;
        LocalizedString RelativeEffectScalingContext;
        LocalizedString AbsoluteValueScalingContext;
        LocalizedString AbsoluteValueScalingContextTooltip;
        LocalizedString RelativeValueScalingContext;
        LocalizedString RelativeValueScalingContextTooltip;
        LocalizedString SetLoopPointContext;
        LocalizedString ClearLoopPointContext;
        LocalizedString SetReleasePointContext;
        LocalizedString ClearReleasePointContext;
        LocalizedString FlattenSelectionContext;
        LocalizedString CopySelectedValuesAsTextContext;
        LocalizedString ResetVertexContext;
        LocalizedString ResetVolumeEnvelopeContext;
        LocalizedString DeleteSelectedSamplesContext;
        LocalizedString RemoveDPCMSampleContext;
        LocalizedString DPCMSamplePropertiesContext;
        LocalizedString SnapEnableContext;
        LocalizedString SnapEnableContextTooltip;
        LocalizedString SnapEffectsContext;
        LocalizedString SnapEffectsContextTooltip;
        LocalizedString SnapToBeatContext;
        LocalizedString SnapToBeatsContext;
        LocalizedString SnapToBeatContextTooltip;
        LocalizedString SnapToBeatsContextTooltip;
        LocalizedString ScaleTypeContext;
        LocalizedString RootNoteContext;
        LocalizedString ScaleMajor;
        LocalizedString ScaleMinor;
        LocalizedString ScaleDorian;
        LocalizedString ScalePhrygian;
        LocalizedString ScaleLydian;
        LocalizedString ScaleMixolydian;
        LocalizedString ScaleLocrian;
        LocalizedString ScaleMelodicMinor;
        LocalizedString ScaleHarmonicMinor;
        LocalizedString ScaleDoubleHarmonic;

        // tooltips
        LocalizedString MoreOptionsTooltip;

        #endregion

        public PianoRoll()
        {
            Localization.Localize(this);
            SetTickEnabled(true);
            supportsLongPress = true;
            supportsDoubleClick = true;
        }

        private void UpdateRenderCoords()
        {
            var videoMode = editMode == EditionMode.VideoRecording;
            var headerScale = editMode == EditionMode.DPCMMapping || editMode == EditionMode.DPCM ? 1 : (editMode == EditionMode.VideoRecording ? 0 : 2);
            var scrollBarSize = Settings.ScrollBars == 1 ? DefaultScrollBarThickness1 : (Settings.ScrollBars == 2 ? DefaultScrollBarThickness2 : 0);
            var effectIconsScale = Platform.IsMobile ? 0.5f : 1.0f;

            minZoom = editMode == EditionMode.Channel && Song != null && Song.UsesFamiStudioTempo ? MinZoomFamiStudio : MinZoomOther;
            maxZoom = editMode == EditionMode.DPCM ? MaxWaveZoom : MaxZoom;
            zoom    = Utils.Clamp(zoom, minZoom, maxZoom);

            headerSizeY               = DpiScaling.ScaleForWindow(DefaultHeaderSizeY * headerScale);
            effectButtonSizeY         = DpiScaling.ScaleForWindow(DefaultEffectButtonSizeY * effectIconsScale);
            noteSizeX                 = DpiScaling.ScaleForWindowFloat(DefaultNoteSizeX * zoom);
            noteSizeY                 = DpiScaling.ScaleForWindow(DefaultNoteSizeY * zoomY);
            noteAttackSizeX           = DpiScaling.ScaleForWindow(DefaultNoteAttackSizeX);
            releaseNoteSizeY          = DpiScaling.ScaleForWindow(DefaultReleaseNoteSizeY * zoomY) & 0xfe; // Keep even
            pianoSizeX                = DpiScaling.ScaleForWindow((videoMode || Platform.IsDesktop ? DefaultPianoSizeX    : DefaultPianoSizeXMobile)    * pianoScaleX);
            blackKeySizeX             = DpiScaling.ScaleForWindow((videoMode || Platform.IsDesktop ? DefaultBlackKeySizeX : DefaultBlackKeySizeXMobile) * pianoScaleX);
            whiteKeySizeY             = DpiScaling.ScaleForWindow(DefaultWhiteKeySizeY * zoomY);
            blackKeySizeY             = DpiScaling.ScaleForWindow(DefaultBlackKeySizeY * zoomY);
            effectIconPosX            = DpiScaling.ScaleForWindow(DefaultEffectIconPosX * effectIconsScale);
            effectIconPosY            = DpiScaling.ScaleForWindow(DefaultEffectIconPosY * effectIconsScale);
            headerIconsPosX           = DpiScaling.ScaleForWindow(headerScale == 1 ? DefaultSnapIconDpcmPosX : DefaultSnapIconPosX);
            headerIconsPosY           = DpiScaling.ScaleForWindow(headerScale == 1 ? DefaultSnapIconDpcmPosY : DefaultSnapIconPosY);
            effectNamePosX            = DpiScaling.ScaleForWindow(DefaultEffectNamePosX * effectIconsScale);
            effectValuePosTextOffsetY = DpiScaling.ScaleForFont(DefaultEffectValuePosTextOffsetY);
            effectValueNegTextOffsetY = DpiScaling.ScaleForFont(DefaultEffectValueNegTextOffsetY);
            bigTextPosX               = DpiScaling.ScaleForFont(DefaultBigTextPosX);
            bigTextPosY               = DpiScaling.ScaleForFont(DefaultBigTextPosY);
            tooltipTextPosX           = DpiScaling.ScaleForFont(DefaultTooltipTextPosX);
            tooltipTextPosY           = DpiScaling.ScaleForFont(DefaultTooltipTextPosY);
            effectPanelTextPosX       = DpiScaling.ScaleForFont(DefaultEffectPanelTextPosX);
            effectPanelTextPosY       = DpiScaling.ScaleForFont(DefaultEffectPanelTextPosY);
            dpcmTextPosX              = DpiScaling.ScaleForFont(DefaultDPCMTextPosX);
            attackIconPosX            = DpiScaling.ScaleForWindow(DefaultAttackIconPosX);
            scrollBarThickness        = DpiScaling.ScaleForWindow(scrollBarSize);
            minScrollBarLength        = DpiScaling.ScaleForWindow(DefaultMinScrollBarLength);
            noteResizeMargin          = DpiScaling.ScaleForWindow(DefaultNoteResizeMargin);
            minPixelDistForLines      = DpiScaling.ScaleForWindow(DefaultMinPixelDistForLines);
            envelopeValueSizeY        = DpiScaling.ScaleForWindowFloat(DefaultEnvelopeSizeY * envelopeValueZoom);
            gizmoSize                 = DpiScaling.ScaleForWindow(DefaultGizmoSize);
            scrollMargin              = (width - pianoSizeX) / 8;

            // Make sure the effect panel actually fit on screen on mobile.
            if (Platform.IsMobile && ParentWindow != null)
                effectPanelSizeY = height / 2 - headerSizeY;
            else
                effectPanelSizeY = DpiScaling.ScaleForWindow(DefaultEffectPanelSizeY);

            // Might as well just open this on mobile, since there is no toggle button for it.
            if (Platform.IsMobile && editMode == EditionMode.DPCM)
                showEffectsPanel = true;

            octaveSizeY = 12 * noteSizeY;
            headerAndEffectSizeY = headerSizeY + (showEffectsPanel ? effectPanelSizeY : 0);
            virtualSizeY = NumNotes * noteSizeY;

            // TODO: Make the piano invisible during DPCM edit mode? Not sure regarding the fullscreen piano roll and effects mode button.
            // We could just always show the volume over the DPCM sample, but it still doesn't solve the full screen piano roll button issue.
            pianoVisible = editMode == EditionMode.Channel || editMode == EditionMode.DPCMMapping || editMode == EditionMode.VideoRecording || Platform.IsDesktop;

            if (Platform.IsMobile && (editMode == EditionMode.Arpeggio || editMode == EditionMode.Envelope))
            {
                Envelope.GetMinMaxValueForType(editInstrument, editEnvelope, out int min, out int max);
                var maxValuesInScreen = editEnvelope == EnvelopeType.FdsWaveform || editEnvelope == EnvelopeType.FdsModulation ? 64 : 16;
                envelopeValueSizeY = (Height - headerAndEffectSizeY) / Math.Min(maxValuesInScreen, max - min + 1);
                virtualSizeY = (int)((max - min + 1) * envelopeValueSizeY);
            }

            if (!pianoVisible)
                pianoSizeX = 0;

            // TODO: Move these to their respective controls where applicable.
            if (piano != null)
                piano.Visible = pianoVisible;

            if (envelopeEditor != null)
                envelopeEditor.Visible = editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio;

            if (waveEditor != null)
            {
                waveEditor.Visible = editMode == EditionMode.DPCM;
                waveEditor.UpdateRenderCoords();
            }

            if (noteArea != null)
                noteArea.Visible = editMode == EditionMode.Channel || editMode == EditionMode.VideoRecording || editMode == EditionMode.DPCMMapping;

            if (effectPanel != null)
                UpdateEffectPanelMode();

            if (piano != null && timeline != null && envelopeEditor != null && waveEditor != null && effectPanel != null)
                UpdateChildLayouts();
        }

        private void UpdateEffectPanelMode()
        {
            var mode = EffectPanel.PanelMode.None;

            if (editMode == EditionMode.Channel)
                mode = EffectPanel.PanelMode.Notes;
            else if (editMode == EditionMode.DPCM)
                mode = EffectPanel.PanelMode.Wave;
            else if (editMode == EditionMode.Envelope && HasRepeatEnvelope())
                mode = EffectPanel.PanelMode.Notes;

            effectPanel.SetPanelMode(mode);
        }

        public void StartTimelineOrEffectPan(int x, int y)
        {
            StartPan(x, y, false);
        }

        public void StartTimelineSeek(int x, int y)
        {
            StartCaptureOperation(x, y, CaptureOperation.DragSeekBar);
        }

        public void StartTimelineSelection(int x, int y)
        {
            if (editMode == EditionMode.DPCM)
                StartSelectWave(x, y, false);
            else
                StartSelection(x, y, false);
        }

        public void AbortTimelineCapture(bool restore = false)
        {
            AbortCaptureOperation(restore);
        }

        public bool IsChangingEnvelopeValue => captureOperation == CaptureOperation.ChangeEnvelopeValue;

        public void UpdateTimelinePan(int x, int y)
        {
            DoScroll(x - mouseLastX, y - mouseLastY);
            SetMouseLastPos(x, y);
        }

        public void UpdateTimelineCapture(int x, int y)
        {
            UpdateCaptureOperation(x, y);
            SetMouseLastPos(x, y);
        }

        public void EndTimelinePan()
        {
            panning = false;
        }

        public void EndTimelineCapture(int x, int y)
        {
            EndCaptureOperation(x, y);
        }

        public bool HandleTimelineEnvelopePointerDown(int x, int y, bool left, bool right)
        {
            return envelopeEditor.HandleTimelinePointerDown(x, y, left, right, timeline.Height);
        }

        public EnvelopeEditor.TimelineHoverRegion GetTimelineEnvelopeHoverRegion(int x, int y, out bool canLoop, out bool canRelease, out bool hasLoopPoint)
        {
            return envelopeEditor.GetTimelineHoverRegion(x, y, timeline.Height, out canLoop, out canRelease, out hasLoopPoint);
        }

        public void StartEnvelopeResize(int x, int y)
        {
            StartResizeEnvelope(x, y);
        }

        public void StartEnvelopeLoopRelease(int x, int y, bool left)
        {
            var op = left ? CaptureOperation.DragLoop : CaptureOperation.DragRelease;

            StartCaptureOperation(x, y, op);

            if (editMode == EditionMode.Envelope)
                App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Arpeggio, editArpeggio.Id);

            ResizeEnvelope(x, y, false);
        }

        public bool StartEnvelopeDraw(int x, int y)
        {
            var env = CurrentEditEnvelope;
            if (env == null || env.Length <= 0)
                return false;

            var noteIdx = GetAbsoluteNoteIndexForPixelX(x - pianoSizeX);

            if (IsEnvelopeValueSelected(noteIdx))
            {
                SetMobileHighlightedNote(noteIdx);
                StartChangeEnvelopeValue(x, y);
            }
            else
            {
                StartDrawEnvelope(x, y);
            }

            return true;
        }

        public bool StartEnvelopeFreeDraw(int x, int y)
        {
            var env = CurrentEditEnvelope;

            if (env == null || env.Length <= 0)
                return false;

            StartDrawEnvelope(x, y);
            return true;
        }

        public bool HandleEnvelopeGizmoPointerDown(int x, int y)
        {
            return HandleTouchDownEnvelopeGizmos(x, y);
        }

        public bool StartMobilePan(int x, int y)
        {
            StartCaptureOperation(x, y, CaptureOperation.MobilePan);
            return true;
        }

        public bool HandleEnvelopeTouchClick(int x, int y)
        {
            if (EditEnvelope == null)
                return false;

            if (HandleTouchClickEnvelope(x, y))
            {
                MarkDirty();
                return true;
            }

            return false;
        }

        private void UpdateTimelineEditMode()
        {
            timeline.SetEditMode(editMode switch
            {
                EditionMode.Channel  => PianoRollTimeline.EditMode.Channel,
                EditionMode.Envelope => PianoRollTimeline.EditMode.Envelope,
                EditionMode.Arpeggio => PianoRollTimeline.EditMode.Arpeggio,
                EditionMode.DPCM     => PianoRollTimeline.EditMode.Dpcm,
                _                    => PianoRollTimeline.EditMode.None,
            });
        }

        public void StartEditChannel(int channelIdx, int patternIdx = 0)
        {
            editMode = EditionMode.Channel;
            editChannel = channelIdx;
            noteTooltip = "";

            UpdateTimelineEditMode();

            var restoredScroll = RestoreChannelScroll();

            BuildSupportEffectList();
            ClearSelection();
            UpdateRenderCoords();
            if (!restoredScroll)
                CenterScroll(patternIdx);
            ClampScroll();
            MarkDirty();
        }

        public void ChangeChannel(int channelIdx)
        {
            if (editMode == EditionMode.Channel)
            {
                AbortCaptureOperation();
                editChannel = channelIdx;
                noteTooltip = "";
                ClearSelection();
                BuildSupportEffectList();
                MarkDirty();
            }
        }

        public void StartEditInstrument(Instrument instrument, int envelope)
        {
            SaveChannelScroll();

            editMode = EditionMode.Envelope;
            editInstrument = instrument;
            editEnvelope = envelope;
            showEffectsPanel = false;
            noteTooltip = "";
            envelopeValueZoom = envelope == EnvelopeType.Volume || envelope == EnvelopeType.DutyCycle || envelope == EnvelopeType.N163Waveform ? 2 : 1;
            envelopeValueOffset = 0;
            Debug.Assert(editInstrument != null);

            UpdateTimelineEditMode();

            ClearSelection();
            ClearHighlightedNote();
            UpdateRenderCoords();
            CenterEnvelopeScroll();
            ClampScroll();
            MarkDirty();
        }

        public void StartEditArpeggio(Arpeggio arpeggio)
        {
            SaveChannelScroll();

            editMode = EditionMode.Arpeggio;
            editEnvelope = EnvelopeType.Arpeggio;
            editInstrument = null;
            editArpeggio = arpeggio;
            showEffectsPanel = false;
            noteTooltip = "";
            envelopeValueZoom = 1;  
            envelopeValueOffset = 0;

            UpdateTimelineEditMode();

            ClearSelection();
            ClearHighlightedNote();
            UpdateRenderCoords();
            CenterEnvelopeScroll();
            ClampScroll();
            MarkDirty();
        }

        public void StartEditDPCMSample(DPCMSample sample)
        {
            SaveChannelScroll();

            editMode = EditionMode.DPCM;
            editSample = sample;
            zoom = 1.0f;
            noteTooltip = "";
            envelopeValueZoom = 1;
            envelopeValueOffset = 0;

            UpdateTimelineEditMode();

            ClearSelection();
            ClearHighlightedNote();
            UpdateRenderCoords();
            CenterWaveScroll();
            ClampScroll();
            MarkDirty();
        }

        public void StartEditDPCMMapping(Instrument instrument)
        {
            SaveChannelScroll();

            editMode = EditionMode.DPCMMapping;
            editInstrument = instrument;
            showEffectsPanel = false;
            zoom = 1.0f;
            noteTooltip = "";
            envelopeValueZoom = 1;
            envelopeValueOffset = 0;

            UpdateTimelineEditMode();

            ClearSelection();
            ClearHighlightedNote();
            UpdateRenderCoords();
            CenterDPCMMappingScroll();
            ClampScroll();
            MarkDirty();
        }

        public void StartVideoRecording(Song song, float videoZoom, float pianoRollScaleX, float pianoRollScaleY, int[] transpose)
        {
            Debug.Assert(transpose == null || transpose.Length == song.Channels.Length);

            editChannel = 0;
            editMode = EditionMode.VideoRecording;
            videoSong = song;
            zoom = videoZoom;
            pianoScaleX = pianoRollScaleX;
            zoomY = pianoRollScaleY;
            videoChannelTranspose = transpose;

            UpdateRenderCoords();
        }

        public void EndVideoRecording()
        {
            videoForceDisplayChannelMask = 0;
            videoChannelTranspose = null;
        }

        public void ApplySettings()
        {
            snapResolution = Settings.SnapResolution;
            snap = Settings.SnapEnabled;
            snapEffects = Settings.SnapEnabled;
            legacySelectMode = Settings.UseLegacySelectionMode;
            ClearSelection(); // In case selection mode was toggled.
        }

        public void SaveSettings()
        {
            Settings.SnapResolution = snapResolution;
            Settings.SnapEnabled = snap;
            Settings.SnapEffects = snapEffects;
            Settings.UseLegacySelectionMode = legacySelectMode;
        }

        public void SaveChannelScroll()
        {
            if (editMode == EditionMode.Channel)
            {
                lastChannelScrollX = scrollX;
                lastChannelScrollY = scrollY;
                lastChannelZoom = zoom;
            }
        }

        public bool RestoreChannelScroll()
        {
            if (Platform.IsMobile && lastChannelScrollX >= 0 && lastChannelScrollY >= 0 && lastChannelZoom > 0)
            {
                scrollX = lastChannelScrollX;
                scrollY = lastChannelScrollY;
                zoom = lastChannelZoom;
                return true;
            }
            else if (Platform.IsDesktop && lastChannelZoom > 0)
            {
                // Intentionally not returning true so that we still center the scroll.
                zoom = lastChannelZoom;
            }

            return false;
        }

        private void BuildSupportEffectList()
        {
            if (editChannel >= 0)
            {
                int cnt = 0;
                for (int i = 0; i < Note.EffectCount; i++)
                {
                    if (Song.Channels[editChannel].ShouldDisplayEffect(i))
                        cnt++;
                }

                supportedEffects = new int[cnt];
                for (int i = 0, j = 0; i < Note.EffectCount; i++)
                {
                    if (Song.Channels[editChannel].ShouldDisplayEffect(i))
                        supportedEffects[j++] = i;
                }

                if (Array.IndexOf(supportedEffects, selectedEffectIdx) == -1)
                    selectedEffectIdx = supportedEffects.Length > 0 ? supportedEffects[0] : -1;

                if (Platform.IsMobile && selectedEffectIdx < 0)
                    showEffectsPanel = false;
            }
        }

        private void CenterWaveScroll()
        {
            zoom = maxZoom;

            var duration = Math.Max(editSample.SourceDuration, editSample.ProcessedDuration);
            var viewSize = Width - pianoSizeX;
            var width    = (int)GetPixelForWaveTime(duration);

            while (width > viewSize && zoom > minZoom)
            {
                zoom /= 2;
                width /= 2;
            }

            scrollX = 0;
            UpdateRenderCoords(); // To update noteSizeX
        }

        private void CenterEnvelopeScroll()
        {
            if (editMode == EditionMode.Arpeggio)
                CenterEnvelopeScroll(editArpeggio.Envelope, EnvelopeType.Arpeggio);
            else if (editMode == EditionMode.Envelope)
                CenterEnvelopeScroll(editInstrument.Envelopes[editEnvelope], editEnvelope, editInstrument);
        }

        private void CenterEnvelopeScroll(Envelope envelope, int envelopeType, Instrument instrument = null)
        {
            var baseNoteSizeX = DpiScaling.ScaleForWindow(DefaultNoteSizeX);
            var envelopeLength = Math.Max(4, envelope.Length);

            zoom = minZoom;
            while (zoom < maxZoom && envelopeLength * baseNoteSizeX * (zoom * 2.0f) < (Width - pianoSizeX))
                zoom *= 2.0f;

            if (Platform.IsMobile)
                envelopeValueZoom = 1.0f;

            UpdateRenderCoords();

            Envelope.GetMinMaxValueForType(instrument, envelopeType, out var typeMin, out var typeMax);

            if (Platform.IsDesktop)
            {
                int midY = virtualSizeY - ((typeMin + typeMax) / 2 + 64 / (int)envelopeValueZoom) * (virtualSizeY / (128 / (int)envelopeValueZoom));

                scrollX = 0;
                scrollY = midY - Height / 2;
            }
            else
            {
                if (typeMax - typeMin > 15)
                {
                    envelope.GetMinMaxValue(out var envMin, out var envMax);
                    scrollY = (int)(virtualSizeY - envelopeValueSizeY * (((envMax + envMin) / 2) - typeMin + 1) - (Height - headerAndEffectSizeY) / 2);
                }
                else
                {
                    scrollY = 0;
                }

                scrollX = 0;
            }

            ClampScroll();
        }

        private void CenterDPCMMappingScroll()
        {
            var midIdx = Note.MusicalNoteC4;

            if (editInstrument.GetMinMaxMappedSampleIndex(out var minIdx, out var maxIdx))
                midIdx = (minIdx + maxIdx) / 2;

            var noteY = virtualSizeY - midIdx * noteSizeY;
            scrollY = noteY - (Height - headerAndEffectSizeY) / 2;
        }

        public int GetPixelXForAbsoluteNoteIndex(int n, bool scroll = true)
        {
            // On PC, all math noteSizeX are always integer, but on mobile, they 
            // can be float. We need to cast into double since at the maximum zoom,
            // in a *very* long song, we are hitting the precision limit of floats.
            var x = (int)(n * (double)noteSizeX);
            if (scroll)
                x -= scrollX;
            return x;
        }

        private int GetPixelXForAbsoluteNoteIndex(float n, bool scroll = true)
        {
            var x = (int)Math.Round(n * noteSizeX);

            if (scroll)
                x -= scrollX;

            return x;
        }

        public int GetAbsoluteNoteIndexForPixelX(int x, bool scroll = true)
        {
            if (scroll)
                x += scrollX;
            return (int)(x / (double)noteSizeX);
        }

        internal int GetPixelYForNoteValue(int note)
        {
            Debug.Assert(Note.IsMusicalNote(note) || editMode == EditionMode.VideoRecording);
            return virtualSizeY - note * noteSizeY - scrollY;
        }

        private void CenterScroll(int patternIdx = 0)
        {
            var maxScrollY = Math.Max(virtualSizeY + headerAndEffectSizeY - Height, 0);

            scrollX = GetPixelXForAbsoluteNoteIndex(Song.GetPatternStartAbsoluteNoteIndex(patternIdx), false);
            scrollY = maxScrollY / 2;

            var channel = Song.Channels[editChannel];
            var note = channel.FindPatternFirstMusicalNote(patternIdx);

            if (note != null)
            {
                int noteY = virtualSizeY - note.Value * noteSizeY;
                scrollY = noteY - (Height - headerAndEffectSizeY) / 2;
            }
        }

        private void SetMouseLastPos(int x, int y)
        {
            mouseLastX = x;
            mouseLastY = y;
        }

        private void SetFlingVelocity(float x, float y)
        {
            flingVelX = x;
            flingVelY = y;
        }

        internal Song Song
        {
            get { return videoSong != null ? videoSong : App?.SelectedSong; }
        }

        public Envelope EditEnvelope
        {
            get
            {
                if (editMode == EditionMode.Envelope)
                    return editInstrument?.Envelopes[(int)editEnvelope];
                else if (editMode == EditionMode.Arpeggio)
                    return editArpeggio.Envelope;
                else
                    return null;
            }
        }

        internal Envelope EditRepeatEnvelope
        {
            get
            {
                if (editMode == EditionMode.Envelope && editInstrument != null && Instrument.EnvelopeHasRepeat(editEnvelope))
                    return editInstrument.Envelopes[EnvelopeType.WaveformRepeat];
                return null;
            }
        }

        public bool HasRepeatEnvelope()
        {
            return EditRepeatEnvelope != null;
        }

        public void HighlightPianoNote(int note)
        {
            piano.HighlightNote = note;
        }

        public void Reset(int channelIdx)
        {
            // At this point, this is just a more agressive StartEditChannel().
            AbortCaptureOperation();
            showEffectsPanel = false;
            editInstrument = null;
            editArpeggio = null;
            zoom = DefaultChannelZoom;
            scrollX = 0;
            scrollY = 0;
            lastChannelScrollX = -1;
            lastChannelScrollY = -1;
            lastChannelZoom = -1;
            StartEditChannel(channelIdx);
        }

        public void SongModified()
        {
            ClearSelection();
            UpdateRenderCoords();
            MarkDirty();
        }

        public void SongChanged(int channelIdx)
        {
            if (editMode == EditionMode.Channel)
            {
                editMode = EditionMode.Channel;
                editChannel = channelIdx;
                editInstrument = null;
                editArpeggio = null;
                showEffectsPanel = false;
                scrollX = 0;
                ClearSelection();
                UpdateRenderCoords();
                MarkDirty();
            }
        }

        protected override void OnAddedToContainer()
        {
            UpdateRenderCoords();

            piano = new Piano(this);
            AddControl(piano);

            timeline = new PianoRollTimeline(this);
            timeline.SeekDragRequested  += Timeline_SeekDragRequested;
            timeline.SelectionRequested += Timeline_SelectionRequested;
            AddControl(timeline);

            envelopeEditor = new EnvelopeEditor(this);
            AddControl(envelopeEditor);

            waveEditor = new WaveEditor(this);
            AddControl(waveEditor);

            effectPanel = new EffectPanel(this);
            AddControl(effectPanel);

            noteArea = new NoteArea(this);
            AddControl(noteArea);

            var g = graphics;
            fontSmallCharSizeX = ParentWindow.Fonts.FontSmall.MeasureString("0", false);
            bmpExpandedSmall = g.GetTextureAtlasRef("ExpandedSmall");
            bmpSnap = g.GetTextureAtlasRef("Snap");
            bmpGizmoResizeLeftRight = g.GetTextureAtlasRef("GizmoResizeLeftRight");
            bmpGizmoResizeUpDown = g.GetTextureAtlasRef("GizmoResizeUpDown");
            bmpGizmoResizeFill = g.GetTextureAtlasRef("GizmoResizeFill");
            bmpEffectFrame = g.GetTextureAtlasRef("EffectFrame");
            bmpEffectRepeat = g.GetTextureAtlasRef("EffectRepeat");
            bmpEffects = g.GetTextureAtlasRefs(EffectType.Icons);

            if (Platform.IsMobile)
            {
                bitmapScale = DpiScaling.ScaleForWindowFloat(0.5f);
                effectBitmapScale = DpiScaling.ScaleForWindowFloat(0.25f);
            }

            ConditionalUpdateNoteGeometries(g);
            UpdateChildLayouts();
        }

        private void UpdateChildLayouts()
        {
            piano.UpdateLayout();
            timeline.UpdateLayout();
            envelopeEditor.UpdateLayout();
            waveEditor.UpdateLayout();
            effectPanel.UpdateLayout();
            noteArea.UpdateLayout();
        }

        private void ConditionalUpdateNoteGeometries(Graphics g)
        {
            if (geometryNoteSizeY == noteSizeY)
                return;

            geometryNoteSizeY = noteSizeY;

            stopNoteGeometry[0] = new float[]
            {
                0.0f, 0,
                0.0f, noteSizeY,
                1.0f, noteSizeY / 2
            };

            stopNoteGeometry[1] = new float[]
            {
                0.0f, 1,
                0.0f, noteSizeY,
                1.0f, noteSizeY / 2
            };

            releaseNoteGeometry[0] = new float[]
            {
                0.0f, 0,
                0.0f, noteSizeY,
                1.0f, noteSizeY - noteSizeY / 2 + releaseNoteSizeY / 2,
                1.0f, noteSizeY / 2 - releaseNoteSizeY / 2
            };

            releaseNoteGeometry[1] = new float[]
            {
                0.0f, 1,
                0.0f, noteSizeY,
                1.0f, noteSizeY - noteSizeY / 2 + releaseNoteSizeY / 2,
                1.0f, noteSizeY / 2 - releaseNoteSizeY / 2 + 1
            };

            stopReleaseNoteGeometry[0] = new float[]
            {
                0.0f, noteSizeY / 2 - releaseNoteSizeY / 2,
                0.0f, noteSizeY / 2 + releaseNoteSizeY / 2,
                1.0f, noteSizeY / 2
            };

            stopReleaseNoteGeometry[1] = new float[]
            {
                0.0f, noteSizeY / 2 - releaseNoteSizeY / 2 + 1,
                0.0f, noteSizeY / 2 + releaseNoteSizeY / 2,
                1.0f, noteSizeY / 2
            };

            slideNoteGeometry = new float[]
            {
                0.0f, 0,
                1.0f, 0,
                1.0f, noteSizeY
            };

            if (Platform.IsMobile)
            {
                mobileEraseGeometry = new float[2 * 64];
                for (var i = 0; i < mobileEraseGeometry.Length / 2; i++)
                {
                    var angle = i / 64.0f * MathF.PI * 2.0f;
                    mobileEraseGeometry[i * 2 + 0] = MathF.Cos(angle) * noteSizeY * 1.5f;
                    mobileEraseGeometry[i * 2 + 1] = MathF.Sin(angle) * noteSizeY * 1.5f;
                }
            }
        }

        public bool GetViewRange(ref int minNoteIdx, ref int maxNoteIdx, ref int channelIndex)
        {
            if (editMode == EditionMode.Channel && Width > pianoSizeX)
            {
                minNoteIdx = Math.Max(GetAbsoluteNoteIndexForPixelX(0), 0);
                maxNoteIdx = Math.Min(GetAbsoluteNoteIndexForPixelX(Width - pianoSizeX) + 1, Song.GetPatternStartAbsoluteNoteIndex(Song.Length));
                channelIndex = editChannel;

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool GetViewRange(ref float minNoteIdx, ref float maxNoteIdx, ref int channelIndex)
        {
            if (editMode == EditionMode.Channel && Width > pianoSizeX)
            {
                minNoteIdx = scrollX / noteSizeX;
                maxNoteIdx = (scrollX + Width - pianoSizeX) / noteSizeX;
                channelIndex = editChannel;

                return true;
            }

            return false;
        }

        private Color GetSeekBarColor()
        {
            if (editMode == EditionMode.Channel)
            {
                if (App.IsRecording)
                {
                    return Theme.DarkRedColor;
                }
                else if (App.IsSeeking)
                {
                    return Theme.Lighten(Theme.YellowColor, (int)(Math.Abs(Math.Sin(Platform.TimeSeconds() * 12.0)) * 75));
                }
            }

            return Theme.YellowColor;
        }

        public float GetSeekFrameToDraw()
        {
            return captureOperation == CaptureOperation.DragSeekBar ? dragSeekPosition : App.VisualCurrentFrame;
        }

        public bool CanEnvelopeDisplayFrame()
        {
            return editEnvelope != EnvelopeType.FdsModulation && editEnvelope != EnvelopeType.WaveformRepeat;
        }

        internal class RenderInfo
        {
            public int maxVisibleNote;
            public int minVisibleNote;
            public int maxVisibleOctave;
            public int minVisibleOctave;
            public int minVisiblePattern;
            public int maxVisiblePattern;
            public float minVisibleWaveTime;
            public float maxVisibleWaveTime;

            public Graphics g;
            public Fonts fonts;
            public CommandList b;
            public CommandList c;
            public CommandList f;
        }

        internal bool GetDPCMKeyColor(int note, out Color color)
        {
            if (editMode != EditionMode.VideoRecording && App.SelectedChannel.Type == ChannelType.Dpcm && App.SelectedInstrument != null)
            {
                var mapping = App.SelectedInstrument.GetDPCMMapping(note);
                if (mapping != null && mapping.Sample != null)
                {
                    color = Settings.DpcmColorMode == Settings.ColorModeSample ?
                        mapping.Sample.Color : App.SelectedInstrument.Color;
                    return true;
                }
            }

            color = Color.Invisible;
            return false;
        }

        private int GetEffectValueForPixelY(int y, int min, int max, float exp = 1.0f)
        {
            var alpha = MathF.Pow(Utils.Saturate(y / (float)effectPanelSizeY), 1.0f / exp);
            return (int)MathF.Round(Utils.Lerp(min, max, alpha));
        }

        public int GetPixelYForEffectValue(int val, int min, int max, float exp = 1.0f)
        {
            return (max == min) ? effectPanelSizeY : (int)MathF.Floor(MathF.Pow((val - min) / (float)(max - min), exp) * effectPanelSizeY);
        }

        private int GetPixelYForEffectValue(int effect, int value)
        {
            var channel = Song.Channels[editChannel];
            var minValue = Note.GetEffectMinValue(Song, channel, effect);
            var maxValue = Note.GetEffectMaxValue(Song, channel, effect);
            return GetPixelYForEffectValue(value, minValue, maxValue);
        }

        private Note[] GetSelectedNotes(bool clone = true)
        {
            if (!IsSelectionValid())
                return null;

            var notes = new Note[selectionMaxX - selectionMinX + 1];

            TransformNotes(selectionMinX, selectionMaxX, false, false, false, (note, idx) =>
            {
                if (note != null && clone)
                    notes[idx] = note.Clone();
                else
                    notes[idx] = note;

                return note;
            });

            return notes;
        }

        internal SortedList<int, Note> GetSparseSelectedNotes(int offset = 0, bool musicalOnly = false)
        {
            if (!IsSelectionValid())
                return null;

            var notes = new SortedList<int, Note>();

            TransformNotes(selectionMinX, selectionMaxX, false, false, false, (note, idx) =>
            {
                var selected = legacySelectMode || selectedNoteIndices.Contains(idx);

                if (selected && note != null && !note.IsEmpty && (note.IsMusical || !musicalOnly))
                    notes[idx + offset] = note.Clone();

                return note;
            });

            return notes;
        }

        private void CopyNotes()
        {
            if (legacySelectMode)
            {
                ClipboardUtils.SaveNotes(App.Project, GetSelectedNotes());
                return;
            }

            var channel = Song.Channels[editChannel];
            var copyMin = selectionMinX;
            var copyMax = selectionMaxX;
            var noteRanges = new List<(int min, int max)>();

            foreach (var idx in selectedNoteIndices)
            {
                var absoluteIdx = selectionMinX + idx;
                var location = NoteLocation.FromAbsoluteNoteIndex(Song, absoluteIdx);
                var note = channel.GetNoteAt(location);

                if (note == null || !note.IsMusical)
                    continue;

                var duration = Math.Max(1, GetVisualNoteDuration(location, note));
                var end = absoluteIdx + duration - 1;

                noteRanges.Add((absoluteIdx, end));
                copyMax = Math.Max(copyMax, end);
            }

            var notes = new Note[copyMax - copyMin + 1];

            TransformNotes(copyMin, copyMax, false, false, false, (note, idx) =>
            {
                if (note == null)
                    return note;

                var absoluteIdx = copyMin + idx;
                var selectedNoteIdx = absoluteIdx - selectionMinX;

                // Ensure we copy every effect of the selected note.
                if (selectedNoteIndices.Contains(selectedNoteIdx))
                {
                    notes[idx] = note.Clone();
                    return note;
                }

                var inNoteBody = false;

                foreach (var range in noteRanges)
                {
                    if (absoluteIdx >= range.min && absoluteIdx <= range.max)
                    {
                        inNoteBody = true;
                        break;
                    }
                }

                var selectedEffect = selectedEffectIdx >= 0 && selectedEffectIndices.Contains(absoluteIdx) && note.HasValidEffectValue(selectedEffectIdx);
                if (!inNoteBody && !selectedEffect)
                    return note;

                var clone = note.Clone();

                // We only want effects from the note.
                clone.Clear(true);

                if (!inNoteBody)
                {
                    for (var effectIdx = 0; effectIdx < Note.EffectCount; effectIdx++)
                    {
                        if (effectIdx != selectedEffectIdx)
                            clone.ClearEffectValue(effectIdx);
                    }
                }

                if (!clone.HasAnyEffect)
                    clone = null;

                notes[idx] = clone;

                return note;
            });

            ClipboardUtils.SaveNotes(App.Project, notes);
        }

        private void CutNotes()
        {
            CopyNotes();
            DeleteSelectedNotes();

            if (!legacySelectMode)
                ClearSelection();
        }

        private void ReplaceNotes(Note[] notes, int startFrameIdx, bool doTransaction, bool pasteNotes = true, int pasteFxMask = Note.EffectAllMask, bool mix = false)
        {
            TransformNotes(startFrameIdx, startFrameIdx + notes.Length - 1, doTransaction, true, true, (note, idx) =>
            {
                var channel = Song.Channels[editChannel];
                var newNote = notes[idx];

                if (!legacySelectMode && newNote == null)
                    return note;

                if (newNote == null)
                    newNote = Note.EmptyNote;

                if (note == null)
                    note = new Note();

                if (pasteNotes)
                {
                    if (!mix || !note.IsMusicalOrStop && newNote.IsMusicalOrStop)
                    {
                        note.Value = newNote.Value;

                        if (note.IsMusical)
                        {
                            note.Instrument = newNote.Instrument != null && channel.SupportsInstrument(newNote.Instrument) ? newNote.Instrument : null;
                            note.SlideNoteTarget = channel.SupportsSlideNotes ? newNote.SlideNoteTarget : (byte)0;
                            note.Flags = newNote.Flags;
                            note.Duration = newNote.Duration;
                            note.Release = channel.SupportsReleaseNotes ? newNote.Release : 0;
                            note.Arpeggio = channel.SupportsArpeggios ? newNote.Arpeggio : null;
                        }
                        else if (note.IsStop)
                        {
                            if (!channel.SupportsStopNotes)
                                return null;

                            note.Duration = 1;
                        }
                        else if (!note.IsValid)
                        {
                            // This will ensure the note is considered "Useless" and may therefore be deleted.
                            note.Instrument = null;
                            note.Duration = 0;
                            note.Release = 0;
                        }
                    }
                }

                for (int i = 0; i < Note.EffectCount; i++)
                {
                    if ((pasteFxMask & (1 << i)) != 0 && (!mix || !note.HasValidEffectValue(i) && newNote.HasValidEffectValue(i)))
                    {
                        note.ClearEffectValue(i);
                        if (channel.SupportsEffect(i) && newNote.HasValidEffectValue(i))
                        {
                            int clampedEffectValue = Note.ClampEffectValue(Song, channel, i, newNote.GetEffectValue(i));
                            note.SetEffectValue(i, clampedEffectValue);
                        }
                    }
                }

                return note;
            });

            SetSelection(startFrameIdx, startFrameIdx + notes.Length - 1);

            if (!legacySelectMode)
            {
                selectedNoteIndices.Clear();
                selectedEffectIndices.Clear();

                for (var i = 0; i < notes.Length; i++)
                {
                    var newNote = notes[i];
                    if (newNote == null || newNote.IsEmpty)
                        continue;

                    var absoluteIdx = startFrameIdx + i;

                    if (pasteNotes && newNote.IsMusicalOrStop)
                        selectedNoteIndices.Add(i);

                    if (selectedEffectIdx >= 0 && (pasteFxMask & (1 << selectedEffectIdx)) != 0 && newNote.HasValidEffectValue(selectedEffectIdx))
                    {
                        selectedEffectIndices.Add(absoluteIdx);
                    }
                }

                PostProcessSelection();
            }
        }

        private void PasteNotes(bool pasteNotes, int pasteFxMask, bool mix, int repeat, ClipboardImportFlags instImportFlags, ClipboardImportFlags arpImportFlags, ClipboardImportFlags sampleImportFlags, int padding = 0)
        {
            if (legacySelectMode && !IsSelectionValid())
                return;

            var createAnythingMissing =
                instImportFlags.HasFlag(ClipboardImportFlags.CreateMissing) ||
                arpImportFlags.HasFlag(ClipboardImportFlags.CreateMissing)  ||
                sampleImportFlags.HasFlag(ClipboardImportFlags.CreateMissing);

            var start = legacySelectMode ? selectionMinX : App.CurrentFrame;

            App.UndoRedoManager.BeginTransaction(createAnythingMissing ? TransactionScope.Project : TransactionScope.Channel, Song.Id, editChannel);

            for (int i = 0; i < repeat; i++)
            {
                var notes = ClipboardUtils.LoadNotes(App.Project, instImportFlags, arpImportFlags, sampleImportFlags);
                if (notes == null)
                {
                    App.UndoRedoManager.AbortTransaction();
                    return;
                }

                var clipboardHasNotes = false;
                var repeatLength = notes.Length;

                for (var j = 0; j < notes.Length; j++)
                {
                    var note = notes[j];
                    if (note != null && note.IsMusicalOrStop)
                    {
                        clipboardHasNotes = true;

                        if (!legacySelectMode)
                            repeatLength = Math.Max(repeatLength, j + note.Duration);
                    }
                }

                if (!legacySelectMode && pasteNotes && !mix)
                {
                    var channel = Song.Channels[editChannel];
                    var songEnd = Song.GetPatternStartAbsoluteNoteIndex(Song.Length);

                    for (var j = 0; j < notes.Length; j++)
                    {
                        var note = notes[j];
                        if (note == null || !note.IsMusical)
                            continue;

                        var noteStart = start + j;
                        var noteEnd = Math.Min(noteStart + note.Duration, songEnd);

                        if (noteEnd > noteStart + 1)
                            channel.DeleteNotesBetween(noteStart + 1, noteEnd, true);
                    }
                }

                ReplaceNotes(notes, start, false, pasteNotes && clipboardHasNotes, pasteFxMask, mix);

                if (i != repeat - 1)
                {
                    start += repeatLength + padding;
                }
            }

            NotesPasted?.Invoke();
            App.UndoRedoManager.EndTransaction();
        }
        
        private void PasteNotesWithConflictDialog(bool pasteNotes = true, int pasteFxMask = Note.EffectAllMask, bool mix = false, int repeat = 1, int padding = 0)
        {
            if (!ClipboardUtils.GetClipboardContentFlags(Song, true, out var instFlags, out var arpFlags, out var sampleFlags, out var patternFlags))
            {
                return;
            }

            var anyConflicts =
                instFlags    != ClipboardContentFlags.None ||
                arpFlags     != ClipboardContentFlags.None ||
                sampleFlags  != ClipboardContentFlags.None ||
                patternFlags != ClipboardContentFlags.None;

            if (pasteNotes && anyConflicts)
            {
                var dlg = new PasteConflictDialog(window, instFlags, arpFlags, sampleFlags);
                dlg.ShowDialogAsync((r) =>
                {
                    if (r == DialogResult.OK)
                    {
                        PasteNotes(pasteNotes, pasteFxMask, mix, repeat, dlg.InstrumentFlags, dlg.ArpeggioFlags, dlg.DPCMSampleFlags, padding);
                    }
                });
            }
            else
            {
                PasteNotes(pasteNotes, pasteFxMask, mix, repeat, ClipboardImportFlags.MatchByName, ClipboardImportFlags.MatchByName, ClipboardImportFlags.MatchByName, padding);
            }
        }

        private sbyte[] GetSelectedEnvelopeValues()
        {
            if (!IsSelectionValid())
                return null;

            var values = new sbyte[selectionMaxX - selectionMinX + 1];

            for (int i = selectionMinX; i <= selectionMaxX; i++)
                values[i - selectionMinX] = EditEnvelope.Values[i];

            return values;
        }

        private void CopyEffectValues(bool text)
        {
            if (editMode == EditionMode.Channel && selectedEffectIdx >= 0 && IsSelectionValid())
            {
                var channel = Song.Channels[editChannel];
                var location = NoteLocation.FromAbsoluteNoteIndex(Song, selectionMinX);
                var wantsPreviousValue = Note.EffectWantsPreviousValue(selectedEffectIdx);
                var prevVal = wantsPreviousValue ? channel.GetLastEffectValue(location, selectedEffectIdx) : Note.GetEffectDefaultValue(Song, selectedEffectIdx);
                var selectedNotes = GetSelectedNotes(false);
                var values = new int[selectedNotes.Length];

                for (int i = 0; i < selectedNotes.Length; i++)
                {
                    var note = selectedNotes[i];
                    if (note != null && note.HasValidEffectValue(selectedEffectIdx))
                    {
                        var val = note.GetEffectValue(selectedEffectIdx);
                        values[i] = val;
                        if (wantsPreviousValue)
                            prevVal = val;
                    }
                    else
                    {
                        values[i] = prevVal;
                    }
                }

                if (text)
                {
                    Platform.SetClipboardString(string.Join(" ", values));
                }
                else
                {
                    ClipboardUtils.SaveEnvelopeValues(values.Select(v => (sbyte)Utils.Clamp(v, sbyte.MinValue, sbyte.MaxValue)).ToArray());
                }
            }
        }

        private void CopyEnvelopeValues(bool text)
        {
            var values = GetSelectedEnvelopeValues();

            if (text)
                Platform.SetClipboardString(string.Join(" ", values));
            else
                ClipboardUtils.SaveEnvelopeValues(values);
        }

        private void CutEnvelopeValues()
        {
            CopyEnvelopeValues(false);
            DeleteSelectedEnvelopeValues();

            if (!legacySelectMode)
                ClearSelection();
        }

        private void ReplaceEnvelopeValues(sbyte[] values, int startIdx)
        {
            TransformEnvelopeValues(startIdx, startIdx + values.Length - 1, (val, idx) =>
            {
                return values[idx];
            });

            SetSelection(startIdx, startIdx + values.Length - 1);
        }

        private void PasteEnvelopeValues()
        {
            if (!IsSelectionValid())
                return;

            var values = ClipboardUtils.LoadEnvelopeValues();

            if (values == null)
                return;

            ReplaceEnvelopeValues(values, selectionMinX);
        }

        public void Copy()
        {
            if (editMode == EditionMode.Channel)
                CopyNotes();
            else if (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio)
                CopyEnvelopeValues(false);
        }

        public void CopyAsText()
        {
            if (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio)
                CopyEnvelopeValues(true);
        }

        public void Cut()
        {
            if (editMode == EditionMode.Channel)
                CutNotes();
            else if (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio)
                CutEnvelopeValues();
        }

        public void Paste()
        {
            AbortCaptureOperation();

            if (editMode == EditionMode.Channel)
                PasteNotesWithConflictDialog();
            else if (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio)
                PasteEnvelopeValues();
        }

        public void Delete()
        {
            if (editMode == EditionMode.Channel)
            {
                // We can delete effects separately if not using legacy mode.
                if (!legacySelectMode && selectedNoteIndices.Count == 0 && selectedEffectIndices.Count > 0)
                    ClearSelectedEffectValues();
                else
                    DeleteSelectedNotes();
            }
            else if (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio)
                DeleteSelectedEnvelopeValues();
            else if (editMode == EditionMode.DPCM)
                DeleteSelectedWaveSection();
        }

        public void PasteSpecial()
        {
            if (editMode == EditionMode.Channel)
            {
                AbortCaptureOperation();

                var dlg = new PasteSpecialDialog(ParentWindow, Song.Channels[editChannel], lastPasteSpecialPasteMix, lastPasteSpecialPasteNotes, lastPasteSpecialPasteEffectMask, !legacySelectMode);

                dlg.ShowDialogAsync((r) =>
                {
                    if (r == DialogResult.OK)
                    {
                        var effectMask = dlg.PasteEffectMask;

                        // Include volume slides if volume is selected.
                        if ((effectMask & Note.EffectVolumeMask) != 0 && Song.Channels[editChannel].SupportsEffect(Note.EffectVolumeSlide))
                            effectMask |= Note.EffectVolumeAndSlideMask;

                        PasteNotesWithConflictDialog(dlg.PasteNotes, effectMask, dlg.PasteMix, dlg.PasteRepeat, dlg.Padding);

                        lastPasteSpecialPasteMix = dlg.PasteMix;
                        lastPasteSpecialPasteNotes = dlg.PasteNotes;
                        lastPasteSpecialPasteEffectMask = dlg.PasteEffectMask;
                    }
                });
            }
        }

        public void DeleteSelection()
        {
            if (editMode == EditionMode.DPCM)
                DeleteSelectedWaveSection();
            else if (editMode == EditionMode.Channel)
            {
                // We can delete effects separately if not using legacy mode.
                if (!legacySelectMode && selectedNoteIndices.Count == 0 && selectedEffectIndices.Count > 0)
                    ClearSelectedEffectValues();
                else
                    DeleteSelectedNotes();
            }
            else if (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio)
                DeleteSelectedEnvelopeValues();
        }

        public void DeleteSpecial()
        {
            if (editMode == EditionMode.Channel)
            {
                AbortCaptureOperation();

                var dlg = new DeleteSpecialDialog(ParentWindow, Song.Channels[editChannel]);

                dlg.ShowDialogAsync((r) =>
                {
                    if (r == DialogResult.OK)
                        DeleteSelectedNotes(true, dlg.DeleteNotes, dlg.DeleteEffectMask);
                });
            }
        }

        internal void PromoteTransaction(TransactionScope scope, int objectId = -1, int subIdx = -1)
        {
            // HACK : When promoting transaction, we end up saving the app state at the 
            // moment when the transaction is promoted. This lead to selections being in
            // the wrong place. 
            var tempSelectionMin = selectionMinX;
            var tempSelectionMax = selectionMaxX;

            selectionMinX = captureSelectionMinX;
            selectionMaxX = captureSelectionMaxX;

            App.UndoRedoManager.AbortTransaction();
            App.UndoRedoManager.BeginTransaction(scope, objectId, subIdx);

            selectionMinX = tempSelectionMin;
            selectionMaxX = tempSelectionMax;
        }

        internal bool IsNoteSelected(int absoluteNoteIdx)
        {
            if (IsSelectionValid())
                return absoluteNoteIdx >= selectionMinX && absoluteNoteIdx <= selectionMaxX;
            else
                return false;
        }

        internal bool IsNoteSelected(NoteLocation location, int duration = 0)
        {
            var absoluteIdx = location.ToAbsoluteNoteIndex(Song);

            if (legacySelectMode)
                return IsNoteSelected(absoluteIdx);

            return selectedNoteIndices.Contains(absoluteIdx - selectionMinX);
        }

        public bool IsEffectFrameSelected(int absoluteIdx)
        {
            if (!IsSelectionValid())
                return false;

            if (legacySelectMode || captureSelectionFromHeader)
            {
                return absoluteIdx >= selectionMinX && absoluteIdx <= selectionMaxX;
            }

            if (captureOperation == CaptureOperation.Select && captureSelectionFromEffectPanel)
            {
                if (ModifierKeys.IsControlDown)
                {
                    return captureSelectedEffectIndices.Contains(absoluteIdx) || (absoluteIdx >= captureMarqueeMinX && absoluteIdx <= captureMarqueeMaxX);
                }

                return absoluteIdx >= captureMarqueeMinX && absoluteIdx <= captureMarqueeMaxX;
            }

            return selectedEffectIndices.Contains(absoluteIdx);
        }

        public bool IsEnvelopeValueSelected(int idx)
        {
            if (!IsSelectionValid())
                return false;

            if (legacySelectMode)
                return idx >= selectionMinX && idx <= selectionMaxX;

            return selectedEnvelopeIndices.Contains(idx);
        }

        private bool GetRepeatEnvelopeSelectionMinMax(out int min, out int max)
        {
            min = -1;
            max = -1;

            var rep = EditRepeatEnvelope;
            var env = EditEnvelope;

            if (IsSelectionValid() && rep != null)
            {
                min = selectionMinX / env.ChunkLength;
                max = selectionMaxX / env.ChunkLength;

                return true;
            }

            return false;
        }

        public bool IsEnvelopeRepeatValueSelected(int idx)
        {
            if (GetRepeatEnvelopeSelectionMinMax(out var min, out var max))
            {
                return idx >= min && idx <= max;
            }

            return false;
        }

        internal void DrawSelectionRect(CommandList c, int height, bool effectsPanel = false, bool header = false)
        {
            if (legacySelectMode)
            {
                if (!IsSelectionValid())
                    return;
            }
            else
            {
                if (captureOperation != CaptureOperation.Select || captureMarqueeMinX < 0 || captureMarqueeMaxX < 0)
                    return;
            }

            var color = IsActiveControl ? selectionBgVisibleColor : selectionBgInvisibleColor;

            var drawMinX = selectionMinX;
            var drawMaxX = selectionMaxX;
            var drawMinY = selectionMinY;
            var drawMaxY = selectionMaxY;

            if (!legacySelectMode && (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio))
            {
                c.FillRectangle(
                    GetPixelXForAbsoluteNoteIndex(captureMarqueeMinX) + 1,  0,
                    GetPixelXForAbsoluteNoteIndex(captureMarqueeMaxX  + 1), height,
                    color);

                return;
            }

            if (editMode == EditionMode.Channel && !legacySelectMode)
            {
                if (header && !captureSelectionFromHeader)
                    return;

                // Modern channel selection rectangles are temporary while dragging.
                if (captureOperation == CaptureOperation.Select)
                {
                    drawMinX = captureMarqueeMinX;
                    drawMaxX = captureMarqueeMaxX;

                    if (!captureSelectionFromHeader && !captureSelectionFromEffectPanel)
                    {
                        drawMinY = captureMarqueeMinY;
                        drawMaxY = captureMarqueeMaxY;
                    }
                }

                if (captureSelectionFromEffectPanel)
                {
                    if (!effectsPanel)
                        return;

                    c.FillRectangle(
                        GetPixelXForAbsoluteNoteIndex(drawMinX) + 1,  0,
                        GetPixelXForAbsoluteNoteIndex(drawMaxX  + 1), height,
                        color);

                    return;
                }

                if (!captureSelectionFromHeader)
                {
                    if (effectsPanel)
                        return;

                    var y0 = GetPixelYForNoteValue(drawMaxY);
                    var y1 = GetPixelYForNoteValue(drawMinY) + noteSizeY;

                    c.FillRectangle(
                        GetPixelXForAbsoluteNoteIndex(drawMinX) + 1,  y0,
                        GetPixelXForAbsoluteNoteIndex(drawMaxX  + 1), y1,
                        color);

                    return;
                }
            }

            c.FillRectangle(
                GetPixelXForAbsoluteNoteIndex(drawMinX) + 1,  0,
                GetPixelXForAbsoluteNoteIndex(drawMaxX  + 1), height,
                color);
        }

        internal bool IsGizmoHighlighted(Gizmo g, int offsetY)
        {
            if (Platform.IsMobile)
            {
                return true;
            }
            else
            {
                var pt = ScreenToControl(CursorPosition);

                if (g.Rect.Contains(pt.X - pianoSizeX, pt.Y - offsetY))
                    return true;

                if (g.Action == GizmoAction.MoveSlide && captureOperation == CaptureOperation.DragSlideNoteTargetGizmo ||
                    g.Action == GizmoAction.MoveVolumeSlideValue && captureOperation == CaptureOperation.DragVolumeSlideTargetGizmo)
                    return true;
            }
            return false;
        }

        public float GetPixelForWaveTime(float time, int scroll = 0)
        {
            var viewSize = Width - pianoSizeX;
            var viewTime = DefaultZoomWaveTime / zoom;

            return time / viewTime * viewSize - scroll;
        }

        public float GetWaveTimeForPixel(int x)
        {
            var viewSize = Width - pianoSizeX;
            var viewTime = DefaultZoomWaveTime / zoom;

            return (x + scrollX) / (float)viewSize * viewTime;
        }

        public float GetWaveTimeForSample(int sampleIndex, bool min)
        {
            // The first sample in a DMC is the initial DPCM counter, its
            // not really part of the data.
            if (!editSample.SourceDataIsWav)
                sampleIndex++;

            float offset = min ? -0.5f : 0.5f;
            return (sampleIndex + offset) / editSample.SourceSampleRate;
        }

        public void RenderVideoFrame(Graphics g, int channel, long forceDisplayMask, int patternIndex, float noteIndex, float centerNote, (int, Color)[] highlightKeys)
        {
            Debug.Assert(editMode == EditionMode.VideoRecording);

            int noteY = (int)Math.Round(virtualSizeY - centerNote * noteSizeY + noteSizeY / 2);

            editChannel = channel;
            scrollX = (int)Math.Round((Song.GetPatternStartAbsoluteNoteIndex(patternIndex) + noteIndex) * (double)noteSizeX);
            scrollY = noteY - (Height - headerAndEffectSizeY) / 2;
            piano.VideoHighlightKeys = highlightKeys;
            videoForceDisplayChannelMask = forceDisplayMask;

            OnRender(g);
        }

        private void RenderScrollBars(RenderInfo r)
        {
            if (Settings.ScrollBars != Settings.ScrollBarsNone && editMode != EditionMode.VideoRecording)
            {
                if (GetScrollBarParams(true, out var scrollBarThumbPosX, out var scrollBarThumbSizeX, out var scrollBarSizeX))
                {
                    r.c.PushTranslation(pianoSizeX - 1, 0);
                    r.c.FillAndDrawRectangle(0, Height - scrollBarThickness, scrollBarSizeX, Height, Theme.DarkGreyColor4, Theme.BlackColor);
                    r.c.FillAndDrawRectangle(scrollBarThumbPosX, Height - scrollBarThickness, scrollBarThumbPosX + scrollBarThumbSizeX, Height, Theme.MediumGreyColor1, Theme.BlackColor);
                    r.c.PopTransform();
                }

                if (GetScrollBarParams(false, out var scrollBarThumbPosY, out var scrollBarThumbSizeY, out var scrollBarSizeY))
                {
                    r.c.PushTranslation(0, headerAndEffectSizeY - 1);
                    r.c.FillAndDrawRectangle(Width - scrollBarThickness, 0, Width, scrollBarSizeY, Theme.DarkGreyColor4, Theme.BlackColor);
                    r.c.FillAndDrawRectangle(Width - scrollBarThickness, scrollBarThumbPosY, Width, scrollBarThumbPosY + scrollBarThumbSizeY, Theme.MediumGreyColor1, Theme.BlackColor);
                    r.c.PopTransform();
                }
            }
        }

        private void RenderDebug(RenderInfo r)
        {
#if DEBUG
            if (Platform.IsMobile)
            {
                r.g.OverlayCommandList.FillRectangle(mouseLastX - 30, mouseLastY - 30, mouseLastX + 30, mouseLastY + 30, Theme.WhiteColor);
            }
#endif
        }

#if DEBUG
        // OpenGL line rasterization tests.
        private void RenderLineDebug(Graphics g)
        {
#if false
            for (int i = 1; i <= 8; i++)
            {
                g.Transform.PushTranslation(i * 110, 100);
                for (int j = 0; j < 16; j++)
                {
                    var cos = (float)Math.Cos(j / 16.0 * Math.PI * 2.0);
                    var sin = (float)Math.Sin(j / 16.0 * Math.PI * 2.0);

                    g.DefaultCommandList.DrawLine(0, 0, cos * 50, sin * 50, new Color(255, 0, 255), i, true);
                    g.ForegroundCommandList.DrawLine(0, 0, cos * 50, sin * 50, new Color(0, 255, 0), 1, false);
                }

                g.DefaultCommandList.DrawRectangle(-50, 100, 50, 110, new Color(255, 0, 255), i, true);
                g.ForegroundCommandList.DrawRectangle(-50, 100, 50, 110, new Color(0, 255, 0), 1, false);
                g.Transform.PopTransform();
            }
#elif false
            releaseNoteGeometry[0][4] = 10.0f;
            releaseNoteGeometry[0][6] = 10.0f;
            for (int i = 1; i <= 8; i++)
            {
                g.Transform.PushTranslation(i * 200, 100);
                g.OverlayCommandList.DrawLine(0, 0, 100, 100, new Color(255, 0, 255), i, true);
                g.OverlayCommandList.DrawLine(0, 0, 100, 0, new Color(255, 0, 255), i, true);
                g.OverlayCommandList.DrawLine(0, 0, 0, 100, new Color(255, 0, 255), i, true);
                g.OverlayCommandList.FillAndDrawRectangle(0, 200, 100, 210, new Color(255, 0, 255), new Color(0, 255, 0), i, true, i > 1);
                g.Transform.PushTranslation(0, 110);
                g.OverlayCommandList.FillGeometry(releaseNoteGeometry[0], Color.Pink, true);
                g.Transform.PushTranslation(0, 20);
                g.OverlayCommandList.DrawGeometry(releaseNoteGeometry[0], Color.SpringGreen, i, true, true);
                g.Transform.PopTransform();
                g.Transform.PopTransform();
                g.Transform.PopTransform();
                g.Transform.PushTranslation(i * 200, 500);
                g.OverlayCommandList.DrawLine(0, 0, 100, 100, new Color(255, 0, 255), i, false);
                g.OverlayCommandList.DrawLine(0, 0, 100, 0, new Color(255, 0, 255), i, false);
                g.OverlayCommandList.DrawLine(0, 0, 0, 100, new Color(255, 0, 255), i, false);
                g.OverlayCommandList.DrawLine(0, 0, 100, 100, new Color(0, 255, 0), 1, false);
                g.OverlayCommandList.DrawLine(0, 0, 100, 0, new Color(0, 255, 0), 1, false);
                g.OverlayCommandList.DrawLine(0, 0, 0, 100, new Color(0, 255, 0), 1, false);
                g.OverlayCommandList.FillAndDrawRectangle(0, 200, 100, 210, new Color(255, 0, 255), new Color(0, 255, 0), i, false);
                g.Transform.PushTranslation(0, 110);
                g.OverlayCommandList.FillGeometry(releaseNoteGeometry[0], Color.Pink, false);
                g.Transform.PushTranslation(0, 20);
                g.OverlayCommandList.DrawGeometry(releaseNoteGeometry[0], Color.SpringGreen, i, false, true);
                g.Transform.PopTransform();
                g.Transform.PopTransform();
                g.Transform.PopTransform();
            }
#else

            var rnd = new Random(1003);

            for (int j = 0; j < 2; j++)
            { 
                var points = new float[50 * 2];
                var x = 30;

                for (int i = 0; i < points.Length / 2; i++)
                {
                    points[i * 2 + 0] = x;
                    points[i * 2 + 1] = rnd.Next(100, 400) + j * 300;

                    x += rnd.Next(10, 50);
                }

                //var points = new float [] { 100, 100, 200, 200, 200, 100 };

                //g.OverlayCommandList.DrawGeometry(points, Color.Pink, 16, true, false);
                g.OverlayCommandList.DrawNiceSmoothLine(points, Color.Pink, 6);
            }
#endif
        }

        private void RenderTextDebug(Graphics g)
        {
            var strings = new[]
            {
                "Noise",
                "Triangle",
                "DPCM",
                "Square"
            };

            for (int i = 0; i < strings.Length; i++)
            {
                g.DefaultCommandList.DrawText(strings[i], Fonts.FontMedium, 20, 20 + i * Fonts.FontMedium.LineHeight, Color.White);
            }
        }
#endif

        protected override void OnRender(Graphics g)
        {
            var r = new RenderInfo();

            var minVisibleNoteIdx = Math.Max(GetAbsoluteNoteIndexForPixelX(0), 0);
            var maxVisibleNoteIdx = Math.Min(GetAbsoluteNoteIndexForPixelX(Width) + 1, Song.GetPatternStartAbsoluteNoteIndex(Song.Length));

            r.g = g;
            r.fonts = Fonts;
            r.b = g.BackgroundCommandList;
            r.c = g.DefaultCommandList;
            r.f = g.ForegroundCommandList;

            var minNote = editMode == EditionMode.VideoRecording ? -10000 : 0;
            var maxNote = editMode == EditionMode.VideoRecording ?  10000 : NumNotes;

            r.maxVisibleNote = NumNotes - Utils.Clamp((int)Math.Floor(scrollY / (float)noteSizeY), minNote, maxNote);
            r.minVisibleNote = NumNotes - Utils.Clamp((int)Math.Ceiling((scrollY + Height - headerAndEffectSizeY) / (float)noteSizeY), minNote, maxNote);

            r.maxVisibleOctave = (int)Math.Ceiling(r.maxVisibleNote / 12.0f);
            r.minVisibleOctave = (int)Math.Floor(r.minVisibleNote / 12.0f);

            r.minVisiblePattern = Utils.Clamp(Song.PatternIndexFromAbsoluteNoteIndex(minVisibleNoteIdx), 0, Song.Length);
            r.maxVisiblePattern = Utils.Clamp(Song.PatternIndexFromAbsoluteNoteIndex(maxVisibleNoteIdx) + 1, 0, Song.Length);

            if (editMode == EditionMode.DPCM)
            {
                r.minVisibleWaveTime = GetWaveTimeForPixel(0);
                r.maxVisibleWaveTime = GetWaveTimeForPixel(Width - pianoSizeX);
            }

            ConditionalUpdateNoteGeometries(g);

            // TODO: This doesn't make sense in the long run, just temporary for refactoring.
            var envelopeMode = editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio;

            //if (pianoVisible)
                //RenderEffectList(r);

            //RenderEffectPanel(r);

            if (!envelopeMode)
            {
                //RenderNoteArea(r);
                //RenderWaveform(r);
            }

            RenderScrollBars(r);
            RenderDebug(r);

            base.OnRender(g);
        }

        private bool GetScrollBarParams(bool horizontal, out int thumbPos, out int thumbSize, out int scrollSize)
        {
            thumbPos   = 0;
            thumbSize  = 0;
            scrollSize = 0;

            if (scrollBarThickness > 0)
            {
                if (horizontal)
                {
                    GetMinMaxScroll(out var minScrollX, out _, out var maxScrollX, out _);

                    if (minScrollX == maxScrollX)
                        return false;

                    scrollSize = Width - pianoSizeX + 1;
                    thumbSize = Math.Max(minScrollBarLength, (int)Math.Round(scrollSize * Math.Min(1.0f, scrollSize / (float)(maxScrollX + scrollSize))));
                    thumbPos  = (int)Math.Round((scrollSize - thumbSize) * (scrollX / (float)maxScrollX));
                    return true;
                }
                else
                {
                    GetMinMaxScroll(out _, out var minScrollY, out _, out var maxScrollY);

                    if (minScrollY == maxScrollY)
                        return false;

                    scrollSize = Height - headerAndEffectSizeY - scrollBarThickness + 1;
                    thumbSize = Math.Max(minScrollBarLength, (int)Math.Round(scrollSize * Math.Min(1.0f, scrollSize / (float)(maxScrollY + scrollSize))));
                    thumbPos  = (int)Math.Round((scrollSize - thumbSize) * (scrollY / (float)maxScrollY));
                    return true;
                }
            }

            return false;
        }

        void ResizeEnvelope(int x, int y, bool final)
        {
            var env = EditEnvelope;
            var length = Utils.RoundDown(GetAbsoluteNoteIndexForPixelX(x - pianoSizeX), env.ChunkLength);

            ScrollIfNearEdge(x, y);

            switch (captureOperation)
            {
                case CaptureOperation.ResizeEnvelope:
                    if (env.Length != length)
                    {
                        env.Length = length;
                        editInstrument?.NotifyEnvelopeChanged(editEnvelope, false); // Instrument is null when editing arps.
                        if (IsSelectionValid())
                            SetSelection(selectionMinX, selectionMaxX);
                    }
                    break;
                case CaptureOperation.DragRelease:
                    if (env.Release != length && length > 0)
                    {
                        env.Release = length;
                        editInstrument?.NotifyEnvelopeChanged(editEnvelope, false); // Instrument is null when editing arps.
                    }
                    break;
                case CaptureOperation.DragLoop:
                    if (env.Loop != length)
                    {
                        env.Loop = length;
                        editInstrument?.NotifyEnvelopeChanged(editEnvelope, false); // Instrument is null when editing arps.
                    }
                    break;
            }

            ClampScroll();
            MarkDirty();

            if (final)
            {
                EnvelopeChanged?.Invoke(editInstrument, editEnvelope);
                App.UndoRedoManager.EndTransaction();
            }
        }

        public void StartChangeEffectValue(int x, int y, NoteLocation location)
        {
            var channel   = Song.Channels[editChannel];
            var pattern   = channel.PatternInstances[location.PatternIndex];
            var note      = channel.GetNoteAt(location);
            var hasValue  = note != null && note.HasValidEffectValue(selectedEffectIdx);
            var selection = IsEffectFrameSelected(location.ToAbsoluteNoteIndex(Song)) && hasValue;
            var newSelect = selection && !legacySelectMode;

            if (!legacySelectMode && !hasValue)
                ClearSelection();

            captureEffectSelectionMove = false;

            StartCaptureOperation(x, y, newSelect ? CaptureOperation.EditEffectSelection : selection ? CaptureOperation.ChangeSelectionEffectValue : CaptureOperation.ChangeEffectValue, false, location.ToAbsoluteNoteIndex(Song));

            // Use channel scope for the experimental effect drag.
            // This lets effects cross pattern boundaries without transaction promotion.
            if (newSelect)
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);

                dragEffects.Clear();

                foreach (var absoluteIdx in selectedEffectIndices)
                {
                    var effectLocation = NoteLocation.FromAbsoluteNoteIndex(Song, absoluteIdx);
                    var effectNote = channel.GetNoteAt(effectLocation);

                    if (effectNote != null && effectNote.HasValidEffectValue(selectedEffectIdx))
                    {
                        dragEffects[absoluteIdx] = effectNote.GetEffectValue(selectedEffectIdx);
                    }
                }
            }
            else
            {
                var minPatternIdx = Song.PatternIndexFromAbsoluteNoteIndex(selectionMinX);
                var maxPatternIdx = Song.PatternIndexFromAbsoluteNoteIndex(selectionMaxX);

                if (selection && minPatternIdx != maxPatternIdx || pattern == null)
                {
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);

                    if (pattern == null)
                        pattern = channel.CreatePatternAndInstance(location.PatternIndex);
                }
                else
                {
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
                }

                UpdateChangeEffectValue(x, y);
            }
        }

        public float GetEffectValueExponent(int maxValue)
        {
            return 1.0f / (maxValue >= 4095 ? 4 : 1);
        }

        private void UpdateEditEffectSelection(int x, int y)
        {
            if (!captureEffectSelectionMove)
            {
                var dx = Math.Abs(x - captureMouseX);
                var dy = Math.Abs(y - captureMouseY);

                if (dx > dy)
                {
                    captureEffectSelectionMove = true;
                }
                else
                {
                    captureOperation = CaptureOperation.ChangeSelectionEffectValue;
                    UpdateChangeEffectValue(x, y);
                    return;
                }
            }

            UpdateEffectSelectionMove(x);
        }

        private void UpdateEffectSelectionMove(int x)
        {
            App.UndoRedoManager.RestoreTransaction(false);

            var channel = Song.Channels[editChannel];
            var songEnd = Song.GetPatternStartAbsoluteNoteIndex(Song.Length);

            var currentAbsoluteIdx = GetAbsoluteNoteIndexForPixelX(x - pianoSizeX);
            var deltaAbsoluteIdx = currentAbsoluteIdx - captureMouseAbsoluteIdx;
            var newCaptureIdx = captureNoteAbsoluteIdx + deltaAbsoluteIdx;
            var delta =  SnapNote(newCaptureIdx) - captureNoteAbsoluteIdx;

            // Don't allow snapping to send the selection opposite the mouse direction.
            if (Math.Sign(deltaAbsoluteIdx) != Math.Sign(delta))
                delta = 0;

            ClearEffects(channel, dragEffects);
            PlaceEffects(channel, dragEffects, delta);

            selectedEffectIndices.Clear();

            selectionMinX = int.MaxValue;
            selectionMaxX = int.MinValue;

            foreach (var kv in dragEffects)
            {
                var absoluteIdx = kv.Key + delta;
                if (absoluteIdx < 0 || absoluteIdx >= songEnd)
                    continue;

                selectedEffectIndices.Add(absoluteIdx);

                selectionMinX = Math.Min(selectionMinX, absoluteIdx);
                selectionMaxX = Math.Max(selectionMaxX, absoluteIdx);
            }

            if (selectedEffectIndices.Count == 0)
            {
                ClearSelection();
            }
            else
            {
                channel.InvalidateCumulativePatternCache(
                    Song.PatternIndexFromAbsoluteNoteIndex(selectionMinX),
                    Song.PatternIndexFromAbsoluteNoteIndex(selectionMaxX));
            }

            MarkDirty();
        }

        void UpdateChangeEffectValue(int x, int y)
        {
            Debug.Assert(selectedEffectIdx >= 0);

            App.UndoRedoManager.RestoreTransaction(false);

            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[captureNoteLocation.PatternIndex];
            var location = captureNoteLocation;
            var captureEffectValue = 0;

            var min = Note.GetEffectMinValue(Song, channel, selectedEffectIdx);
            var max = Note.GetEffectMaxValue(Song, channel, selectedEffectIdx);
            var exp = GetEffectValueExponent(max);

            if (pattern == null || !pattern.TryGetNoteWithEffectAt(location.NoteIndex, selectedEffectIdx, out var note))
            {
                if (SnapEnabled && SnapEffectEnabled)
                    location = SnapNote(location);

                if (pattern == null)
                    pattern = channel.CreatePatternAndInstance(location.PatternIndex);

                captureEffectValue = GetEffectValueForPixelY(effectPanelSizeY - (captureMouseY - headerSizeY), min, max, exp);
                note = pattern.GetOrCreateNoteAt(location.NoteIndex);
                note.SetEffectValue(selectedEffectIdx, captureEffectValue);
            }
            else
            {
                captureEffectValue = note.GetEffectValue(selectedEffectIdx);
            }

            var delta = 0;
            var originalValue = note.GetEffectValue(selectedEffectIdx);

            // On mobile, we use gizmos, so let's pretend the drag is happening at the effect value instead of at the gizmo position.
            if (Platform.IsMobile)
                y += headerAndEffectSizeY - GetPixelYForEffectValue(captureEffectValue, min, max, exp) - captureMouseY;

            if (ModifierKeys.IsControlDown)
            {
                delta = (captureMouseY - y) / 4;
            }
            else 
            {
                var ratio = 1.0f - Utils.Saturate(((y - headerSizeY) / (float)effectPanelSizeY));
                var newValue = (int)Math.Round(Utils.Lerp(min, max, MathF.Pow(ratio, 1.0f / exp)));

                delta = newValue - originalValue;
            }

            if (captureOperation == CaptureOperation.ChangeSelectionEffectValue)
            {
                var scaling = (Utils.Clamp(originalValue + delta, min, max) - min) / (float)(originalValue - min);
                var minLocation = NoteLocation.FromAbsoluteNoteIndex(Song, selectionMinX);
                var maxLocation = NoteLocation.FromAbsoluteNoteIndex(Song, selectionMaxX);
                var processedNotes = new HashSet<Note>();

                for (var it = channel.GetSparseNoteIterator(minLocation, maxLocation, Note.GetFilterForEffect(selectedEffectIdx)); !it.Done; it.Next())
                {
                    var absoluteIdx = it.Location.ToAbsoluteNoteIndex(Song);

                    if (!IsEffectFrameSelected(absoluteIdx))
                        continue;

                    if (!processedNotes.Contains(it.Note))
                    {
                        var value = it.Note.GetEffectValue(selectedEffectIdx);

                        if (relativeEffectScaling)
                        {
                            it.Note.SetEffectValue(selectedEffectIdx, Utils.Clamp((int)Math.Round((value - min) * scaling + min), min, max));
                        }
                        else
                        {
                            it.Note.SetEffectValue(selectedEffectIdx, Utils.Clamp(value + delta, min, max));
                        }

                        processedNotes.Add(it.Note);
                    }
                }

                channel.InvalidateCumulativePatternCache(minLocation.PatternIndex, maxLocation.PatternIndex);
            }
            else
            {
                var value = note.GetEffectValue(selectedEffectIdx);
                note.SetEffectValue(selectedEffectIdx, Utils.Clamp(value + delta, min, max));

                channel.InvalidateCumulativePatternCache(pattern);
            }

            if (!legacySelectMode && selectedNoteIndices.Count > 0)
            {
                UpdateSelectedEffectsFromNotes();
            }

            MarkDirty();
        }

        void StartChangeEnvelopeRepeatValue(int x, int y)
        {
            StartCaptureOperation(x, y, CaptureOperation.ChangeEnvelopeRepeatValue, false, GetAbsoluteNoteIndexForPixelX(x - pianoSizeX) / EditEnvelope.ChunkLength);
            App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id);
            UpdateChangeEnvelopeRepeatValue(x, y);
        }

        void UpdateChangeEnvelopeRepeatValue(int x, int y)
        {
            App.UndoRedoManager.RestoreTransaction(false);

            var env = EditEnvelope;
            var rep = EditRepeatEnvelope;
            var idx = Utils.Clamp(captureMouseAbsoluteIdx, 0, env.Length);

            idx /= env.ChunkLength;

            Envelope.GetMinMaxValueForType(editInstrument, EnvelopeType.WaveformRepeat, out var minRepeat, out var maxRepeat);

            var originalValue = rep.Values[idx];
            var delta = 0;

            // On mobile, we use gizmos, so let's pretend the drag is happening at the effect value instead of at the gizmo position.
            if (Platform.IsMobile)
                y += headerAndEffectSizeY - GetPixelYForEffectValue(originalValue, minRepeat, maxRepeat) - captureMouseY;

            var ratio = (y - headerSizeY) / (float)effectPanelSizeY;
            var newValue = (int)Math.Round(Utils.Lerp(maxRepeat, minRepeat, ratio));

            delta = newValue - originalValue;

            if (IsSelectionValid() && IsEnvelopeRepeatValueSelected(idx))
            {
                GetRepeatEnvelopeSelectionMinMax(out var min, out var max);

                for (int i = min; i <= max; i++)
                    rep.Values[i] = (sbyte)Utils.Clamp(rep.Values[i] + delta, minRepeat, maxRepeat);
            }
            else
            {
                rep.Values[idx] = (sbyte)Utils.Clamp(rep.Values[idx] + delta, minRepeat, maxRepeat);
            }

            MarkDirty();
        }

        public void StartDragVolumeSlide(int x, int y, NoteLocation location)
        {
            StartCaptureOperation(x, y, CaptureOperation.DragVolumeSlideTarget, false, location.ToAbsoluteNoteIndex(Song));

            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[location.PatternIndex];

            if (pattern == null)
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
                pattern = channel.CreatePatternAndInstance(location.PatternIndex);
            }
            else
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
            }

            var note = pattern.GetOrCreateNoteAt(captureNoteLocation.NoteIndex);

            var ratio = Utils.Clamp(1.0f - (y - headerSizeY) / (float)effectPanelSizeY, 0.0f, 1.0f);
            var volume = (byte)Math.Round(ratio * Note.VolumeMax);

            if (!note.HasVolume)
                note.Volume = volume;
            note.VolumeSlideTarget = volume;

            pattern.InvalidateCumulativeCache();
        }

        void StartDragVolumeSlideGizmo(int x, int y, Note note, NoteLocation location)
        {
            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[location.PatternIndex];
            var offsetY = headerSizeY + effectPanelSizeY - GetPixelYForEffectValue(Note.EffectVolumeSlide, note.VolumeSlideTarget) - y;
           
            StartCaptureOperation(x, y, CaptureOperation.DragVolumeSlideTargetGizmo, false, location.ToAbsoluteNoteIndex(Song), 0, offsetY);
            App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
        }

        void UpdateDragVolumeSlide(int x, int y, bool final)
        {
            if (Platform.IsMobile)
                App.UndoRedoManager.RestoreTransaction(false);

            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[captureNoteLocation.PatternIndex];
            var note    = pattern.Notes[captureNoteLocation.NoteIndex];

            // On mobile, we use gizmos, so let's pretend the drag is happening at the effect value instead of at the gizmo position.
            if (Platform.IsMobile)
                y += headerAndEffectSizeY - GetPixelYForEffectValue(note.VolumeSlideTarget, 0, Note.VolumeMax) - captureMouseY;

            var ratio = Utils.Clamp(1.0f - (y - headerSizeY) / (float)effectPanelSizeY, 0.0f, 1.0f);
            note.VolumeSlideTarget = (byte)Math.Round(ratio * Note.VolumeMax);

            if (final)
            {
                if (note.VolumeSlideTarget == note.Volume)
                    note.HasVolumeSlide = false;

                pattern.InvalidateCumulativeCache();
                App.UndoRedoManager.EndTransaction();
            }
            else
            {
                pattern.InvalidateCumulativeCache();
            }

            MarkDirty();
        }

        void DrawEnvelope(int x, int y, bool first = false, bool final = false)
        {
            ScrollIfNearEdge(x, y);

            if (GetEnvelopeValueForCoord(x, y, out int idx1, out sbyte val1))
            {
                int idx0;
                sbyte val0;

                if (first || !GetEnvelopeValueForCoord(mouseLastX, mouseLastY, out idx0, out val0))
                {
                    idx0 = idx1;
                    val0 = val1;
                }

                Envelope.GetMinMaxValueForType(editInstrument, editEnvelope, out int min, out int max);

                var env = EditEnvelope;

                idx0 = Utils.Clamp(idx0, 0, env.Length - 1);
                idx1 = Utils.Clamp(idx1, 0, env.Length - 1);

                if (idx0 != idx1)
                {
                    if (idx1 < idx0)
                    {
                        Utils.Swap(ref idx0, ref idx1);
                        Utils.Swap(ref val0, ref val1);
                    }

                    for (int i = idx0; i <= idx1; i++)
                    {
                        int val = (int)Math.Round(Utils.Lerp(val0, val1, (i - idx0) / (float)(idx1 - idx0)));
                        env.Values[i] = (sbyte)Utils.Clamp(val, min, max);
                    }
                }
                else
                {
                    env.Values[idx0] = (sbyte)Utils.Clamp(val0, min, max);
                }

                MarkDirty();
            }

            if (final)
            {
                editInstrument?.NotifyEnvelopeChanged(editEnvelope, true);
                EnvelopeChanged?.Invoke(editInstrument, editEnvelope);
                App.UndoRedoManager.EndTransaction();
            }
        }

        private void EditDPCMSampleMappingProperties(Point pt, DPCMSampleMapping mapping)
        {
            var strings = DPCMSampleRate.GetStringList(true, FamiStudio.StaticInstance.PalPlayback, true, true);

            var dlg = new PropertyDialog(ParentWindow, SampleMappingTitle, new Point(left + pt.X, top + pt.Y), 400, false, pt.Y > Height / 2);
            dlg.Properties.AddDropDownList(PitchLabel.Colon, strings, strings[mapping.Pitch]); // 0
            dlg.Properties.AddCheckBox(LoopLabel.Colon, mapping.Loop); // 1
            dlg.Properties.AddCheckBox(OverrideDMCInitialValueLabel.Colon, mapping.OverrideDmcInitialValue); // 2
            dlg.Properties.AddNumericUpDown(DMCInitialValueDiv2Label.Colon, mapping.DmcInitialValueDiv2, 0, 63, 1); // 3
            dlg.Properties.Build();
            dlg.Properties.SetPropertyEnabled(3, mapping.OverrideDmcInitialValue);
            dlg.Properties.PropertyChanged += DPCMSampleMapping_PropertyChanged;

            dlg.ShowDialogAsync((r) =>
            {
                if (r == DialogResult.OK)
                {
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id);
                    mapping.Pitch = dlg.Properties.GetSelectedIndex(0);
                    mapping.Loop = dlg.Properties.GetPropertyValue<bool>(1);
                    mapping.OverrideDmcInitialValue = dlg.Properties.GetPropertyValue<bool>(2);
                    mapping.DmcInitialValueDiv2 = dlg.Properties.GetPropertyValue<int>(3);
                    App.UndoRedoManager.EndTransaction();
                    MarkDirty();
                }
            });
        }

        private void DPCMSampleMapping_PropertyChanged(PropertyPage props, int propIdx, int rowIdx, int colIdx, object value)
        {
            if (propIdx == 2)
            {
                props.SetPropertyEnabled(3, (bool)value);
            }
        }

        private void CaptureMouse(int x, int y, bool capturePointer = true)
        {
            SetMouseLastPos(x, y);
            captureMouseX = x;
            captureMouseY = y;
            captureScrollX = scrollX;
            captureScrollY = scrollY;

            if (capturePointer)
                CapturePointer();
        }

        internal void StartCaptureOperation(int x, int y, CaptureOperation op, bool allowSnap = false, int noteIdx = -1, int offsetX = 0, int offsetY = 0, bool capturePointer = true)
        {
#if DEBUG
            Debug.Assert(captureOperation == CaptureOperation.None);
#else
            if (captureOperation != CaptureOperation.None)
                AbortCaptureOperation();
#endif

            CaptureMouse(x, y, capturePointer);
            captureOperation = op;
            captureThresholdMet = captureThresholds[(int)op] == 0;
            captureRealTimeUpdate = captureWantsRealTimeUpdate[(int)op];
            captureWaveTime = editMode == EditionMode.DPCM ? GetWaveTimeForPixel(x - pianoSizeX) : 0.0f;
            captureNoteValue = NumNotes - Utils.Clamp((y + scrollY - headerAndEffectSizeY) / noteSizeY, 0, NumNotes);
            captureSelectionMinX = selectionMinX;
            captureSelectionMaxX = selectionMaxX;
            captureSelectionMinY = selectionMinY;
            captureSelectionMaxY = selectionMaxY;
            captureOffsetX = offsetX;
            captureOffsetY = offsetY;
            canFling = false;

            if (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio)
                GetEnvelopeValueForCoord(x, y, out _, out captureEnvelopeValue);

            captureMouseAbsoluteIdx = GetAbsoluteNoteIndexForPixelX(x - pianoSizeX);
            captureNoteAbsoluteIdx = noteIdx >= 0 ? noteIdx : captureMouseAbsoluteIdx;
            captureNoteLocation = Song.AbsoluteNoteIndexToNoteLocation(captureNoteAbsoluteIdx);

            if (noteIdx >= 0)
                highlightNoteAbsIndex = captureNoteAbsoluteIdx;
        }

        private void UpdateScrollBarX(int x, int y)
        {
            GetScrollBarParams(true, out _, out var scrollBarThumbSizeX, out var scrollAreaSizeX);
            GetMinMaxScroll(out _, out _, out var maxScrollX, out _);

            scrollX = (int)Math.Round(captureScrollX + ((x - captureMouseX) / (float)(scrollAreaSizeX - scrollBarThumbSizeX) * maxScrollX));

            ClampScroll();
            MarkDirty();
        }

        private void UpdateScrollBarY(int x, int y)
        {
            GetScrollBarParams(false, out _, out var scrollBarThumbSizeY, out var scrollAreaSizeY);
            GetMinMaxScroll(out _, out _, out _, out var maxScrollY);

            scrollY = (int)Math.Round(captureScrollY + ((y - captureMouseY) / (float)(scrollAreaSizeY - scrollBarThumbSizeY) * maxScrollY));

            ClampScroll();
            MarkDirty();
        }

        private void UpdateCaptureOperation(int x, int y, float scale = 1.0f, bool realTime = false)
        {  
            if (captureOperation != CaptureOperation.None && !captureThresholdMet)
            {
                var threshold = captureThresholds[(int)captureOperation];

                if (Math.Abs(x - captureMouseX) >= threshold ||
                    Math.Abs(y - captureMouseY) >= threshold)
                {
                    captureThresholdMet = true;
                }
            }

            if (captureOperation != CaptureOperation.None && captureThresholdMet && (captureRealTimeUpdate || !realTime))
            {
                x += captureOffsetX;
                y += captureOffsetY;

                switch (captureOperation)
                {
                    case CaptureOperation.DragLoop:
                    case CaptureOperation.DragRelease:
                    case CaptureOperation.ResizeEnvelope:
                        ResizeEnvelope(x, y, false);
                        break;
                    case CaptureOperation.ChangeEffectValue:
                    case CaptureOperation.ChangeSelectionEffectValue:
                        UpdateChangeEffectValue(x, y);
                        break;
                    case CaptureOperation.ChangeEnvelopeRepeatValue:
                        UpdateChangeEnvelopeRepeatValue(x, y);
                        break;
                    case CaptureOperation.DragSample:
                        UpdateDragDPCMSampleMapping(x, y);
                        break;
                    case CaptureOperation.DrawEnvelope:
                        DrawEnvelope(x, y);
                        break;
                    case CaptureOperation.Select:
                        UpdateSelection(x, y);
                        break;
                    case CaptureOperation.SelectWave:
                        UpdateWaveSelection(x, y);
                        break;
                    case CaptureOperation.CreateSlideNote:
                    case CaptureOperation.DragSlideNoteTarget:
                        UpdateSlideNoteCreation(x, y, false);
                        break;
                    case CaptureOperation.DragSlideNoteTargetGizmo:
                        UpdateSlideNoteCreation(x, y, false, true);
                        break;
                    case CaptureOperation.DragVolumeSlideTarget:
                    case CaptureOperation.DragVolumeSlideTargetGizmo:
                        UpdateDragVolumeSlide(x, y, false);
                        break;
                    case CaptureOperation.CreateNote:
                        noteArea.UpdateNoteCreation(x, y, false, false);
                        break;
                    case CaptureOperation.ResizeNoteEnd:
                    case CaptureOperation.ResizeSelectionNoteEnd:
                        noteArea.UpdateNoteResizeEnd(x, y, false);
                        break;
                    case CaptureOperation.MoveNoteRelease:
                    case CaptureOperation.MoveSelectionNoteRelease:
                        noteArea.UpdateMoveNoteRelease(x, y, false);
                        break;
                    case CaptureOperation.DragNote:
                    case CaptureOperation.DragSelection:
                    case CaptureOperation.ResizeNoteStart:
                    case CaptureOperation.ResizeSelectionNoteStart:
                        noteArea.UpdateNoteDrag(x, y, false);
                        break;
                    case CaptureOperation.AltZoom:
                        UpdateAltZoom(x, y);
                        break;
                    case CaptureOperation.DragSeekBar:
                        UpdateSeekDrag(x, y, false);
                        break;
                    case CaptureOperation.DragWaveVolumeEnvelope:
                        UpdateVolumeEnvelopeDrag(x, y, false);
                        break;
                    case CaptureOperation.ChangeEnvelopeValue:
                        UpdateChangeEnvelopeValue(x, y);
                        break;
                    case CaptureOperation.ScrollBarX:
                        UpdateScrollBarX(x, y);
                        break;
                    case CaptureOperation.ScrollBarY:
                        UpdateScrollBarY(x, y);
                        break;
                    case CaptureOperation.EditEffectSelection:
                        UpdateEditEffectSelection(x, y);
                        break;
                    case CaptureOperation.MobilePan:
                        DoScroll(x - mouseLastX, y - mouseLastY);
                        break;
                    case CaptureOperation.MobileZoomVertical:
                        ZoomVerticallyAtLocation(y, scale);
                        break;
                    case CaptureOperation.MobileZoom:
                        ZoomAtLocation(x, scale);
                        DoScroll(x - mouseLastX, y - mouseLastY);
                        break;
                    case CaptureOperation.DeleteNotes:
                        UpdateDeleteNotes(x, y);
                        break;
                }
            }
        }
        private void UpdateDragDPCMSampleMapping(int x, int y)
        {
            if (draggedSample == null)
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id);
                draggedSample = editInstrument.GetDPCMMapping(captureNoteValue);
                editInstrument.UnmapDPCMSample(captureNoteValue);
            }
            else
            {
                ScrollIfNearEdge(x, y, false, true);
                MarkDirty();
            }
        }

        private void EndDragDPCMSampleMapping(int x, int y)
        {
            if (draggedSample != null)
            {
                if (GetNoteValueForCoord(x, y, out var noteValue) && noteValue != captureNoteValue && draggedSample != null)
                {
                    var sample = draggedSample;

                    // Map the sample right away so that it renders correctly as the message box pops.
                    editInstrument.UnmapDPCMSample(noteValue);
                    editInstrument.MapDPCMSample(noteValue, sample.Sample, sample.Pitch, sample.Loop);

                    draggedSample = null;

                    Platform.MessageBoxAsync(ParentWindow, TransposeSampleMessage, TransposeSampleTitle, MessageBoxButtons.YesNo, (r) =>
                    {
                        if (r == DialogResult.Yes)
                        {
                            // Need to promote the transaction to project level since we are going to be transposing 
                            // potentially in multiple songs.
                            App.UndoRedoManager.RestoreTransaction(false);
                            App.UndoRedoManager.AbortTransaction();
                            App.UndoRedoManager.BeginTransaction(TransactionScope.Project);

                            // Need to redo everything + transpose.
                            editInstrument.UnmapDPCMSample(captureNoteValue);
                            editInstrument.UnmapDPCMSample(noteValue);
                            editInstrument.MapDPCMSample(noteValue, sample.Sample, sample.Pitch, sample.Loop);
                            App.Project.TransposeDPCMMapping(captureNoteValue, noteValue, editInstrument);
                        }

                        DPCMSampleMapped?.Invoke(noteValue);
                        ManyPatternChanged?.Invoke();

                        App.UndoRedoManager.EndTransaction();
                    });

                    if (Platform.IsMobile)
                        highlightDPCMSample = noteValue;
                }
                else
                {
                    App.UndoRedoManager.RestoreTransaction(false);
                    App.UndoRedoManager.AbortTransaction();

                    if (noteValue != captureNoteValue && draggedSample != null)
                        Platform.Beep();
                }
            }
        }

        private void EndCaptureOperation(int x, int y)
        {
            if (captureOperation != CaptureOperation.None)
            {
                x += captureOffsetX;
                y += captureOffsetY;

                switch (captureOperation)
                {
                    case CaptureOperation.ResizeEnvelope:
                        ResizeEnvelope(x, y, true);
                        break;
                    case CaptureOperation.DrawEnvelope:
                        DrawEnvelope(x, y, false, true);
                        break;
                    case CaptureOperation.CreateSlideNote:
                    case CaptureOperation.DragSlideNoteTarget:
                        UpdateSlideNoteCreation(x, y, true);
                        break;
                    case CaptureOperation.DragSlideNoteTargetGizmo:
                        UpdateSlideNoteCreation(x, y, true, true);
                        break;
                    case CaptureOperation.DragVolumeSlideTarget:
                    case CaptureOperation.DragVolumeSlideTargetGizmo:
                        UpdateDragVolumeSlide(x, y, true);
                        break;
                    case CaptureOperation.CreateNote:
                        noteArea.UpdateNoteCreation(x, y, false, true);
                        break;
                    case CaptureOperation.ResizeNoteEnd:
                    case CaptureOperation.ResizeSelectionNoteEnd:
                        noteArea.UpdateNoteResizeEnd(x, y, true);
                        break;
                    case CaptureOperation.MoveNoteRelease:
                    case CaptureOperation.MoveSelectionNoteRelease:
                        noteArea.UpdateMoveNoteRelease(x, y, true);
                        break;
                    case CaptureOperation.DragNote:
                    case CaptureOperation.DragSelection:
                    case CaptureOperation.ResizeNoteStart:
                    case CaptureOperation.ResizeSelectionNoteStart:
                        noteArea.UpdateNoteDrag(x, y, true);
                        break;
                    case CaptureOperation.DragSample:
                        EndDragDPCMSampleMapping(x, y);
                        break;
                    case CaptureOperation.DragSeekBar:
                        UpdateSeekDrag(x, y, true);
                        break;
                    case CaptureOperation.DragWaveVolumeEnvelope:
                        UpdateVolumeEnvelopeDrag(x, y, true);
                        break;
                    case CaptureOperation.MobilePan:
                    case CaptureOperation.MobileZoom:
                        canFling = true;
                        break;
                    case CaptureOperation.MobileZoomVertical:
                        break;
                    case CaptureOperation.DeleteNotes:
                        EndDeleteNotes();
                        break;
                    case CaptureOperation.ChangeEnvelopeValue:
                        UpdateChangeEnvelopeValue(x, y, true);
                        break;
                    case CaptureOperation.Select:
                        PostProcessSelection();
                        if (Platform.IsMobile && (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio) && IsSelectionValid())
                        {
                            if (captureSelectionFromEffectPanel && HasRepeatEnvelope())
                            {
                                highlightNoteAbsIndex = selectionMaxX / EditEnvelope.ChunkLength;
                                highlightRepeatEnvelope = true;
                            }
                            else
                            {
                                highlightNoteAbsIndex = selectionMaxX;
                                highlightRepeatEnvelope = false;
                            }
                        }
                        break;
                    case CaptureOperation.DragLoop:
                    case CaptureOperation.DragRelease:
                    case CaptureOperation.ChangeEffectValue:
                    case CaptureOperation.ChangeSelectionEffectValue:
                    case CaptureOperation.EditEffectSelection:
                    case CaptureOperation.ChangeEnvelopeRepeatValue:
                        App.UndoRedoManager.EndTransaction();
                        break;
                }

                draggedSample = null;
                captureOperation = CaptureOperation.None;
                panning = false;
                if (!Platform.IsMobile)
                    highlightNoteAbsIndex = -1;

                // Otherwise this control's own last capture cursor (e.g. Move, Eraser) leaks up the
                // parent chain and overrides NoteArea's (or another child control's) default cursor
                // once the mouse stops being routed here after the capture ends.
                Cursor = Cursors.Default;

                ReleasePointer();
                MarkDirty();
            }
        }

        internal void AbortCaptureOperation(bool restore = false)
        {
            if (captureOperation != CaptureOperation.None)
            {
                if (App.UndoRedoManager.HasTransactionInProgress)
                {
                    if (restore)
                        App.UndoRedoManager.RestoreTransaction(false);
                    App.UndoRedoManager.AbortTransaction();
                }

                MarkDirty();
                ReleasePointer();
                App.StopInstrument();

                captureOperation = CaptureOperation.None;
                panning = false;
                canFling = false;

                if (!Platform.IsMobile)
                    highlightNoteAbsIndex = -1;

                Cursor = Cursors.Default;

                ManyPatternChanged?.Invoke();
            }
        }

        public bool IsSelectionValid()
        {
            return selectionMinX >= 0 && selectionMaxX >= 0;
        }

        private bool HasSelectedNotes()
        {
            if (legacySelectMode)
                return IsSelectionValid();

            return selectedNoteIndices.Count > 0;
        }

        private bool HasSelectionContent()
        {
            if (!IsSelectionValid())
                return false;

            if (!legacySelectMode && editMode == EditionMode.Channel)
                return selectedNoteIndices.Count > 0 || selectedEffectIndices.Count > 0;

            return true;
        }

        private bool SelectionCoversMultiplePatterns()
        {
            return IsSelectionValid() && Song.PatternIndexFromAbsoluteNoteIndex(selectionMinX) != Song.PatternIndexFromAbsoluteNoteIndex(selectionMaxX);
        }

        private void ClearEffects(Channel channel, SortedList<int, int> effects)
        {
            foreach (var kv in effects)
            {
                var location = NoteLocation.FromAbsoluteNoteIndex(Song, kv.Key);
                channel.GetNoteAt(location)?.ClearEffectValue(selectedEffectIdx);
            }
        }

        private void PlaceEffects(Channel channel, SortedList<int, int> effects, int amount)
        {
            var songEnd = Song.GetPatternStartAbsoluteNoteIndex(Song.Length);

            foreach (var kv in effects)
            {
                var frame = kv.Key + amount;
                if (frame < 0 || frame >= songEnd)
                    continue;

                var location = NoteLocation.FromAbsoluteNoteIndex(Song, frame);
                var pattern  = channel.PatternInstances[location.PatternIndex];

                pattern ??= channel.CreatePatternAndInstance(location.PatternIndex);

                var note = pattern.GetOrCreateNoteAt(location.NoteIndex);
                note.SetEffectValue(selectedEffectIdx, kv.Value);
            }
        }

        private void ClearNotes(Channel channel, SortedList<int, Note> notes)
        {
            foreach (var kv in notes)
            {
                var location = NoteLocation.FromAbsoluteNoteIndex(Song, kv.Key);
                var pattern = channel.PatternInstances[location.PatternIndex];
                if (pattern == null)
                    continue;

                var note = channel.GetNoteAt(location);
                if (note == null)
                    continue;

                note.Clear(false);

                if (note.IsEmpty)
                {
                    pattern.DeleteNotesBetween(location.NoteIndex, location.NoteIndex + 1);
                }
            }
        }

        private void PlaceNotes(Channel channel, SortedList<int, Note> notes, int amount)
        {
            foreach (var kv in notes)
            {
                var frame    = kv.Key + amount;
                var location = NoteLocation.FromAbsoluteNoteIndex(Song, frame);
                var pattern  = channel.PatternInstances[location.PatternIndex];

                pattern ??= channel.CreatePatternAndInstance(location.PatternIndex);
                pattern.SetNoteAt(location.NoteIndex, kv.Value.Clone());
            }
        }

        private void UpdateSelectionAfterMove(int amount, int songEnd, int[] movedSelectedNotes, int[] movedSelectedEffects)
        {
            selectionMinX = Utils.Clamp(selectionMinX + amount, 0, songEnd - 1);
            selectionMaxX = Utils.Clamp(selectionMaxX + amount, 0, songEnd - 1);

            selectedNoteIndices.Clear();

            foreach (var absoluteIdx in movedSelectedNotes)
                selectedNoteIndices.Add(absoluteIdx - selectionMinX);

            selectedEffectIndices.Clear();

            foreach (var absoluteIdx in movedSelectedEffects)
                selectedEffectIndices.Add(absoluteIdx);
        }

        private void NotifyMovedPatterns(Channel channel, int oldMin, int oldMax, int newMin, int newMax)
        {
            var patternMin = Math.Min(oldMin, newMin);
            var patternMax = Math.Max(oldMax, newMax);

            channel.InvalidateCumulativePatternCache(patternMin, patternMax);

            for (int i = patternMin; i <= patternMax; i++)
            {
                var pattern = channel.PatternInstances[i];
                if (pattern != null)
                    PatternChanged?.Invoke(pattern);
            }
        }
        
        private void MoveNotes(int amount)
        {
            if (legacySelectMode)
            {
                if (!IsSelectionValid() || selectionMinX + amount < 0)
                    return;

                if (selectionMaxX + amount >= Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                    return;

                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);

                var notes = GetSelectedNotes();

                DeleteSelectedNotes(false);
                ReplaceNotes(notes, selectionMinX + amount, false);

                App.UndoRedoManager.EndTransaction();

                return;
            }

            // No need to process anything if no notes are selected.
            if (selectedNoteIndices.Count == 0)
                return;

            var selectedMin = selectionMinX + selectedNoteIndices.Min();
            var selectedMax = selectionMinX + selectedNoteIndices.Max();
            var songEnd = Song.GetPatternStartAbsoluteNoteIndex(Song.Length);

            amount = Utils.Clamp(amount, -selectedMin, songEnd - 1 - selectedMax);

            // Exit if we haven't moved anything.
            if (amount == 0)
                return;

            var movedSelectedNotes   = selectedNoteIndices.Select(idx => selectionMinX + idx + amount).ToArray();
            var movedSelectedEffects = selectedEffectIndices.Select(idx => idx + amount).Where(idx => idx >= 0 && idx < songEnd).ToArray();

            App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);

            var channel  = Song.Channels[editChannel];
            var newNotes = new SortedList<int, Note>();

            var effectSelectionMin = selectionMinX;
            var effectSelectionMax = selectionMaxX;

            TransformNotes(selectionMinX, selectionMaxX, false, false, false, (note, idx) =>
            {
                if (note != null && !note.IsEmpty && selectedNoteIndices.Contains(idx))
                {
                    var absoluteIdx = selectionMinX + idx;
                    var clone = note.Clone();

                    if (clone.IsMusical)
                    {
                        var location = NoteLocation.FromAbsoluteNoteIndex(Song, absoluteIdx);
                        var visualDuration = GetVisualNoteDuration(location, note);

                        clone.Duration = (ushort)Math.Max(1, visualDuration);

                        if (clone.HasRelease && clone.Release >= clone.Duration)
                            clone.Release = Math.Max(0, clone.Duration - 1);

                        effectSelectionMax = Math.Max(effectSelectionMax, absoluteIdx + visualDuration - 1);
                    }

                    newNotes[absoluteIdx] = clone;
                }

                return note;
            });

            var selectedEffects = new SortedList<int, int>();

            if (selectedEffectIdx >= 0)
            {
                var minLocation = NoteLocation.FromAbsoluteNoteIndex(Song, effectSelectionMin);
                var maxLocation = NoteLocation.FromAbsoluteNoteIndex(Song, effectSelectionMax);

                for (var it = channel.GetSparseNoteIterator(minLocation, maxLocation, Note.GetFilterForEffect(selectedEffectIdx)); !it.Done; it.Next())
                {
                    var absoluteIdx = it.Location.ToAbsoluteNoteIndex(Song);

                    if (selectedEffectIndices.Contains(absoluteIdx) && it.Note.HasValidEffectValue(selectedEffectIdx))
                    {
                        selectedEffects[absoluteIdx] = it.Note.GetEffectValue(selectedEffectIdx);
                    }
                }
            }

            var oldMin = Song.PatternIndexFromAbsoluteNoteIndex(selectedMin);
            var oldMax = Song.PatternIndexFromAbsoluteNoteIndex(selectedMax);
            var newMin = Song.PatternIndexFromAbsoluteNoteIndex(selectedMin + amount);
            var newMax = Song.PatternIndexFromAbsoluteNoteIndex(selectedMax + amount);

            // We have to clear all the effects and notes and place them again in order to move them.
            ClearEffects(channel, selectedEffects);
            ClearNotes(channel, newNotes);
            PlaceNotes(channel, newNotes, amount);
            PlaceEffects(channel, selectedEffects, amount);
            UpdateSelectionAfterMove(amount, songEnd, movedSelectedNotes, movedSelectedEffects);
            NotifyMovedPatterns(channel, oldMin, oldMax, newMin, newMax);

            App.UndoRedoManager.EndTransaction();

            MarkDirty();
        }

        private void ForEachNoteInRange(int minAbsoluteNoteIdx, int maxAbsoluteNoteIdx, Action<Note, int> function)
        {
            var channel     = Song.Channels[editChannel];
            var minLocation = Song.AbsoluteNoteIndexToNoteLocation(minAbsoluteNoteIdx);
            var maxLocation = Song.AbsoluteNoteIndexToNoteLocation(maxAbsoluteNoteIdx);

            for (var p = minLocation.PatternIndex; p <= maxLocation.PatternIndex; p++)
            {
                var pattern = channel.PatternInstances[p];

                if (pattern == null)
                    continue;

                var patternLen   = Song.GetPatternLength(p);
                var n0           = p == minLocation.PatternIndex ? minLocation.NoteIndex : 0;
                var n1           = p == maxLocation.PatternIndex ? maxLocation.NoteIndex : patternLen - 1;
                var patternStart = Song.GetPatternStartAbsoluteNoteIndex(p);

                for (var it = pattern.GetDenseNoteIterator(n0, n1 + 1); !it.Done; it.Next())
                {
                    var note = it.CurrentNote;
                    if (note != null)
                        function(note, patternStart + it.CurrentTime);
                }
            }
        }

        private void FindPreviousNotes(int beforeAbsoluteIdx, out int prevAnyIdx, out Note prevAnyNote, out int prevMusicalIdx, out Note prevMusicalNote)
        {
            prevAnyIdx = -1;
            prevAnyNote = null;
            prevMusicalIdx = -1;
            prevMusicalNote = null;

            if (beforeAbsoluteIdx <= 0)
                return;

            var channel  = Song.Channels[editChannel];
            var location = Song.AbsoluteNoteIndexToNoteLocation(beforeAbsoluteIdx - 1);

            for (var p = location.PatternIndex; p >= 0; p--)
            {
                var pattern = channel.PatternInstances[p];

                if (pattern == null)
                    continue;

                var n1           = p == location.PatternIndex ? location.NoteIndex + 1 : Song.GetPatternLength(p);
                var patternStart = Song.GetPatternStartAbsoluteNoteIndex(p);

                for (var it = pattern.GetDenseNoteIterator(0, n1, true); !it.Done; it.Next())
                {
                    var note = it.CurrentNote;

                    if (note == null)
                        continue;

                    if (prevAnyNote == null)
                    {
                        prevAnyIdx  = patternStart + it.CurrentTime;
                        prevAnyNote = note;
                    }

                    if (note.IsMusical)
                    {
                        prevMusicalIdx  = patternStart + it.CurrentTime;
                        prevMusicalNote = note;
                        return;
                    }
                }
            }
        }

        internal void TransformNotes(int minAbsoluteNoteIdx, int maxAbsoluteNoteIdx, bool doTransaction, bool doPatternChangeEvent, bool createMissingPatterns, Func<Note, int, Note> function)
        {
            if (doTransaction)
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);

            var channel     = Song.Channels[editChannel];
            var minLocation = Song.AbsoluteNoteIndexToNoteLocation(minAbsoluteNoteIdx);
            var maxLocation = Song.AbsoluteNoteIndexToNoteLocation(maxAbsoluteNoteIdx);

            for (var p = minLocation.PatternIndex; p <= maxLocation.PatternIndex; p++)
            {
                var pattern = channel.PatternInstances[p];

                if (pattern == null)
                {
                    if (createMissingPatterns && p < Song.Length)
                    {
                        pattern = channel.CreatePatternAndInstance(p);
                    }
                    else
                    {
                        continue;
                    }
                }

                var patternLen = Song.GetPatternLength(p);
                var n0 = p == minLocation.PatternIndex ? minLocation.NoteIndex : 0;
                var n1 = p == maxLocation.PatternIndex ? maxLocation.NoteIndex : patternLen - 1;
                var newNotes = new SortedList<int, Note>();

                for (var it = pattern.GetDenseNoteIterator(n0, n1 + 1); !it.Done; it.Next())
                {
                    var transformedNote = function(it.CurrentNote, Song.GetPatternStartAbsoluteNoteIndex(p) + it.CurrentTime - minAbsoluteNoteIdx);
                    if (transformedNote != null)
                        newNotes[it.CurrentTime] = transformedNote;
                }

                pattern.DeleteNotesBetween(n0, n1 + 1);

                foreach (var kv in newNotes)
                    pattern.SetNoteAt(kv.Key, kv.Value);

                if (doPatternChangeEvent)
                    PatternChanged?.Invoke(pattern);
            }

            channel.InvalidateCumulativePatternCache(minLocation.PatternIndex, maxLocation.PatternIndex);

            if (doTransaction)
                App.UndoRedoManager.EndTransaction();

            MarkDirty();
        }

        private void TransformEnvelopeValues(int startFrameIdx, int endFrameIdx, Func<sbyte, int, sbyte> function)
        {
            if (editMode == EditionMode.Arpeggio)
                App.UndoRedoManager.BeginTransaction(TransactionScope.Arpeggio, editArpeggio.Id);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id);

            Envelope.GetMinMaxValueForType(editInstrument, editEnvelope, out int minVal, out int maxVal);

            startFrameIdx = Math.Max(startFrameIdx, 0);
            endFrameIdx   = Math.Min(endFrameIdx, EditEnvelope.Length - 1);

            for (int i = startFrameIdx; i <= endFrameIdx; i++)
                EditEnvelope.Values[i] = (sbyte)Utils.Clamp(function(EditEnvelope.Values[i], i - startFrameIdx), minVal, maxVal);

            editInstrument?.NotifyEnvelopeChanged(editEnvelope, true);
            EnvelopeChanged?.Invoke(editInstrument, editEnvelope);
            App.UndoRedoManager.EndTransaction();
            MarkDirty();
        }

        private void TransposeNotes(int amount)
        {
            var processedNotes = new HashSet<Note>();

            TransformNotes(selectionMinX, selectionMaxX, true, true, false, (note, idx) =>
            {
                var selected = legacySelectMode || selectedNoteIndices.Contains(idx);

                if (note != null && note.IsMusical && selected && !processedNotes.Contains(note))
                {
                    var value = note.Value + amount;

                    if (value < Note.MusicalNoteMin || value > Note.MusicalNoteMax)
                    {
                        note.Clear();
                    }
                    else
                    {
                        note.Value = (byte)value;
                        if (note.IsSlideNote)
                            note.SlideNoteTarget = (byte)Utils.Clamp(note.SlideNoteTarget + amount, Note.MusicalNoteMin, Note.MusicalNoteMax);
                    }

                    processedNotes.Add(note);
                }

                return note;
            });

            if (!legacySelectMode)
            {
                selectionMinY = Utils.Clamp(selectionMinY + amount, Note.MusicalNoteMin, Note.MusicalNoteMax);
                selectionMaxY = Utils.Clamp(selectionMaxY + amount, Note.MusicalNoteMin, Note.MusicalNoteMax);
            }
        }

        private void IncrementEnvelopeValues(int amount)
        {
            TransformEnvelopeValues(selectionMinX, selectionMaxX, (val, idx) =>
            {
                return (sbyte)Utils.Clamp(val + amount, sbyte.MinValue, sbyte.MaxValue);
            });
        }

        private void MoveEnvelopeValues(int amount)
        {
            if (legacySelectMode)
            {
                if (selectionMinX + amount >= 0)
                    ReplaceEnvelopeValues(GetSelectedEnvelopeValues(), selectionMinX + amount);

                return;
            }

            if (selectedEnvelopeIndices.Count == 0)
                return;

            var min = selectedEnvelopeIndices.Min();
            var max = selectedEnvelopeIndices.Max();

            amount = Utils.Clamp(amount, -min, EditEnvelope.Length - 1 - max);

            if (amount == 0)
                return;

            // We need to store selected values, remove the old positions,
            // write to the new positions, then move the selection itself.
            var values = new SortedList<int, sbyte>();

            foreach (var idx in selectedEnvelopeIndices)
                values[idx] = EditEnvelope.Values[idx];

            foreach (var idx in selectedEnvelopeIndices)
                EditEnvelope.Values[idx] = 0;

            foreach (var kv in values)
                EditEnvelope.Values[kv.Key + amount] = kv.Value;

            selectedEnvelopeIndices.Clear();

            foreach (var idx in values.Keys)
                selectedEnvelopeIndices.Add(idx + amount);

            selectionMinX = selectedEnvelopeIndices.Min();
            selectionMaxX = selectedEnvelopeIndices.Max();

            MarkDirty();
        }

        private void DeleteSelectedNotes(bool doTransaction = true, bool deleteNotes = true, int deleteEffectsMask = Note.EffectAllMask)
        {
            TransformNotes(selectionMinX, selectionMaxX, doTransaction, true, false, (note, idx) =>
            {
                if (note != null)
                {
                    var absoluteIdx = selectionMinX + idx;
                    var noteSelected = legacySelectMode || selectedNoteIndices.Contains(idx);
                    var effectSelected = !legacySelectMode && selectedEffectIdx >= 0 && selectedEffectIndices.Contains(absoluteIdx);

                    if (noteSelected)
                    {
                        if (deleteNotes && deleteEffectsMask == Note.EffectAllMask)
                        {
                            note.Clear(false);
                        }
                        else
                        {
                            if (deleteNotes)
                                note.Clear(true);

                            for (int i = 0; i < Note.EffectCount; i++)
                            {
                                if ((deleteEffectsMask & (1 << i)) != 0)
                                    note.ClearEffectValue(i);
                            }
                        }
                    }
                    else if (effectSelected && (deleteEffectsMask & (1 << selectedEffectIdx)) != 0)
                    {
                        note.ClearEffectValue(selectedEffectIdx);
                    }
                }

                return note;
            });
        }

        private void DeleteSelectedEnvelopeValues()
        {
            TransformEnvelopeValues(selectionMinX, selectionMaxX, (val, idx) =>
            {
                return 0;
            });
        }

        private void FlattenEnvelopeValues(int refValueIdx)
        {
            var value = EditEnvelope.Values[refValueIdx];

            TransformEnvelopeValues(selectionMinX, selectionMaxX, (val, idx) =>
            {
                return value;
            });
        }

        private void DeleteSelectedWaveSection()
        {
            if (IsSelectionValid())
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.DPCMSample, editSample.Id);
                if (editSample.TrimSourceSourceData(selectionMinX, selectionMaxX))
                {
                    editSample.Process();
                    App.UndoRedoManager.EndTransaction();
                    DPCMSampleChanged?.Invoke();
                }
                else
                {
                    App.UndoRedoManager.AbortTransaction();
                }

                ClearSelection();
                ClampScroll();
                MarkDirty();
            }
        }

        private Note GetNoteForDesktopNoteGizmos(out NoteLocation location)
        {
            Debug.Assert(Platform.IsDesktop);
            location = NoteLocation.Invalid;

            if (captureOperation == CaptureOperation.DragSlideNoteTargetGizmo)
            {
                location = captureNoteLocation;
            }
            else
            {
                var pt = ScreenToControl(CursorPosition);
                if (!GetLocationForCoord(pt.X, pt.Y, out var mouseLocation, out byte noteValue) || !mouseLocation.IsInSong(Song))
                    return null;
                location = mouseLocation;
            }

            return Song.Channels[editChannel].FindMusicalNoteAtLocation(ref location, -1);
        }

        private Note GetNoteForDesktopEffectGizmos(out NoteLocation location)
        {
            Debug.Assert(Platform.IsDesktop);
            location = NoteLocation.Invalid;

            if (captureOperation == CaptureOperation.DragVolumeSlideTargetGizmo) 
            {
                location = captureNoteLocation;
            }
            else
            {
                var pt = ScreenToControl(CursorPosition);
                if (!GetEffectNoteForCoord(pt.X, pt.Y, out var mouseLocation) || !mouseLocation.IsInSong(Song))
                    return null;
                location = mouseLocation;
            }

            var channel = Song.Channels[editChannel];
            location = channel.GetLastEffectLocation(location, Note.EffectVolumeSlide);

            if (location.IsValid)
                return channel.GetNoteAt(location);

            return null;
        }

        private bool AllowGizmos()
        {
            return  window != null && !window.IsAsyncDialogInProgress && !window.IsOutOfProcessDialogInProgress;
        }

        internal List<Gizmo> GetNoteGizmos(out Note note, out NoteLocation location)
        {
            note = Platform.IsDesktop ? 
                GetNoteForDesktopNoteGizmos(out location) :
                GetHighlightedNoteAndLocation(out location);

            if (note == null || !note.IsMusical || !AllowGizmos())
                return null;

            var locationAbsIndex = location.ToAbsoluteNoteIndex(Song);
            var visualDuration = GetVisualNoteDuration(locationAbsIndex, note);
            var list = new List<Gizmo>();

            // Resize gizmo
            if (Platform.IsMobile)
            {
                var x = GetPixelXForAbsoluteNoteIndex(locationAbsIndex + visualDuration) + gizmoSize / 4;
                var y = virtualSizeY - note.Value * noteSizeY - scrollY - gizmoSize / 4;

                if ((captureOperation == CaptureOperation.ResizeNoteEnd ||
                     captureOperation == CaptureOperation.ResizeSelectionNoteEnd) && captureThresholdMet)
                {
                    x = mouseLastX - pianoSizeX - gizmoSize / 2;
                }

                Gizmo resizeGizmo = new Gizmo();
                resizeGizmo.Image = bmpGizmoResizeLeftRight;
                resizeGizmo.FillImage = bmpGizmoResizeFill;
                resizeGizmo.Action = GizmoAction.ResizeNote;
                resizeGizmo.Rect = new Rectangle(x, y, gizmoSize, gizmoSize);
                resizeGizmo.OffsetX = -gizmoSize * 3 / 4;
                list.Add(resizeGizmo);
            }

            // Release gizmo
            if (Platform.IsMobile && note.HasRelease && note.Release < visualDuration)
            {
                var x = GetPixelXForAbsoluteNoteIndex(locationAbsIndex + note.Release) - gizmoSize / 2;
                var y = virtualSizeY - note.Value * noteSizeY - scrollY + gizmoSize * 3 / 4;

                if ((captureOperation == CaptureOperation.MoveNoteRelease ||
                     captureOperation == CaptureOperation.MoveSelectionNoteRelease) && captureThresholdMet)
                {
                    x = mouseLastX - pianoSizeX - gizmoSize / 2;
                }

                Gizmo releaseGizmo = new Gizmo();
                releaseGizmo.Image = bmpGizmoResizeLeftRight;
                releaseGizmo.FillImage = bmpGizmoResizeFill;
                releaseGizmo.Action = GizmoAction.MoveRelease;
                releaseGizmo.Rect = new Rectangle(x, y, gizmoSize, gizmoSize);
                list.Add(releaseGizmo);
            }

            // Slide note gizmo
            if (note.IsSlideNote)
            {
                var side = note.SlideNoteTarget > note.Value ? 1 : -1;
                var x = GetPixelXForAbsoluteNoteIndex(locationAbsIndex + visualDuration) + (Platform.IsMobile ? gizmoSize / 4 : -5 * gizmoSize / 4);
                var y = 0;

                if (Platform.IsMobile)
                {
                    if (Platform.IsMobile && captureOperation == CaptureOperation.DragSlideNoteTargetGizmo && captureThresholdMet)
                        y = mouseLastY - headerAndEffectSizeY - gizmoSize / 2;
                    else
                        y = virtualSizeY  - Utils.Clamp((note.SlideNoteTarget + side) * noteSizeY + gizmoSize / 4, gizmoSize, virtualSizeY) - scrollY;
                }
                else
                {
                    y = virtualSizeY - note.SlideNoteTarget * noteSizeY - scrollY - (gizmoSize - noteSizeY) / 2;
                }

                Gizmo slideGizmo = new Gizmo();
                slideGizmo.Image = bmpGizmoResizeUpDown;
                slideGizmo.FillImage = bmpGizmoResizeFill;
                slideGizmo.Action = GizmoAction.MoveSlide;
                slideGizmo.Rect = new Rectangle(x, y, gizmoSize, gizmoSize);
                slideGizmo.GizmoText = Note.GetFriendlyName(note.SlideNoteTarget);
                list.Add(slideGizmo);
            }

            return list;
        }

        internal List<Gizmo> GetEffectGizmos(out Note note, out NoteLocation location)
        {
            note = Platform.IsDesktop ?
                GetNoteForDesktopEffectGizmos(out location) :
                GetHighlightedNoteAndLocation(out location);

            if (!showEffectsPanel || selectedEffectIdx < 0 || note == null || !note.HasValidEffectValue(selectedEffectIdx) || !AllowGizmos())
                return null;

            var list = new List<Gizmo>();

            var locationAbsIndex = location.ToAbsoluteNoteIndex(Song);
            var channel  = Song.Channels[editChannel];
            var minValue = Note.GetEffectMinValue(Song, channel, selectedEffectIdx);
            var maxValue = Note.GetEffectMaxValue(Song, channel, selectedEffectIdx);
            var exp = GetEffectValueExponent(maxValue);
            var midValue = (int)MathF.Round(Utils.Lerp(minValue, maxValue, MathF.Pow(0.5f, 1.0f / exp)));
            var value    = note.GetEffectValue(selectedEffectIdx);

            // Effect values
            if (Platform.IsMobile)
            {
                var effectPosY = effectPanelSizeY - GetPixelYForEffectValue(value, minValue, maxValue, exp);
                var x = GetPixelXForAbsoluteNoteIndex(locationAbsIndex + 1) + gizmoSize / 4;
                var y = (int)(effectPosY + (value >= midValue ? gizmoSize / 4 : -gizmoSize * 5 / 4));

                Gizmo effectGizmo = new Gizmo();
                effectGizmo.Image = bmpGizmoResizeUpDown;
                effectGizmo.FillImage = bmpGizmoResizeFill;
                effectGizmo.Action = GizmoAction.ChangeEffectValue;
                effectGizmo.Rect = new Rectangle(x, y, gizmoSize, gizmoSize);
                list.Add(effectGizmo);
            }

            // Volume slide.
            if (selectedEffectIdx == Note.EffectVolume && channel.SupportsEffect(Note.EffectVolumeSlide) && note.HasVolumeSlide)
            {
                var duration = channel.GetVolumeSlideDuration(location);
                var effectPosY = effectPanelSizeY - GetPixelYForEffectValue(note.VolumeSlideTarget, minValue, maxValue, exp);

                var x = GetPixelXForAbsoluteNoteIndex(locationAbsIndex + duration) - gizmoSize * 5 / 4;
                var y = (int)(effectPosY + (note.VolumeSlideTarget >= midValue ? gizmoSize / 4 : -gizmoSize * 5 / 4));

                Gizmo slideGizmo = new Gizmo();
                slideGizmo.Image = bmpGizmoResizeUpDown;
                slideGizmo.FillImage = bmpGizmoResizeFill;
                slideGizmo.Action = GizmoAction.MoveVolumeSlideValue;
                slideGizmo.Rect = new Rectangle(x, y, gizmoSize, gizmoSize);
                list.Add(slideGizmo);
            }

            return list;
        }

        internal List<Gizmo> GetEnvelopeGizmos()
        {
            if (Platform.IsDesktop)
                return null;

            var env = EditEnvelope;

            if (highlightNoteAbsIndex < 0 || highlightNoteAbsIndex >= env.Length || highlightRepeatEnvelope || !AllowGizmos())
                return null;

            Envelope.GetMinMaxValueForType(editInstrument, editEnvelope, out int min, out int max);
            var midValue = (max + min) / 2;
            var value = env.Values[highlightNoteAbsIndex];

            var x = GetPixelXForAbsoluteNoteIndex(highlightNoteAbsIndex + 1) + gizmoSize / 4;
            var y = 0;
            
            if (editEnvelope == EnvelopeType.Arpeggio)
                y = (int)(virtualSizeY - envelopeValueSizeY * (value - min)) - scrollY - gizmoSize * 3 / 4;
            else
                y = (int)(virtualSizeY - envelopeValueSizeY * (value - min + (value < 0 ? 0 : 1))) + (value >= midValue ? gizmoSize / 4 : -gizmoSize * 5 / 4) - scrollY;

            Gizmo slideGizmo = new Gizmo();
            slideGizmo.Image = bmpGizmoResizeUpDown;
            slideGizmo.FillImage = bmpGizmoResizeFill;
            slideGizmo.Action = GizmoAction.ChangeEnvValue;
            slideGizmo.Rect = new Rectangle(x, y, gizmoSize, gizmoSize);

            var list = new List<Gizmo>();
            list.Add(slideGizmo);
            return list;
        }

        internal List<Gizmo> GetEnvelopeEffectsGizmos()
        {
            if (Platform.IsDesktop)
                return null;

            var env = EditEnvelope;
            var rep = EditRepeatEnvelope;

            if (highlightNoteAbsIndex < 0 || highlightNoteAbsIndex >= env.Length || !highlightRepeatEnvelope || rep == null || !AllowGizmos())
                return null;

            Envelope.GetMinMaxValueForType(editInstrument, EnvelopeType.WaveformRepeat, out int min, out int max);
            var val = rep.Values[highlightNoteAbsIndex];
            var mid = (min + max) / 2;
            var effectPosY = GetPixelYForEffectValue(val, min, max);

            var x = GetPixelXForAbsoluteNoteIndex(highlightNoteAbsIndex * env.ChunkLength + env.ChunkLength / 2) - gizmoSize / 2;
            var y = effectPanelSizeY - effectPosY + (val >= mid ? gizmoSize / 4 : -gizmoSize * 5 / 4);

            Gizmo repeatGizmo = new Gizmo();
            repeatGizmo.Image = bmpGizmoResizeUpDown;
            repeatGizmo.FillImage = bmpGizmoResizeFill;
            repeatGizmo.Action = GizmoAction.ChangeEnvEffectValue;
            repeatGizmo.Rect = new Rectangle(x, y, gizmoSize, gizmoSize);

            var list = new List<Gizmo>();
            list.Add(repeatGizmo);
            return list;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            UpdateCursor();

            if (captureOperation != CaptureOperation.None)
                return;

            if (e.Key == Keys.Escape)
            {
                ClearSelection();
                MarkDirty();
            }
            else if (IsActiveControl && Settings.SelectAllShortcut.Matches(e))
            {
                SelectAll();
            }
            else if (Settings.EffectPanelShortcut.Matches(e))
            {
                ToggleEffectPanel();
            }
            else if (Settings.MaximizePianoRollShortcut.Matches(e))
            {
                ToggleMaximize();
            }
            else if (e.Key >= Keys.D1 && e.Key <= Keys.D9 && e.Alt)
            {
                for (int i = SnapResolutionType.Min; i <= SnapResolutionType.Max; i++)
                {
                    if (SnapResolutionType.KeyboardShortcuts[i] == e.Key)
                    {
                        SetAndMarkDirty(ref snapResolution, i);
                        break;
                    }
                }
            }
            else if (Settings.SnapToggleShortcut.Matches(e))
            {
                if (SnapAllowed)
                {
                    snap = !snap;
                    MarkDirty();
                }
            }
            else if (IsActiveControl && Settings.PasteShortcut.Matches(e) && CanPaste)
            {
                Paste();
            }
            else if (IsActiveControl && Settings.PasteSpecialShortcut.Matches(e) && CanPaste)
            {
                PasteSpecial();
            }
            else if (IsActiveControl && IsSelectionValid())
            {
                if (Settings.CopyShortcut.Matches(e))
                {
                    Copy();
                }
                else if (Settings.CutShortcut.Matches(e))
                {
                    Cut();
                }
                else if (Settings.DeleteShortcut.Matches(e))
                {
                    Delete();
                }
                else if (Settings.DeleteSpecialShortcut.Matches(e))
                {
                    DeleteSpecial();
                }

                if (editMode == EditionMode.Channel)
                {
                    switch (e.Key)
                    {
                        case Keys.Up:
                            TransposeNotes(e.Control ? 12 : 1);
                            break;
                        case Keys.Down:
                            TransposeNotes(e.Control ? -12 : -1);
                            break;
                        case Keys.Right:
                            MoveNotes(e.Control ? (Song.Project.UsesFamiTrackerTempo ? Song.BeatLength : Song.NoteLength) : 1);
                            break;
                        case Keys.Left:
                            MoveNotes(e.Control ? -(Song.Project.UsesFamiTrackerTempo ? Song.BeatLength : Song.NoteLength) : -1);
                            break;
                    }
                }
                else if (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio)
                {
                    switch (e.Key)
                    {
                        case Keys.Up:
                            IncrementEnvelopeValues(e.Control ? 4 : 1);
                            break;
                        case Keys.Down:
                            IncrementEnvelopeValues(e.Control ? -4 : -1);
                            break;
                        case Keys.Right:
                            MoveEnvelopeValues(e.Control ? 4 : 1);
                            break;
                        case Keys.Left:
                            MoveEnvelopeValues(e.Control ? -4 : -1);
                            break;
                    }
                }
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            UpdateCursor();
        }

        public int GetDPCMSampleMappingNoteAtPos(Point pos, out Instrument instrument)
        {
            if (editMode == EditionMode.DPCMMapping && GetNoteValueForCoord(pos.X, pos.Y, out var noteValue))
            {
                instrument = editInstrument;
                return noteValue;
            }

            instrument = null;
            return Note.NoteInvalid;
        }

        private bool EnsureSeekBarVisible(float percent = float.MinValue)
        {
            if (percent == float.MinValue)
                percent = Settings.FollowPercent;

            var seekX = GetPixelXForAbsoluteNoteIndex(GetSeekFrameToDraw());
            var minX = 0;
            var maxX = (int)((Width - pianoSizeX) * percent);

            // Keep everything visible 
            if (seekX < minX)
                scrollX -= (minX - seekX);
            else if (seekX > maxX)
                scrollX += (seekX - maxX);

            ClampScroll();

            seekX = GetPixelXForAbsoluteNoteIndex(GetSeekFrameToDraw());
            return seekX == maxX;
        }

        public void DeleteRecording(int frame)
        {
            if (App.IsRecording && editMode == EditionMode.Channel)
            {
                var endFrame = frame;
                var startFrame = SnapNote(frame, false, true);

                if (startFrame == 0)
                    return;

                if (startFrame == endFrame)
                    startFrame = SnapNote(startFrame - 1, false, true);

                var startPatternIdx = Song.PatternIndexFromAbsoluteNoteIndex(startFrame);
                var endPatternIdx   = Song.PatternIndexFromAbsoluteNoteIndex(endFrame);

                var channel = Song.Channels[editChannel];

                if (startPatternIdx == endPatternIdx)
                {
                    if (channel.PatternInstances[startPatternIdx] != null)
                        App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, channel.PatternInstances[startPatternIdx].Id);
                    else
                        App.UndoRedoManager.BeginTransaction(TransactionScope.Application);
                }
                else
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);

                channel.DeleteNotesBetween(startFrame, endFrame);
                App.SeekSong(startFrame);
                EnsureSeekBarVisible();
                App.UndoRedoManager.EndTransaction();

                for (int i = startPatternIdx; i <= endPatternIdx; i++)
                    PatternChanged?.Invoke(channel.PatternInstances[startPatternIdx]);

                MarkDirty();
            }
        }

        public void AdvanceRecording(int frame, bool doTransaction = false)
        {
            if (App.IsRecording && editMode == EditionMode.Channel)
            {
                if (doTransaction)
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Application);

                App.SeekSong(SnapNote(frame, true, true));
                EnsureSeekBarVisible();

                if (doTransaction)
                    App.UndoRedoManager.EndTransaction();

                MarkDirty();
            }
        }

        public void RecordNote(Note note)
        {
            if (App.IsRecording && editMode == EditionMode.Channel && note.IsMusical)
            {
                var currentFrame = SnapNote(App.CurrentFrame, false, true);
                var location = NoteLocation.FromAbsoluteNoteIndex(Song, currentFrame);
                var channel = Song.Channels[editChannel];
                var pattern = channel.PatternInstances[location.PatternIndex];

                // Create a pattern if needed.
                if (pattern == null)
                {
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
                    pattern = channel.CreatePattern();
                    channel.PatternInstances[location.PatternIndex] = pattern;
                }
                else
                {
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
                }

                var nextFrame = SnapNote(currentFrame, true, true);

                var newNote = note.Clone();
                newNote.HasVolume = false;
                newNote.Duration = nextFrame - currentFrame;
                pattern.Notes[location.NoteIndex] = newNote;
                channel.InvalidateCumulativePatternCache(pattern);
                PatternChanged?.Invoke(pattern);

                AdvanceRecording(currentFrame);

                App.UndoRedoManager.EndTransaction();

                MarkDirty();
            }
        }

        public void ToggleEffectPanel()
        {
            if (editMode == EditionMode.Channel || editMode == EditionMode.DPCM && !Platform.IsMobile || editMode == EditionMode.Envelope && HasRepeatEnvelope())
                SetShowEffectPanel(!showEffectsPanel);
        }

        public void SetShowEffectPanel(bool expanded)
        {
            showEffectsPanel = expanded;
            UpdateRenderCoords();
            ClampScroll();
            MarkDirty();
        }

        public void ToggleMaximize()
        {
            maximized = !maximized;
            MaximizedChanged?.Invoke();
            UpdateRenderCoords();
            ClampScroll();
            MarkDirty();
        }

        private bool HandleMouseDownPan(PointerEventArgs e)
        {
            var middle = e.Middle || (e.Left && ModifierKeys.IsAltDown && Settings.AltLeftForMiddle);
            if (middle && e.Y > headerSizeY && e.X > pianoSizeX)
            {
                StartPan(e.X, e.Y);
                return true;
            }

            return false;
        }

        private bool HandleMouseDownScrollbar(PointerEventArgs e)
        {
            if (e.Left && scrollBarThickness > 0 && e.X > pianoSizeX && e.Y > headerAndEffectSizeY)
            {
                if (e.Y >= (Height - scrollBarThickness) && GetScrollBarParams(true, out var scrollBarThumbPosX, out var scrollBarThumbSizeX, out _))
                {
                    var x = e.X - pianoSizeX;
                    if (x < scrollBarThumbPosX)
                    {
                        scrollX -= (Width - pianoSizeX);
                        ClampScroll();
                        MarkDirty();
                    }
                    else if (x > (scrollBarThumbPosX + scrollBarThumbSizeX))
                    {
                        scrollX += (Width - pianoSizeX);
                        ClampScroll();
                        MarkDirty();
                    }
                    else if (x >= scrollBarThumbPosX && x <= (scrollBarThumbPosX + scrollBarThumbSizeX))
                    {
                        StartCaptureOperation(e.X, e.Y, CaptureOperation.ScrollBarX);
                    }
                    return true;
                }
                if (e.X >= (Width - scrollBarThickness) && GetScrollBarParams(false, out var scrollBarThumbPosY, out var scrollBarThumbSizeY, out _))
                {
                    var y = e.Y - headerAndEffectSizeY;
                    if (y < scrollBarThumbPosY)
                    {
                        scrollY -= (Height - headerAndEffectSizeY);
                        ClampScroll();
                        MarkDirty();
                    }
                    else if (y > (scrollBarThumbPosY + scrollBarThumbSizeY))
                    {
                        scrollX += (Height - headerAndEffectSizeY);
                        ClampScroll();
                        MarkDirty();
                    }
                    else if (y >= scrollBarThumbPosY && y <= (scrollBarThumbPosY + scrollBarThumbSizeY))
                    {
                        StartCaptureOperation(e.X, e.Y, CaptureOperation.ScrollBarY);
                    }
                    return true;
                }
            }

            return false;
        }

        private bool HandleMouseDownEnvelopeEffectPanel(PointerEventArgs e)
        {
            if (e.Left && HasRepeatEnvelope() && IsPointInEffectPanel(e.X, e.Y))
            {
                StartChangeEnvelopeRepeatValue(e.X, e.Y);
                return true;
            }

            return false;
        }

        private bool HandleMouseDownEnvelopeSelection(PointerEventArgs e)
        {
            if (e.Right && (IsPointInHeaderTopPart(e.X, e.Y) || IsPointInEffectPanel(e.X, e.Y) || IsPointInNoteArea(e.X, e.Y)))
            {
                e.DelayRightClick(); // Need to wait and see if its a context menu click or not.
                return true;
            }

            return false;
        }

        public int GetEffectIndexForPosition(int x, int y, int maxEffects)
        {
            if (IsPointInEffectList(x, y))
            {
                int effectIdx = (y - headerSizeY) / effectButtonSizeY;
                if (effectIdx >= 0 && effectIdx < maxEffects)
                {
                    return effectIdx;
                }
            }

            return -1;
        }


        private bool HandleMouseDownAltZoom(PointerEventArgs e)
        {
            if (e.Right && ModifierKeys.IsAltDown && Settings.AltZoomAllowed)
            {
                StartCaptureOperation(e.X, e.Y, CaptureOperation.AltZoom);
                return true;
            }

            return false;
        }

        private void StartResizeEnvelope(int x, int y)
        {
            StartCaptureOperation(x, y, CaptureOperation.ResizeEnvelope);

            if (editMode == EditionMode.Envelope)
                App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Arpeggio, editArpeggio.Id);

            ResizeEnvelope(x, y, false);
        }

        private void StartDrawEnvelope(int x, int y)
        {
            StartCaptureOperation(x, y, CaptureOperation.DrawEnvelope);

            if (editMode == EditionMode.Envelope)
                App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Arpeggio, editArpeggio.Id);

            DrawEnvelope(x, y, true);
        }

        private bool HandleMouseDownDrawEnvelope(PointerEventArgs e)
        {
            if (e.Left && IsPointInNoteArea(e.X, e.Y) && EditEnvelope.Length > 0)
            {
                var noteIdx = GetAbsoluteNoteIndexForPixelX(e.X - pianoSizeX);

                if (IsNoteSelected(noteIdx))
                    StartChangeEnvelopeValue(e.X, e.Y);
                else
                    StartDrawEnvelope(e.X, e.Y);

                return true;
            }

            return false;
        }


        private void StartDragWaveVolumeEnvelope(int x, int y, int vertexIdx)
        {
            volumeEnvelopeDragVertex = vertexIdx;
            App.UndoRedoManager.BeginTransaction(TransactionScope.DPCMSample, editSample.Id);
            StartCaptureOperation(x, y, CaptureOperation.DragWaveVolumeEnvelope);
        }

        public bool HandleMouseDownDPCMVolumeEnvelope(PointerEventArgs e)
        {
            if (e.Left && IsPointInEffectPanel(e.X, e.Y))
            {
                var vertexIdx = GetWaveVolumeEnvelopeVertexIndex(e.X, e.Y);
                if (vertexIdx >= 0)
                {
                    StartDragWaveVolumeEnvelope(e.X, e.Y, vertexIdx);
                    return true;
                }
            }

            return false;
        }

        public void StartSelectWave(int x, int y, bool capturePointer = true)
        {
            StartCaptureOperation(x, y, CaptureOperation.SelectWave, capturePointer: capturePointer);

            if (captureThresholdMet)
                UpdateWaveSelection(x, y);
        }

        private bool HandleMouseDownEffectGizmos(PointerEventArgs e)
        {
            return e.Left && HandleEffectsGizmos(e.X, e.Y);
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                OnTouchDown(e);
                return;
            }

            bool left  = e.Left;
            bool right = e.Right;

            if (captureOperation != CaptureOperation.None && (left || right))
                return;

            UpdateCursor();

            // General stuff.
            if (HandleMouseDownPan(e)) goto Handled;
            if (HandleMouseDownScrollbar(e)) goto Handled;
            if (HandleMouseDownAltZoom(e)) goto Handled;

            if (editMode == EditionMode.Channel)
            {
                if (HandleMouseDownEffectGizmos(e)) goto Handled;
            }

            if (editMode == EditionMode.Envelope || 
                editMode == EditionMode.Arpeggio)
            {
                if (HandleMouseDownEnvelopeEffectPanel(e)) goto Handled;
                if (HandleMouseDownEnvelopeSelection(e)) goto Handled;
                if (HandleMouseDownDrawEnvelope(e)) goto Handled;
            }

            return;

        Handled:
            MarkDirty();
        }

        private bool HandleMouseDownDelayedChannelNotes(PointerEventArgs e)
        {
            bool right = e.Right;

            if (right && GetLocationForCoord(e.X, e.Y, out var mouseLocation, out byte noteValue) && mouseLocation.PatternIndex < Song.Length)
            {
                var channel = Song.Channels[editChannel];
                var noteLocation = mouseLocation;
                var note = channel.FindMusicalNoteAtLocation(ref noteLocation, noteValue);

                if (note == null)
                    StartSelection(e.X, e.Y);

                return true;
            }

            return false;
        }

        private bool HandleMouseDownDelayedHeaderSelection(PointerEventArgs e)
        {
            if (e.Right && IsPointInHeader(e.X, e.Y))
            {
                StartSelection(e.X, e.Y);
                return true;
            }

            return false;
        }


        private bool HandleMouseDownDelayedWaveSelection(PointerEventArgs e)
        {
            if (e.Right && (IsPointInNoteArea(e.X, e.Y) || IsPointInHeader(e.X, e.Y)))
            {
                StartSelectWave(e.X, e.Y);
                return true;
            }

            return false;
        }

        protected override void OnPointerDownDelayed(PointerEventArgs e)
        {
            if (editMode == EditionMode.Channel)
            {
                if (HandleMouseDownDelayedChannelNotes(e)) goto Handled;
                //if (HandleMouseDownDelayedHeaderSelection(e)) goto Handled;
            }

            if (editMode == EditionMode.DPCM)
            {
                if (HandleMouseDownDelayedWaveSelection(e)) goto Handled;
            }

            return;

        Handled:
            MarkDirty();
        }

        internal Note CreateSingleNote(int x, int y)
        {
            GetLocationForCoord(x, y, out var location, out var noteValue, true);

            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[location.PatternIndex];

            if (!channel.SupportsInstrument(App.SelectedInstrument, false))
            {
                App.ShowInstrumentError(channel, true);
                return null;
            }

            if (pattern != null)
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
            }
            else
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
                pattern = channel.CreatePatternAndInstance(location.PatternIndex);
            }

            App.PlayInstrumentNote(noteValue, false, false, false, null, null, 0.5f);

            var abs = location.ToAbsoluteNoteIndex(Song);
            var note = pattern.GetOrCreateNoteAt(location.NoteIndex);
            note.Value = noteValue;
            note.Duration = SnapEnabled ? Math.Max(1, SnapNote(abs, true) - abs) : Song.GetPatternBeatLength(location.PatternIndex);
            note.Instrument = App.SelectedInstrument;
            note.Arpeggio = channel.SupportsArpeggios ? App.SelectedArpeggio : null;
            note.ClearInvalidSlide();

            SetMobileHighlightedNote(abs);
            MarkPatternDirty(pattern);
            App.UndoRedoManager.EndTransaction();

            return note;
        }

        private bool HandleTouchDownPan(int x, int y)
        {
            if (IsPointInNoteArea(x, y) || IsPointInEffectPanel(x, y))
            {
                StartCaptureOperation(x, y, CaptureOperation.MobilePan);
                return true;
            }

            return false;
        }

        private bool HandleTouchDownEnvelopeSelection(int x, int y)
        {
            if (IsPointInHeader(x, y) && x < GetPixelXForAbsoluteNoteIndex(EditEnvelope.Length))
            {
                StartSelection(x, y);
                return true;
            }

            return false;
        }

        private bool HandleTouchDownEnvelopeResize(int x, int y)
        {
            if (IsPointInHeader(x, y) && EditEnvelope.CanResize && x > GetPixelXForAbsoluteNoteIndex(EditEnvelope.Length))
            {
                StartResizeEnvelope(x, y);
                return true;
            }

            return false;
        }

        private bool HandleEffectsGizmos(int x, int y)
        {
            if (IsPointInNoteArea(x, y) || IsPointInEffectPanel(x, y))
            {
                var gizmos = GetEffectGizmos(out var gizmoNote, out var gizmoNoteLocation);
                if (gizmos != null)
                {
                    foreach (var g in gizmos)
                    {
                        if (g.Rect.Contains(x - pianoSizeX, y - headerSizeY))
                        {
                            switch (g.Action)
                            {
                                case GizmoAction.ChangeEffectValue:
                                    StartChangeEffectValue(x, y, gizmoNoteLocation);
                                    break;
                                case GizmoAction.MoveVolumeSlideValue:
                                    StartDragVolumeSlideGizmo(x, y, gizmoNote, gizmoNoteLocation);
                                    break;
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool HandleTouchDownNoteEffectsGizmos(int x, int y)
        {
            return HandleEffectsGizmos(x, y);
        }

        private bool HandleTouchDownDragNote(int x, int y)
        {
            if (HasHighlightedNote() && IsPointInNoteArea(x, y))
            {
                var mouseNote = GetNoteForCoord(x, y, out _, out _, out var duration);
                var highlightNote = GetHighlightedNote();

                if (highlightNote != null && mouseNote == highlightNote)
                {
                    noteArea.StartNoteDrag(x, y, IsHighlightedNoteSelected() ? CaptureOperation.DragSelection : CaptureOperation.DragNote, NoteLocation.FromAbsoluteNoteIndex(Song, highlightNoteAbsIndex), highlightNote);
                    return true;
                }
            }

            return false;
        }

        private void Timeline_SeekDragRequested(Control sender, PointerEventArgs e)
        {
            var p = WindowToControl(timeline.ControlToWindow(e.Position));
            // Timeline already captured itself before raising this, so don't capture again.
            StartCaptureOperation(p.X, p.Y, CaptureOperation.DragSeekBar, capturePointer: false);
            UpdateSeekDrag(p.X, p.Y, false);
        }

        private void Timeline_SelectionRequested(Control sender, PointerEventArgs e)
        {
            var p = WindowToControl(timeline.ControlToWindow(e.Position));
            StartTimelineSelection(p.X, p.Y);
        }

        private void UpdateChangeEnvelopeValue(int x, int y, bool final = false)
        {
            App.UndoRedoManager.RestoreTransaction(false);

            GetEnvelopeValueForCoord(x, y, out _, out var value);
            Envelope.GetMinMaxValueForType(editInstrument, editEnvelope, out int min, out int max);

            var env = EditEnvelope;
            var idx = Platform.IsMobile ? highlightNoteAbsIndex : captureNoteAbsoluteIdx;
            var originalValue = env.Values[idx];
            var delta = 0;

            if (Platform.IsDesktop)
            {
                delta = value - originalValue;
            }
            else // On mobile we drag using gizmos
            {
                delta = value - captureEnvelopeValue;
            }

            if (IsEnvelopeValueSelected(idx))
            {
                var scaling = originalValue == 0 ? 0.0f : Utils.Clamp(originalValue + delta, min, max) / MathF.Abs(originalValue);

                for (int i = selectionMinX; i <= selectionMaxX; i++)
                {
                    if (!legacySelectMode && !IsEnvelopeValueSelected(i))
                        continue;

                    if (relativeEffectScaling)
                        env.Values[i] = (sbyte)Utils.Clamp((int)Math.Round(env.Values[i] * scaling), min, max);
                    else
                        env.Values[i] = (sbyte)Utils.Clamp(env.Values[i] + delta, min, max);
                }
            }
            else
            {
                env.Values[idx] = (sbyte)Utils.Clamp(env.Values[idx] + delta, min, max);
            }

            if (final)
            {
                // Arps will have null instruments.
                if (editInstrument != null)
                {
                    editInstrument.NotifyEnvelopeChanged(editEnvelope, true);
                }

                EnvelopeChanged?.Invoke(editInstrument, editEnvelope);
                App.UndoRedoManager.EndTransaction();
            }

            MarkDirty();
        }

        private void StartChangeEnvelopeValue(int x, int y)
        {
            StartCaptureOperation(x, y, CaptureOperation.ChangeEnvelopeValue);

            if (editMode == EditionMode.Envelope)
                App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Arpeggio, editArpeggio.Id);

            UpdateChangeEnvelopeValue(x, y);
        }

        public bool HandleTouchDownEnvelopeEffectsGizmos(int x, int y)
        {
            if (HasHighlightedNote() && highlightRepeatEnvelope && IsPointInEffectPanel(x, y))
            {
                var gizmos = GetEnvelopeEffectsGizmos();
                if (gizmos != null)
                {
                    foreach (var g in gizmos)
                    {
                        if (g.Rect.Contains(x - pianoSizeX, y - headerSizeY))
                        {
                            switch (g.Action)
                            {
                                case GizmoAction.ChangeEnvEffectValue:
                                    StartChangeEnvelopeRepeatValue(x, y);
                                    break;
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool HandleTouchDownEnvelopeGizmos(int x, int y)
        {
            if (HasHighlightedNote() && !highlightRepeatEnvelope && IsPointInNoteArea(x, y))
            {
                var gizmos = GetEnvelopeGizmos();
                if (gizmos != null)
                {
                    foreach (var g in gizmos)
                    {
                        if (g.Rect.Contains(x - pianoSizeX, y - headerAndEffectSizeY))
                        {
                            switch (g.Action)
                            {
                                case GizmoAction.ChangeEnvValue:
                                    StartChangeEnvelopeValue(x, y);
                                    break;
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool HandleTouchDownDPCMVolumeEnvelope(int x, int y)
        {
            if (showEffectsPanel && IsPointInEffectPanel(x, y))
            {
                var vertexIdx = GetWaveVolumeEnvelopeVertexIndex(x, y);
                if (vertexIdx >= 0)
                {
                    StartDragWaveVolumeEnvelope(x, y, vertexIdx);
                    return true;
                }
            }

            return false;
        }

        private bool HandleTouchDownDPCMMapping(int x, int y)
        {
            if (GetLocationForCoord(x, y, out _, out var noteValue) && noteValue == highlightDPCMSample)
            {
                var mapping = editInstrument.GetDPCMMapping(noteValue);

                if (mapping != null)
                {
                    StartDragDPCMSampleMapping(x, y, noteValue);
                    return true;
                }
            }

            return false;
        }

        private bool HandleTouchClickEnvelope(int x, int y)
        {
            if ((IsPointInHeader(x, y) || IsPointInNoteArea(x, y)) && x < GetPixelXForAbsoluteNoteIndex(EditEnvelope.Length))
            {
                var absIdx = Utils.Clamp(GetAbsoluteNoteIndexForPixelX(x - pianoSizeX), 0, EditEnvelope.Length - 1);
                highlightNoteAbsIndex = absIdx == highlightNoteAbsIndex ? -1 : absIdx;
                highlightRepeatEnvelope = false;
                return true;
            }

            return false;
        }

        public bool HandleTouchClickEnvelopeEffectPanel(int x, int y)
        {
            if (HasRepeatEnvelope() && IsPointInEffectPanel(x, y))
            {
                var env = EditEnvelope;
                var absIdx = Utils.Clamp(GetAbsoluteNoteIndexForPixelX(x - pianoSizeX), 0, env.Length - 1) / env.ChunkLength;
                highlightNoteAbsIndex   = absIdx == highlightNoteAbsIndex && highlightRepeatEnvelope ? -1 : absIdx;
                highlightRepeatEnvelope = true;
                return true;
            }

            return false;
        }

        private bool HandleTouchClickDPCMMapping(int x, int y)
        {
            if (GetLocationForCoord(x, y, out _, out var noteValue))
            {
                var mapping = editInstrument.GetDPCMMapping(noteValue);

                if (mapping == null)
                {
                    MapDPCMSample(noteValue);
                    highlightDPCMSample = noteValue;
                }
                else
                {
                    highlightDPCMSample = highlightDPCMSample == noteValue ? -1 : noteValue;
                }

                return true;
            }

            return false;
        }
            
        public bool HandleTouchClickHeaderSeek(int x, int y)
        {
            if (IsPointInHeader(x, y))
            {
                var absNoteIndex = GetAbsoluteNoteIndexForPixelX(x - pianoSizeX);
                App.SeekSong(SnapNote(absNoteIndex));
                return true;
            }

            return false;
        }

        private bool HandleTouchDoubleClickChannelNote(int x, int y)
        {
            if (GetLocationForCoord(x, y, out var mouseLocation, out byte noteValue))
            {
                if (mouseLocation.PatternIndex >= Song.Length)
                    return true;

                var channel = Song.Channels[editChannel];
                var noteLocation = mouseLocation;
                var note = channel.FindMusicalNoteAtLocation(ref noteLocation, noteValue);

                if (note != null)
                {
                    AbortCaptureOperation();
                    DeleteSingleNote(noteLocation, mouseLocation, note);
                }

                return true;
            }

            return false;
        }

        private void SetSingleEffectValue(int x, int y, NoteLocation location)
        {
            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[location.PatternIndex];

            if (pattern == null)
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
                pattern = channel.CreatePatternAndInstance(location.PatternIndex);
            }
            else
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
            }

            var note = pattern.GetOrCreateNoteAt(location.NoteIndex);

            var minValue = Note.GetEffectMinValue(Song, channel, selectedEffectIdx);
            var maxValue = Note.GetEffectMaxValue(Song, channel, selectedEffectIdx);
            var ratio = Utils.Clamp(1.0f - (y - headerSizeY) / (float)effectPanelSizeY, 0.0f, 1.0f);
            var value = (int)Math.Round(ratio * (maxValue - minValue) + minValue);

            note.SetEffectValue(selectedEffectIdx, value);
            MarkPatternDirty(pattern);

            App.UndoRedoManager.EndTransaction();
        }

        public bool HandleTouchClickEffectPanel(int x, int y)
        {
            if (showEffectsPanel && selectedEffectIdx >= 0 && IsPointInEffectPanel(x, y) && GetEffectNoteForCoord(x, y, out var location))
            {
                var channel = Song.Channels[editChannel];
                var note    = channel.GetNoteAt(location);
                var absIdx  = location.ToAbsoluteNoteIndex(Song);

                if (note != null && note.HasValidEffectValue(selectedEffectIdx))
                {
                    highlightNoteAbsIndex = highlightNoteAbsIndex == absIdx ? -1 : absIdx;
                }
                else
                {
                    if (SnapEnabled && SnapEffectEnabled)
                        absIdx = SnapNote(absIdx);

                    location = NoteLocation.FromAbsoluteNoteIndex(Song, absIdx);
                    SetSingleEffectValue(x, y, location);
                    highlightNoteAbsIndex = absIdx;
                }

                return true;
            }

            return false;
        }

        private void ToggleSlideNote(NoteLocation location, Note note)
        {
            var pattern = Song.Channels[editChannel].PatternInstances[location.PatternIndex];
            var selected = IsNoteSelected(location);

            if (selected && SelectionCoversMultiplePatterns())
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);

            note.SlideNoteTarget = note.IsSlideNote ? (byte)0 : (byte)(note.Value + (note.Value < Note.MusicalNoteC7 ? 5 : -5));

            if (selected)
            {
                TransformNotes(selectionMinX, selectionMaxX, false, true, false, (n, idx) =>
                {
                    if (n != null && n.IsMusical)
                    {
                        if (!note.IsSlideNote)
                            n.IsSlideNote = false;
                        else if (!n.IsSlideNote)
                            n.SlideNoteTarget = (byte)(n.Value + (n.Value < Note.MusicalNoteC7 ? 5 : -5));
                    }

                    return n;
                });

                MarkSelectedPatternsDirty();
            }
            else
            {
                MarkPatternDirty(location.PatternIndex);
            }

            App.UndoRedoManager.EndTransaction();
        }

        private void ToggleNoteRelease(NoteLocation location, Note note)
        {
            var pattern = Song.Channels[editChannel].PatternInstances[location.PatternIndex];
            var selected = IsNoteSelected(location);

            if (selected && SelectionCoversMultiplePatterns())
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);

            Song.Channels[editChannel].SetNoteDurationToMaximumLength(
                NoteLocation.FromAbsoluteNoteIndex(Song, selectionMinX),
                NoteLocation.FromAbsoluteNoteIndex(Song, selectionMaxX));

            note.Release = note.HasRelease ? 0 : Math.Max(1, Math.Min(note.Duration, App.SelectedChannel.GetDistanceToNextNote(location)) / 2);

            if (selected)
            {
                TransformNotes(selectionMinX, selectionMaxX, false, true, false, (n, idx) =>
                {
                    if (n != null && n.IsMusical)
                    {
                        if (!note.HasRelease)
                            n.HasRelease = false;
                        else if (!n.HasRelease)
                            n.Release = Math.Max(1, n.Duration / 2);
                    }

                    return n;
                });

                MarkSelectedPatternsDirty();
            }
            else
            {
                MarkPatternDirty(location.PatternIndex);
            }

            App.UndoRedoManager.EndTransaction();
        }

        private void ToggleVolumeSlide(NoteLocation location, Note note)
        {
            var pattern = Song.Channels[editChannel].PatternInstances[location.PatternIndex];
            var selected = IsNoteSelected(location);

            App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);

            if (note.HasVolumeSlide)
                note.HasVolumeSlide = false;
            else
                note.VolumeSlideTarget = (byte)Utils.Clamp(note.Volume + note.Value >= 8 ? -5 : 5, 0, Note.VolumeMax);

            MarkPatternDirty(location.PatternIndex);

            App.UndoRedoManager.EndTransaction();
        }

        private void ConvertToStopNote(NoteLocation location, Note note)
        {
            var pattern = Song.Channels[editChannel].PatternInstances[location.PatternIndex];
            App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
            note.Release = 0;
            note.Duration = 1;
            note.IsStop = true;
            note.Instrument = null;
            note.Arpeggio = null;
            MarkPatternDirty(pattern);
            App.UndoRedoManager.EndTransaction();
        }

        internal bool HandleContextMenuChannelNote(int x, int y)
        {
            if (GetLocationForCoord(x, y, out var mouseLocation, out byte noteValue))
            {
                if (!mouseLocation.IsInSong(Song))
                    return true;

                var channel = Song.Channels[editChannel];
                var noteLocation = mouseLocation;
                var note = channel.FindMusicalNoteAtLocation(ref noteLocation, noteValue);

                SetMobileHighlightedNote(noteLocation.ToAbsoluteNoteIndex(Song));

                var selection = IsNoteSelected(noteLocation);
                var menu = new List<ContextMenuOption>();

                if (note != null)
                {
                    if (note.IsMusical)
                    {
                        if (!legacySelectMode && selection)
                            menu.Insert(0, new ContextMenuOption("MenuDeleteSelection", DeleteSelectedNotesContext, () => { DeleteSelectedNotes(); }));
                        else
                            menu.Insert(0, new ContextMenuOption("MenuDelete", DeleteNoteContext, () => { DeleteSingleNote(noteLocation, mouseLocation, note); }));
                        if (channel.SupportsNoAttackNotes)
                            menu.Add(new ContextMenuOption("MenuToggleAttack", selection ? ToggleSelectedNoteAttackContext : ToggleNoteAttackContext, () => { ToggleNoteAttack(noteLocation, note); }, ContextMenuSeparator.Before));
                        if (channel.SupportsSlideNotes)
                            menu.Add(new ContextMenuOption("MenuToggleSlide", selection ? ToggleSelectedSlideNoteContext : ToggleSlideNoteContext, () => { ToggleSlideNote(noteLocation, note); }));
                        if (channel.SupportsReleaseNotes)
                            menu.Add(new ContextMenuOption("MenuToggleRelease", selection ? ToggleSelectedReleaseContext : ToggleReleaseContext, () => { ToggleNoteRelease(noteLocation, note); }));
                        if (channel.SupportsStopNotes)
                            menu.Add(new ContextMenuOption("MenuStopNote", MakeStopNoteContext, () => { ConvertToStopNote(noteLocation, note); }));
                        if (App.SelectedInstrument != null && Song.Channels[editChannel].SupportsInstrument(App.SelectedInstrument))
                            menu.Add(new ContextMenuOption("MenuReplaceSelection", ReplaceAllInstrumentContext.Format(App.SelectedInstrument), () => { ReplaceSelectionInstrument(App.SelectedInstrument, new Point(x, y), null); }));
                        if (App.SelectedInstrument != null && Song.Channels[editChannel].SupportsInstrument(App.SelectedInstrument) && note.Instrument != null && App.SelectedInstrument != note.Instrument)
                            menu.Add(new ContextMenuOption("MenuReplaceSelection", ReplaceSpecificInstrumentContext.Format(note.Instrument, App.SelectedInstrument), () => { ReplaceSelectionInstrument(App.SelectedInstrument, new Point(x, y), note.Instrument); }));
                        if (App.SelectedInstrument != null && Song.Channels[editChannel].SupportsArpeggios)
                            menu.Add(new ContextMenuOption("MenuReplaceSelection", ReplaceAllArpeggioContext.Format(App.SelectedArpeggio?.Name ?? ArpeggioNoneContext), () => { ReplaceSelectionArpeggio(App.SelectedArpeggio, new Point(x, y), null); }));
                        if (App.SelectedInstrument != null && Song.Channels[editChannel].SupportsArpeggios && App.SelectedArpeggio != note.Arpeggio)
                            menu.Add(new ContextMenuOption("MenuReplaceSelection", ReplaceSpecificInstrumentContext.Format(note.Arpeggio?.Name ?? ArpeggioNoneContext, App.SelectedArpeggio?.Name ?? ArpeggioNoneContext), () => { ReplaceSelectionArpeggio(App.SelectedArpeggio, new Point(x, y), note.Arpeggio); }));
                        if (note.Instrument != null)
                            menu.Add(new ContextMenuOption("MenuEyedropper", MakeInstrumentCurrentContext, () => { Eyedrop(note); }));

                        var factor = GetBestSnapFactorForNote(noteLocation, note);
                        if (factor >= 0)
                            menu.Add(new ContextMenuOption("MenuSnap", SetSnapContext.Format(SnapResolutionType.Names[factor]), () => { snapResolution = factor; snap = true; MarkDirty(); }));
                    }

                    if (HasSelectedNotes())
                        menu.Add(new ContextMenuOption("MenuClearSelection", ClearSelectionContext, () => { ClearSelection(); ClearHighlightedNote(); }));

                    // This only really serves a purpose in legacy select mode.
                    if (legacySelectMode)
                        menu.Add(new ContextMenuOption("MenuSelectNote", SelectNoteRangeContext, () => { SelectSingleNote(noteLocation, mouseLocation, note); }));
                }
                else
                {
                    // We want to append these options to the end of the list, after the scales.
                    var opt = new List<ContextMenuOption>();
                    note = channel.FindMusicalNoteAtLocation(ref noteLocation, -1);

                    // This only really serves a purpose in legacy select mode.
                    if (legacySelectMode && note != null)
                        opt.Add(new ContextMenuOption("MenuSelectNote", SelectNoteRangeContext, () => { SelectSingleNote(noteLocation, mouseLocation, note); }, ContextMenuSeparator.Before));

                    if (HasSelectedNotes())
                        opt.Add(new ContextMenuOption("MenuClearSelection", ClearSelectionContext, () => { ClearSelection(); ClearHighlightedNote(); }));

                    var scales = new[] { ScaleMajor, ScaleMinor, ScaleDorian, ScalePhrygian, ScaleLydian, ScaleMixolydian, ScaleLocrian, ScaleMelodicMinor, ScaleHarmonicMinor, ScaleDoubleHarmonic };
                    var roots  = new[] { "C", "C# / Db", "D", "D# / Eb", "E", "F", "F# / Gb", "G", "G# / Ab", "A", "A# / Bb", "B" };

                    var scaleOptions = new ContextMenuOption[scales.Length];
                    var rootOptions  = new ContextMenuOption[roots.Length];

                    for (var i = 0; i < scales.Length; i++)
                    {
                        var j = i; // Important, copy for lamdba.
                        var name = scales[i];

                        scaleOptions[i] = new ContextMenuOption(
                            name,
                            tooltip,
                            () => { scaleType = j; },
                            () => scaleType == j ? ContextMenuCheckState.Radio : ContextMenuCheckState.None);
                    }

                    for (var i = 0; i < roots.Length; i++)
                    {
                        var j = i; // Important, copy for lamdba.
                        var name = roots[i];

                        rootOptions[i] = new ContextMenuOption(
                            name,
                            tooltip,
                            () => { rootNoteIdx = j; },
                            () => rootNoteIdx == j ? ContextMenuCheckState.Radio : ContextMenuCheckState.None);
                    }

                    menu.Add(new ContextMenuOption(null, ScaleTypeContext, scaleOptions));
                    menu.Add(new ContextMenuOption(null, RootNoteContext, rootOptions, Platform.IsDesktop ? ContextMenuSeparator.After : ContextMenuSeparator.MobileAfter));

                    if (legacySelectMode && selection)
                        menu.Add(new ContextMenuOption("MenuDeleteSelection", DeleteSelectedNotesContext, () => { DeleteSelectedNotes(); }));
                    
                    menu.AddRange(opt);
                }

                if (menu.Count > 0)
                    App.ShowContextMenuAsync(menu.ToArray());

                return true;
            }

            return false;
        }

        private bool HandleDoubleTapLongPressChannelNote(int x, int y)
        {
            HandleTouchDoubleClickChannelNote(x, y);
            StartCaptureOperation(x, y, CaptureOperation.DeleteNotes);
            Platform.VibrateClick();
            Platform.ShowToast(window, HoldFingersToEraseMessage);
            return true;
        }

        private bool HandleTouchLongPressChannelNote(int x, int y)
        {
            return HandleContextMenuChannelNote(x, y);
        }

        public bool HandleTimelineEnvelopeContextMenu(int x, int y)
        {
            return HandleContextMenuEnvelopeInternal(x, y);
        }

        private void SetRelativeEffectScaling(bool rel)
        {
            if (rel != relativeEffectScaling)
            { 
                App.UndoRedoManager.BeginTransaction(TransactionScope.Application);
                relativeEffectScaling = rel;
                App.UndoRedoManager.EndTransaction();
                MarkDirty();
            }
        }

        public bool HandleContextMenuEffectPanel(int x, int y)
        {
            if (showEffectsPanel && selectedEffectIdx >= 0 && IsPointInEffectPanel(x, y) && GetEffectNoteForCoord(x, y, out var location))
            {
                var channel = Song.Channels[editChannel];
                var note = channel.GetNoteAt(location);
                var absIdx = location.ToAbsoluteNoteIndex(Song);
                var hasValue = note != null && note.HasValidEffectValue(selectedEffectIdx);
                var effectSelected = selectedEffectIndices.Contains(absIdx);

                SetMobileHighlightedNote(absIdx);

                var menu = new List<ContextMenuOption>();

                if (hasValue)
                {
                    menu.Add(new ContextMenuOption("Type", EnterEffectValueContext, () => { EnterEffectValue(x, y, location, note); }));

                    if (!effectSelected)
                    {
                        menu.Add(new ContextMenuOption("MenuDelete", ClearEffectValueContext, () => { ClearEffectValue(location, false); }, ContextMenuSeparator.After));
                    }
                }

                if (!legacySelectMode && effectSelected)
                {
                    menu.Add(new ContextMenuOption("MenuDeleteSelection", ClearSelectEffectValuesContext, () => { ClearSelectedEffectValues(); }, ContextMenuSeparator.After));
                }

                if (legacySelectMode && IsNoteSelected(location))
                {
                    menu.Add(new ContextMenuOption("MenuDeleteSelection", ClearSelectEffectValuesContext, () => { ClearEffectValue(location, true); }));
                    menu.Add(new ContextMenuOption("MenuDeleteSelection", DeleteSelectedNotesContext, () => { DeleteSelectedNotes(); }, ContextMenuSeparator.After));
                }

                if (IsSelectionValid())
                {
                    menu.Add(new ContextMenuOption("MenuClearSelection", ClearSelectionContext, () => { ClearSelection(); ClearHighlightedNote(); }, ContextMenuSeparator.After));
                }

                if (hasValue && selectedEffectIdx == Note.EffectVolume && channel.SupportsEffect(Note.EffectVolumeSlide))
                {
                    menu.Add(new ContextMenuOption("MenuToggleSlide", ToggleVolumeSlideContext, () => { ToggleVolumeSlide(location, note); }, ContextMenuSeparator.After));
                }

                if (IsSelectionValid())
                {
                    menu.Add(new ContextMenuOption("MenuCopy", CopyEffectValuesAsEnvValuesContext, () => { CopyEffectValues(false); }));

                    if (Platform.IsDesktop)
                    {
                        menu.Add(new ContextMenuOption("MenuCopy", CopyEffectValuesAsTextContext, () => { CopyEffectValues(true); }, ContextMenuSeparator.After));
                    }
                }

                menu.Add(new ContextMenuOption(AbsoluteEffectScalingContext, AbsoluteValueScalingContextTooltip, () => { SetRelativeEffectScaling(false); }, () => !relativeEffectScaling ? ContextMenuCheckState.Radio : ContextMenuCheckState.None, ContextMenuSeparator.MobileBefore));
                menu.Add(new ContextMenuOption(RelativeEffectScalingContext, RelativeValueScalingContextTooltip, () => { SetRelativeEffectScaling(true); },  () =>  relativeEffectScaling ? ContextMenuCheckState.Radio : ContextMenuCheckState.None, ContextMenuSeparator.After));

                if (menu.Count > 0)
                    App.ShowContextMenuAsync(menu.ToArray());

                return true;
            }

            return false;
        }

        private bool HandleTouchLongPressEffectPanel(int x, int y)
        {
            return HandleContextMenuEffectPanel(x, y);
        }

        private bool HandleTouchLongPressDrawEnvelope(int x, int y)
        {
            if (IsPointInNoteArea(x, y) && EditEnvelope.Length > 0)
            {
                Platform.VibrateClick();
                Platform.ShowToast(window, HoldFingersToDrawMessage);
                StartDrawEnvelope(x, y);
                return true;
            }

            return false;
        }

        private void SetEnvelopeLoopRelease(int x, int y, bool release)
        {
            var env = EditEnvelope;
            var idx = Utils.RoundDown(GetAbsoluteNoteIndexForPixelX(x - pianoSizeX), env.ChunkLength);

            if (editMode == EditionMode.Envelope)
                App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Arpeggio, editArpeggio.Id);

            if (release)
            {
                if (idx > 0)
                {
                    if (env.Loop < 0 || env.Loop >= idx)
                        env.Loop = idx - env.ChunkLength;
                    env.Release = idx;
                }
            }
            else
            {
                if (env.Release > 0)
                    env.Release = idx + env.ChunkLength;
                env.Loop = idx;
            }

            editInstrument?.NotifyEnvelopeChanged(editEnvelope, false);
            App.UndoRedoManager.EndTransaction();
        }

        private void ClearEnvelopeLoopRelease(bool release)
        {
            if (editMode == EditionMode.Envelope)
                App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Arpeggio, editArpeggio.Id);

            var env = EditEnvelope;

            if (release)
                env.Release = -1;
            else
                env.Loop = -1;

            editInstrument?.NotifyEnvelopeChanged(editEnvelope, false);
            App.UndoRedoManager.EndTransaction();
        }

            private bool HandleContextMenuEnvelopeInternal(int x, int y)
            {
                var env = EditEnvelope;
                var rep = EditRepeatEnvelope;
                var lastPixel = GetPixelXForAbsoluteNoteIndex(env.Length);
                var menu = new List<ContextMenuOption>();
                var absIdx = Utils.Clamp(GetAbsoluteNoteIndexForPixelX(x - pianoSizeX), 0, EditEnvelope.Length - 1);

            if ((editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio) && x < lastPixel)
            {
                if (env.CanLoop || (rep != null && rep.CanLoop))
                {
                    menu.Add(new ContextMenuOption("MenuLoopPoint", SetLoopPointContext, () => { SetEnvelopeLoopRelease(x, y, false); }));
                    if (env.Loop >= 0)
                        menu.Add(new ContextMenuOption("MenuClearLoopPoint", ClearLoopPointContext, () => { ClearEnvelopeLoopRelease(false); }));
                }
                if (env.CanRelease || (rep != null && rep.CanRelease))
                {
                    if (absIdx > 0)
                        menu.Add(new ContextMenuOption("MenuRelease", SetReleasePointContext, () => { SetEnvelopeLoopRelease(x, y, true); }));
                    if (env.Release >= 0)
                        menu.Add(new ContextMenuOption("MenuClearRelease", ClearReleasePointContext, () => { ClearEnvelopeLoopRelease(true); }));
                }
            }

            if (IsSelectionValid())
            {
                if (GetEnvelopeValueForCoord(x, y, out int idx, out _) && idx < EditEnvelope.Length)
                    menu.Insert(0, new ContextMenuOption("MenuClearEnvelope", FlattenSelectionContext, () => { FlattenEnvelopeValues(idx); }, ContextMenuSeparator.After));

                menu.Add(new ContextMenuOption("MenuClearSelection", ClearSelectionContext, () => { ClearSelection(); ClearHighlightedNote(); }, ContextMenuSeparator.Before));
            }

            if (Platform.IsDesktop && IsSelectionValid())
            {
                menu.Add(new ContextMenuOption("MenuCopy", CopySelectedValuesAsTextContext, () => { CopyAsText(); }, ContextMenuSeparator.Before));
            }

            menu.Add(new ContextMenuOption(AbsoluteValueScalingContext, AbsoluteValueScalingContextTooltip, () => { SetRelativeEffectScaling(false); }, () => !relativeEffectScaling ? ContextMenuCheckState.Radio : ContextMenuCheckState.None, ContextMenuSeparator.MobileBefore));
            menu.Add(new ContextMenuOption(RelativeValueScalingContext, RelativeValueScalingContextTooltip, () => { SetRelativeEffectScaling(true); }, () => relativeEffectScaling ? ContextMenuCheckState.Radio : ContextMenuCheckState.None));

            if (menu.Count > 0)
                App.ShowContextMenuAsync(menu.ToArray());

            return true;
        }
        
        public bool HandleContextMenuEnvelope(int x, int y)
        {
            if (Platform.IsMobile && IsPointInHeader(x, y) || Platform.IsDesktop && (IsPointInHeaderTopPart(x, y) || IsPointInNoteArea(x, y)))
            {
                return HandleContextMenuEnvelopeInternal(x, y);
            }

            return false;
        }

        private bool HandleTouchLongPressEnvelopeHeader(int x, int y)
        {
            return HandleContextMenuEnvelope(x, y);
        }

        private void ResetVolumeEnvelope()
        {
            App.UndoRedoManager.BeginTransaction(TransactionScope.DPCMSample, editSample.Id);
            editSample.ResetVolumeEnvelope();
            App.UndoRedoManager.EndTransaction();
            MarkDirty();
        }

        private void ResetDPCMVolumeEnvelopeVertex(int idx)
        {
            App.UndoRedoManager.BeginTransaction(TransactionScope.DPCMSample, editSample.Id);
            editSample.VolumeEnvelope[idx].volume = 1.0f;
            editSample.Process();
            App.UndoRedoManager.EndTransaction();
            MarkDirty();
        }

        public bool HandleContextMenuWave(int x, int y)
        {
            var menu = new List<ContextMenuOption>();

            if (IsPointInEffectPanel(x, y))
            {
                var vertexIdx = GetWaveVolumeEnvelopeVertexIndex(x, y);
                if (vertexIdx >= 0)
                    menu.Add(new ContextMenuOption("MenuClearEnvelope", ResetVertexContext, () => { ResetDPCMVolumeEnvelopeVertex(vertexIdx); }));
                menu.Add(new ContextMenuOption("MenuClearEnvelope", ResetVolumeEnvelopeContext, () => { ResetVolumeEnvelope(); }));
            }

            if (IsPointInNoteArea(x, y) && IsSelectionValid())
            {
                menu.Add(new ContextMenuOption("MenuDeleteSelection", DeleteSelectedSamplesContext, () => { DeleteSelectedWaveSection(); }, ContextMenuSeparator.Before));
                menu.Add(new ContextMenuOption("MenuClearSelection", ClearSelectionContext, () => { ClearSelection(); ClearHighlightedNote(); }, ContextMenuSeparator.Before));
            }

            if (menu.Count > 0)
                App.ShowContextMenuAsync(menu.ToArray());

            return true;
        }

        private bool HandleTouchLongPressWave(int x, int y)
        {
            return HandleContextMenuWave(x, y);
        }

        internal bool HandleContextMenuDPCMMapping(int x, int y)
        {
            if (GetLocationForCoord(x, y, out _, out var noteValue))
            {
                var mapping = editInstrument.GetDPCMMapping(noteValue);

                if (mapping != null)
                {
                    if (Platform.IsMobile)
                        highlightDPCMSample = noteValue;

                    App.ShowContextMenuAsync(new[]
                    {
                        new ContextMenuOption("MenuDelete", RemoveDPCMSampleContext, () => { ClearDPCMSampleMapping(noteValue); }),
                        new ContextMenuOption("MenuProperties", DPCMSamplePropertiesContext, () => { EditDPCMSampleMappingProperties(new Point(x, y), mapping); }),
                    });

                    return true;
                }
            }

            return true;
        }

        private bool HandleTouchLongPressDPCMMapping(int x, int y)
        {
            return HandleContextMenuDPCMMapping(x, y);
        }

        internal void StartMobileZoom(int x, int y, bool vertical)
        {
            if (captureOperation != CaptureOperation.None)
            {
                Debug.Assert(captureOperation != CaptureOperation.MobileZoomVertical && captureOperation != CaptureOperation.MobileZoom);
                AbortCaptureOperation(true);
            }

            StartCaptureOperation(x, y, vertical ? CaptureOperation.MobileZoomVertical : CaptureOperation.MobileZoom);
            SetMouseLastPos(x, y);
        }

        private void HandleTouchLongPressRelease(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            if (editMode == EditionMode.Channel)
            {
                if (HandleTouchLongPressChannelNote(x, y)) goto Handled;
                if (HandleTouchLongPressEffectPanel(x, y)) goto Handled;
            }

            if (editMode == EditionMode.Envelope ||
                editMode == EditionMode.Arpeggio)
            {
                if (HandleTouchLongPressDrawEnvelope(x, y)) goto Handled;
                if (HandleTouchLongPressEnvelopeHeader(x, y)) goto Handled;
            }

            if (editMode == EditionMode.DPCM)
            {
                if (HandleTouchLongPressWave(x, y)) goto Handled;
            }

            if (editMode == EditionMode.DPCMMapping)
            {
                if (HandleTouchLongPressDPCMMapping(x, y)) goto Handled;
            }

            return;

            Handled:
                MarkDirty();
        }

        protected void OnTouchDown(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            SetFlingVelocity(0, 0);
            SetMouseLastPos(x, y);
            
            // Special case, this operation is triggered on a double-tap, and a "TouchDown" is emitted
            // immediately after, so ignore the tap here.
            if (captureOperation == CaptureOperation.DeleteNotes)
            {
                return;
            }

            if (editMode == EditionMode.Channel)
            {
                if (HandleTouchDownNoteEffectsGizmos(x, y)) goto Handled;
                if (HandleTouchDownDragNote(x, y)) goto Handled;
            }

            if (editMode == EditionMode.Envelope ||
                editMode == EditionMode.Arpeggio)
            {
                if (HandleTouchDownEnvelopeSelection(x, y)) goto Handled;
                if (HandleTouchDownEnvelopeResize(x, y)) goto Handled;
                if (HandleTouchDownEnvelopeGizmos(x, y)) goto Handled;
            }

            if (editMode == EditionMode.DPCMMapping)
            {
                if (HandleTouchDownDPCMMapping(x, y)) goto Handled;
            }

            if (HandleTouchDownPan(x, y)) goto Handled;

            return;

        Handled:
            MarkDirty();
        }

        protected void OnTouchMove(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            UpdateCaptureOperation(x, y);
            SetMouseLastPos(x, y);
        }

        protected void OnTouchUp(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            // In the modern select mode, we use long press release for the context menu here.
            var context = !legacySelectMode && e.IsLongPress && captureOperation == CaptureOperation.Select && !captureThresholdMet;
            if (context)
            {
                HandleTouchLongPressRelease(e);
            }

            EndCaptureOperation(x, y);
            SetMouseLastPos(x, y);
        }

        protected override void OnTouchFling(PointerEventArgs e)
        {
            HandleTouchFling(e.X, e.Y, e.FlingVelocityX, e.FlingVelocityY);
        }

        public void HandleTouchFling(int x, int y, float velX, float velY)
        {
            if (canFling)
            {
                EndCaptureOperation(x, y);
                SetFlingVelocity(velX, velY);
            }
        }

        public void HandleTouchScaleBegin(int x, int y, bool vertical)
        {
            StartMobileZoom(x, y, vertical);
        }

        public void HandleTouchScale(int x, int y, float scale)
        {
            UpdateCaptureOperation(x, y, scale);
            SetMouseLastPos(x, y);
        }

        public void HandleTouchScaleEnd(int x, int y)
        {
            EndCaptureOperation(x, y);
            SetMouseLastPos(x, y);
        }

        protected override void OnTouchScaleBegin(PointerEventArgs e)
        {
            base.OnTouchScaleBegin(e);
            StartMobileZoom(e.X, e.Y, false);
        }

        protected override void OnTouchScale(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            UpdateCaptureOperation(x, y, e.TouchScale);
            SetMouseLastPos(x, y);
        }

        protected override void OnTouchScaleEnd(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            EndCaptureOperation(x, y);
            SetMouseLastPos(x, y);
        }

        protected override void OnTouchClick(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            SetMouseLastPos(x, y);

            if (editMode == EditionMode.Envelope ||
                editMode == EditionMode.Arpeggio)
            {
                if (HandleTouchClickEnvelope(x, y)) goto Handled;
            }

            if (editMode == EditionMode.DPCMMapping)
            {
                if (HandleTouchClickDPCMMapping(x, y)) goto Handled;
            }

            return;

        Handled:
            MarkDirty();
        }

        protected override void OnTouchDoubleClick(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            SetMouseLastPos(x, y);

            // Ignore double tap if we handled a single tap recently.
            if (captureOperation != CaptureOperation.None || (DateTime.Now - lastNoteCreateTime).TotalMilliseconds < 500)
            {
                return;
            }

            if (editMode == EditionMode.Channel)
            {
                if (HandleTouchDoubleClickChannelNote(x, y)) goto Handled;
            }

            return;

        Handled:
            MarkDirty();
        }

        protected override void OnTouchLongPress(PointerEventArgs e)
        {
            var x = e.X;
            var y = e.Y;

            if (captureOperation == CaptureOperation.DeleteNotes                ||
                captureOperation == CaptureOperation.ChangeEnvelopeValue        ||
                captureOperation == CaptureOperation.ChangeEffectValue          ||
                captureOperation == CaptureOperation.ChangeSelectionEffectValue ||
                captureOperation == CaptureOperation.ChangeEnvelopeRepeatValue  ||
                captureOperation == CaptureOperation.ResizeNoteStart            ||
                captureOperation == CaptureOperation.ResizeSelectionNoteStart   ||
                captureOperation == CaptureOperation.ResizeNoteEnd              ||
                captureOperation == CaptureOperation.ResizeSelectionNoteEnd     ||
                captureOperation == CaptureOperation.DragSlideNoteTargetGizmo   ||
                captureOperation == CaptureOperation.DragVolumeSlideTargetGizmo ||
                captureOperation == CaptureOperation.MoveNoteRelease            ||
                captureOperation == CaptureOperation.MoveSelectionNoteRelease) 
            {
                return;
            }

            AbortCaptureOperation();

            if (e.IsDoubleTapLongPress)
            {
                if (editMode == EditionMode.Channel)
                {
                    if (HandleDoubleTapLongPressChannelNote(x, y)) goto Handled;
                }

                return;
            }

            // Trigger context menu if using legacy selection mode or selecting a note. Otherwise, start a selection.
            var validNoteArea = editMode == EditionMode.Channel && IsPointInNoteArea(x, y);
            var note = validNoteArea ? GetNoteForCoord(x, y, out _, out _, out _) : null;

            // Same idea for the effect panel: long press starts a selection, release pops a context menu if we didn't swipe.
            var validEffectArea = editMode == EditionMode.Channel && !HasRepeatEnvelope() &&
                selectedEffectIdx >= 0 && IsPointInEffectPanel(x, y) && GetEffectNoteForCoord(x, y, out _);

            if (!legacySelectMode && (validNoteArea && note == null || validEffectArea))
            {
                Platform.VibrateClick();
                StartSelection(x, y);
            }
            else
            {
                HandleTouchLongPressRelease(e);
            }
            
            return;

        Handled:
            MarkDirty();
        }

        public void LayoutChanged()
        {
            UpdateRenderCoords();
            ClampScroll();
            MarkDirty();
        }

        protected override void OnResize(EventArgs e)
        {
            UpdateRenderCoords();
            ClampScroll();

            if (Platform.IsMobile && (editMode == EditionMode.Arpeggio || editMode == EditionMode.Envelope))
                CenterEnvelopeScroll();

            timeline.UpdateLayout();
            piano.UpdateLayout();
        }

        private void GetMinMaxScroll(out int minScrollX, out int minScrollY, out int maxScrollX, out int maxScrollY)
        {
            minScrollX = 0;
            minScrollY = 0;
            maxScrollX = 0;
            maxScrollY = Math.Max(virtualSizeY + headerAndEffectSizeY - Height, 0);

            if (editMode == EditionMode.Channel ||
                editMode == EditionMode.VideoRecording)
            {
                maxScrollX = Math.Max(GetPixelXForAbsoluteNoteIndex(Song.GetPatternStartAbsoluteNoteIndex(Song.Length), false) - scrollMargin, 0);
            }
            else if (editMode == EditionMode.Envelope ||
                     editMode == EditionMode.Arpeggio)
            {
                maxScrollX = Math.Max(GetPixelXForAbsoluteNoteIndex(EditEnvelope.Length, false) - scrollMargin, 0);
            }
            else if (editMode == EditionMode.DPCM)
            {
                maxScrollX = Math.Max((int)Math.Ceiling(GetPixelForWaveTime(Math.Max(editSample.SourceDuration, editSample.ProcessedDuration))) - scrollMargin, 0);
                minScrollY = Math.Max(virtualSizeY + headerAndEffectSizeY - Height, 0) / 2;
                maxScrollY = minScrollY;
            }
        }

        private bool ClampScroll()
        {
            var scrolledX = true;
            var scrolledY = true;

            if (Song != null)
            {
                GetMinMaxScroll(out var minScrollX, out var minScrollY, out var maxScrollX, out var maxScrollY);

                if (scrollX < minScrollX) { scrollX = minScrollX; scrolledX = false; }
                if (scrollX > maxScrollX) { scrollX = maxScrollX; scrolledY = false; }
                if (scrollY < minScrollY) { scrollY = minScrollY; scrolledY = false; }
                if (scrollY > maxScrollY) { scrollY = maxScrollY; scrolledY = false; }
            }

            ScrollChanged?.Invoke();

            return scrolledX || scrolledY;
        }

        private bool DoScroll(int deltaX, int deltaY)
        {
            scrollX -= deltaX;
            scrollY -= deltaY;
            MarkDirty();
            return ClampScroll();
        }

        public void StartPan(int x, int y, bool capturePointer = true)
        {
            panning = true;
            CaptureMouse(x, y, capturePointer);
        }

        private void SetSelection(int min, int max)
        {
            int rangeMax;

            if (editMode == EditionMode.Channel ||
                editMode == EditionMode.Envelope ||
                editMode == EditionMode.Arpeggio)
            {
                rangeMax = editMode == EditionMode.Channel ? Song.GetPatternStartAbsoluteNoteIndex(Song.Length) - 1 : EditEnvelope.Length - 1;
            }
            else if (editMode == EditionMode.DPCM)
            {
                rangeMax = editSample.SourceData.NumSamples - 1;

                // DMC file can only select in groups of 8 samples (1 byte)
                if (!editSample.SourceDataIsWav)
                {
                    min = Utils.RoundDown(min, 8);
                    max = Utils.RoundUp  (max, 8);
                    max += (max == min) ? 7 : -1;
                }
            }
            else
            {
                return;
            }

            if (min > rangeMax)
            {
                ClearSelection();
            }
            else
            {
                selectionMinX = Utils.Clamp(min, 0, rangeMax);
                selectionMaxX = Utils.Clamp(max, min, rangeMax);
            }
        }

        internal void ClearSelection()
        {
            selectionMinX = -1;
            selectionMaxX = -1;
            selectionMinY = -1;
            selectionMaxY = -1;

            selectedNoteIndices.Clear();
            selectedEffectIndices.Clear();

            selectedEnvelopeIndices.Clear();
            captureSelectedEnvelopeIndices.Clear();
        }

        internal void SetMobileHighlightedNote(int absNoteIndex)
        {
            if (Platform.IsMobile)
                highlightNoteAbsIndex = absNoteIndex;
        }

        private void ClearHighlightedNote()
        {
            highlightRepeatEnvelope = false;
            highlightNoteAbsIndex = -1;
            highlightDPCMSample = -1;
        }

        internal bool HasHighlightedNote()
        {
            return highlightNoteAbsIndex >= 0;
        }

        private bool IsHighlightedNoteSelected()
        {
            return HasHighlightedNote() && IsNoteSelected(highlightNoteAbsIndex);
        }

        private Note GetHighlightedNote()
        {
            return HasHighlightedNote() ? Song.Channels[editChannel].GetNoteAt(NoteLocation.FromAbsoluteNoteIndex(Song, highlightNoteAbsIndex)) : null; 
        }

        private Note GetHighlightedNoteAndLocation(out NoteLocation location)
        {
            location = NoteLocation.FromAbsoluteNoteIndex(Song, highlightNoteAbsIndex);
            return HasHighlightedNote() ? Song.Channels[editChannel].GetNoteAt(location) : null;
        }

        internal void ScrollIfNearEdge(int x, int y, bool scrollHorizontal = true, bool scrollVertical = false)
        {
            if (scrollHorizontal)
            {
                int posMinX = 0;
                int posMaxX = Platform.IsDesktop ? Width + pianoSizeX : (IsLandscape ? Width + headerSizeY : Width);
                int marginMinX = Platform.IsDesktop ? pianoSizeX : headerSizeY; 
                int marginMaxX = Platform.IsDesktop ? pianoSizeX : headerSizeY;

                scrollX += Utils.ComputeScrollAmount(x, posMinX, marginMinX, App.AverageTickRate * ScrollSpeedFactor, true);
                scrollX += Utils.ComputeScrollAmount(x, posMaxX, marginMaxX, App.AverageTickRate * ScrollSpeedFactor, false);
                ClampScroll();
            }

            if (scrollVertical)
            {
                int posMinY = 0;
                int posMaxY = Platform.IsMobile && !IsLandscape ? Height + headerSizeY : Height;
                int marginMinY = headerSizeY;
                int marginMaxY = headerSizeY;

                scrollY += Utils.ComputeScrollAmount(y, posMinY, marginMinY, App.AverageTickRate * ScrollSpeedFactor, true);
                scrollY += Utils.ComputeScrollAmount(y, posMaxY, marginMaxY, App.AverageTickRate * ScrollSpeedFactor, false);
                ClampScroll();
            }
        }

        internal void MarkPatternDirty(int patternIdx)
        {
            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[patternIdx];
            channel.InvalidateCumulativePatternCache(pattern);
            PatternChanged?.Invoke(pattern);
        }

        internal void MarkPatternDirty(Pattern pattern)
        {
            pattern.InvalidateCumulativeCache();
            PatternChanged?.Invoke(pattern);
        }

        private void MarkSelectedPatternsDirty()
        {
            if (IsSelectionValid())
            {
                var channel = Song.Channels[editChannel];
                var patternMin = Song.PatternIndexFromAbsoluteNoteIndex(selectionMinX);
                var patternMax = Song.PatternIndexFromAbsoluteNoteIndex(selectionMaxX);

                channel.InvalidateCumulativePatternCache(patternMin, patternMax);

                for (int i = patternMin; i <= patternMax; i++)
                {
                    var pattern = channel.PatternInstances[i];
                    if (pattern != null)
                        PatternChanged?.Invoke(pattern);
                }
            }
        }

        public void StartSelection(int x, int y, bool capturePointer = true)
        {
            captureSelectionFromHeader = IsPointInHeader(x, y);
            captureSelectionFromEffectPanel = IsPointInEffectPanel(x, y);

            captureMarqueeMinX = -1;
            captureMarqueeMaxX = -1;
            captureMarqueeMinY = -1;
            captureMarqueeMaxY = -1;

            captureSelectedNoteIndices.Clear();

            var keepLast = !legacySelectMode && (ModifierKeys.IsControlDown || (Platform.IsMobile && Settings.RetainPreviousSelection));
            if (keepLast && editMode == EditionMode.Channel)
            {
                foreach (var idx in selectedNoteIndices)
                    captureSelectedNoteIndices.Add(selectionMinX + idx);
            }

            captureSelectedEffectIndices.Clear();

            if (keepLast && editMode == EditionMode.Channel && captureSelectionFromEffectPanel)
            {
                foreach (var idx in selectedEffectIndices)
                    captureSelectedEffectIndices.Add(idx);
            }

            captureSelectedEnvelopeIndices.Clear();

            if (keepLast && (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio))
            {
                foreach (var idx in selectedEnvelopeIndices)
                    captureSelectedEnvelopeIndices.Add(idx);
            }

            StartCaptureOperation(x, y, CaptureOperation.Select, false, capturePointer: capturePointer);

            if (captureThresholdMet)
                UpdateSelection(x, y);
        }

        private void UpdateSelection(int x, int y)
        {
            ScrollIfNearEdge(x, y);

            int noteIdx = GetAbsoluteNoteIndexForPixelX(x - pianoSizeX);

            int minSelectionIdx = Math.Min(noteIdx, captureMouseAbsoluteIdx);
            int maxSelectionIdx = Math.Max(noteIdx, captureMouseAbsoluteIdx);
            int pad = SnapEnabled && !SnapTemporarelyDisabled ? -1 : 0;

            var marqueeMinX = SnapNote(minSelectionIdx);
            var marqueeMaxX = SnapNote(maxSelectionIdx, true) + pad;

            if (legacySelectMode)
            {
                SetSelection(marqueeMinX, marqueeMaxX);
                MarkDirty();
                return;
            }

            if (editMode == EditionMode.Channel)
            {
                var songEnd = Song.GetPatternStartAbsoluteNoteIndex(Song.Length) - 1;

                marqueeMinX = Utils.Clamp(marqueeMinX, 0, songEnd);
                marqueeMaxX = Utils.Clamp(marqueeMaxX, 0, songEnd);
            }

            captureMarqueeMinX = marqueeMinX;
            captureMarqueeMaxX = marqueeMaxX;

            var keepLast = ModifierKeys.IsControlDown || (Platform.IsMobile && Settings.RetainPreviousSelection);
            
            // Envelope and arpeggio selection supports using CTRL for separate selections.
            if (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio)
            {
                var result = keepLast ? new HashSet<int>(captureSelectedEnvelopeIndices) : new HashSet<int>();

                var min = Math.Max(marqueeMinX, 0);
                var max = Math.Min(marqueeMaxX, EditEnvelope.Length - 1);

                for (var i = min; i <= max; i++)
                    result.Add(i);

                selectedEnvelopeIndices.Clear();

                if (result.Count > 0)
                {
                    selectionMinX = int.MaxValue;
                    selectionMaxX = int.MinValue;

                    foreach (var idx in result)
                    {
                        selectedEnvelopeIndices.Add(idx);

                        selectionMinX = Math.Min(selectionMinX, idx);
                        selectionMaxX = Math.Max(selectionMaxX, idx);
                    }
                }
                else
                {
                    selectionMinX = marqueeMinX;
                    selectionMaxX = marqueeMaxX;
                }

                MarkDirty();
                return;
            }

            if (editMode != EditionMode.Channel)
            {
                SetSelection(marqueeMinX, marqueeMaxX);
                MarkDirty();
                return;
            }

            if (captureSelectionFromEffectPanel)
            {
                selectedNoteIndices.Clear();

                var result = keepLast ? new HashSet<int>(captureSelectedEffectIndices) : new HashSet<int>();
                var channel = Song.Channels[editChannel];
                var minLocation = NoteLocation.FromAbsoluteNoteIndex(Song, marqueeMinX);
                var maxLocation = NoteLocation.FromAbsoluteNoteIndex(Song, marqueeMaxX);

                if (selectedEffectIdx >= 0)
                {
                    for (var it = channel.GetSparseNoteIterator(minLocation, maxLocation, Note.GetFilterForEffect(selectedEffectIdx)); !it.Done; it.Next())
                    {
                        if (it.Note.HasValidEffectValue(selectedEffectIdx))
                            result.Add(it.Location.ToAbsoluteNoteIndex(Song));
                    }
                }

                selectedEffectIndices.Clear();

                if (result.Count > 0)
                {
                    selectionMinX = int.MaxValue;
                    selectionMaxX = int.MinValue;

                    foreach (var idx in result)
                    {
                        selectedEffectIndices.Add(idx);

                        selectionMinX = Math.Min(selectionMinX, idx);
                        selectionMaxX = Math.Max(selectionMaxX, idx);
                    }
                }
                else
                {
                    selectionMinX = marqueeMinX;
                    selectionMaxX = marqueeMaxX;
                }

                selectionMinY = -1;
                selectionMaxY = -1;

                MarkDirty();
                return;
            }

            var notesInMarquee = new HashSet<int>();

            if (captureSelectionFromHeader)
            {
                // A musical note that started before the selected range but whose duration
                // still overlaps it needs to be included too, same as the 2D marquee below.
                FindPreviousNotes(marqueeMinX, out var prevAnyIdx, out var prevAnyNote, out _, out _);

                if (prevAnyNote != null && prevAnyNote.IsMusical)
                {
                    var duration = GetVisualNoteDuration(prevAnyIdx, prevAnyNote);
                    var noteEnd = prevAnyIdx + duration - 1;

                    if (noteEnd >= marqueeMinX)
                        notesInMarquee.Add(prevAnyIdx);
                }

                ForEachNoteInRange(marqueeMinX, marqueeMaxX, (note, idx) =>
                {
                    if (!note.IsEmpty)
                        notesInMarquee.Add(idx);
                });
            }
            else
            {
                // Note area itself. This is where we are using 2 dimensional selection.
                int noteValue   = NumNotes - Utils.Clamp((y + scrollY - headerAndEffectSizeY) / noteSizeY, 0, NumNotes);
                var marqueeMinY = Math.Min(noteValue, captureNoteValue);
                var marqueeMaxY = Math.Max(noteValue, captureNoteValue);

                captureMarqueeMinY = marqueeMinY;
                captureMarqueeMaxY = marqueeMaxY;

                FindPreviousNotes(marqueeMinX, out var prevAnyIdx, out var prevAnyNote, out var lastMusicalIdx, out var lastMusicalNote);

                if (prevAnyNote != null && prevAnyNote.IsMusical && prevAnyNote.Value >= marqueeMinY && prevAnyNote.Value <= marqueeMaxY)
                {
                    var duration = GetVisualNoteDuration(prevAnyIdx, prevAnyNote);
                    var noteEnd = prevAnyIdx + duration - 1;

                    if (noteEnd >= marqueeMinX)
                        notesInMarquee.Add(prevAnyIdx);
                }

                ForEachNoteInRange(marqueeMinX, marqueeMaxX, (note, idx) =>
                {
                    if (note.IsMusical)
                    {
                        if (note.Value >= marqueeMinY && note.Value <= marqueeMaxY)
                        {
                            var duration = GetVisualNoteDuration(idx, note);
                            var noteEnd = idx + duration - 1;

                            if (idx <= marqueeMaxX && noteEnd >= marqueeMinX)
                                notesInMarquee.Add(idx);
                        }

                        lastMusicalIdx = idx;
                        lastMusicalNote = note;
                    }
                    else if (note.IsStop)
                    {
                        // A stop note will always be positioned at C4 unless it follows a musical note.
                        var stopY = Note.MusicalNoteC4;

                        if (lastMusicalNote != null)
                        {
                            var prevDuration = GetVisualNoteDuration(lastMusicalIdx, lastMusicalNote);
                            if (lastMusicalIdx + prevDuration == idx)
                                stopY = lastMusicalNote.Value;
                        }

                        if (stopY >= marqueeMinY && stopY <= marqueeMaxY && idx <= marqueeMaxX && idx >= marqueeMinX)
                            notesInMarquee.Add(idx);
                    }
                });
            }

            if (keepLast)
            {
                var result = new HashSet<int>(captureSelectedNoteIndices);

                foreach (var absoluteIdx in notesInMarquee)
                    result.Add(absoluteIdx);

                selectedNoteIndices.Clear();

                if (result.Count > 0)
                {
                    selectionMinX = int.MaxValue;
                    selectionMaxX = int.MinValue;

                    foreach (var absoluteIdx in result)
                    {
                        selectionMinX = Math.Min(selectionMinX, absoluteIdx);
                        selectionMaxX = Math.Max(selectionMaxX, absoluteIdx);
                    }

                    foreach (var absoluteIdx in result)
                        selectedNoteIndices.Add(absoluteIdx - selectionMinX);

                    var channel = Song.Channels[editChannel];

                    selectionMinY = Note.MusicalNoteMax;
                    selectionMaxY = Note.MusicalNoteMin;

                    foreach (var absoluteIdx in result)
                    {
                        var location = NoteLocation.FromAbsoluteNoteIndex(Song, absoluteIdx);
                        var note = channel.GetNoteAt(location);

                        if (note != null && note.IsMusical)
                        {
                            selectionMinY = Math.Min(selectionMinY, note.Value);
                            selectionMaxY = Math.Max(selectionMaxY, note.Value);
                        }
                    }
                }
                else
                {
                    selectionMinX = -1;
                    selectionMaxX = -1;
                    selectionMinY = -1;
                    selectionMaxY = -1;
                }
            }
            else
            {
                selectionMinX = marqueeMinX;
                selectionMaxX = marqueeMaxX;

                if (notesInMarquee.Count > 0)
                {
                    foreach (var absoluteIdx in notesInMarquee)
                    {
                        selectionMinX = Math.Min(selectionMinX, absoluteIdx);
                        selectionMaxX = Math.Max(selectionMaxX, absoluteIdx);
                    }
                }

                if (!captureSelectionFromHeader)
                {
                    selectionMinY = captureMarqueeMinY;
                    selectionMaxY = captureMarqueeMaxY;
                }

                selectedNoteIndices.Clear();

                foreach (var absoluteIdx in notesInMarquee)
                    selectedNoteIndices.Add(absoluteIdx - selectionMinX);
            }

            UpdateSelectedEffectsFromNotes();
            MarkDirty();
        }

        private void PostProcessSelection()
        {
            // In modern selection, we need to trim any empty frames.
            if (legacySelectMode || editMode != EditionMode.Channel)
                return;

            if (selectedNoteIndices.Count == 0 && selectedEffectIndices.Count == 0)
            {
                ClearSelection();
                return;
            }

            var oldMinX = selectionMinX;
            var minX = int.MaxValue;
            var maxX = int.MinValue;

            foreach (var idx in selectedNoteIndices)
            {
                var absoluteIdx = oldMinX + idx;
                minX = Math.Min(minX, absoluteIdx);
                maxX = Math.Max(maxX, absoluteIdx);
            }

            foreach (var idx in selectedEffectIndices)
            {
                minX = Math.Min(minX, idx);
                maxX = Math.Max(maxX, idx);
            }

            if (minX != oldMinX)
            {
                var delta = oldMinX - minX;
                var rebased = new HashSet<int>();

                foreach (var idx in selectedNoteIndices)
                    rebased.Add(idx + delta);

                selectedNoteIndices = rebased;
            }

            selectionMinX = minX;
            selectionMaxX = maxX;
        }

        private void UpdateSelectedEffectsFromNotes()
        {
            selectedEffectIndices.Clear();

            if (legacySelectMode || selectedEffectIdx < 0)
                return;

            var channel = Song.Channels[editChannel];

            foreach (var offset in selectedNoteIndices)
            {
                var noteAbsIdx = selectionMinX + offset;
                var location = NoteLocation.FromAbsoluteNoteIndex(Song, noteAbsIdx);
                var note = channel.GetNoteAt(location);

                if (note == null || !note.IsMusical)
                    continue;

                var duration = GetVisualNoteDuration(location, note);
                var endAbsIdx = noteAbsIdx + duration - 1;

                var minLocation = NoteLocation.FromAbsoluteNoteIndex(Song, noteAbsIdx);
                var maxLocation = NoteLocation.FromAbsoluteNoteIndex(Song, endAbsIdx);

                for (var it = channel.GetSparseNoteIterator(minLocation, maxLocation, Note.GetFilterForEffect(selectedEffectIdx)); !it.Done; it.Next())
                {
                    if (it.Note.HasValidEffectValue(selectedEffectIdx))
                    {
                        selectedEffectIndices.Add(it.Location.ToAbsoluteNoteIndex(Song));
                    }
                }
            }
        }

        private void UpdateWaveSelection(int x, int y)
        {
            ScrollIfNearEdge(x, y);

            float time = Math.Max(0.0f, GetWaveTimeForPixel(x - pianoSizeX));

            float minSelectionTime = Math.Min(time, captureWaveTime);
            float maxSelectionTime = Math.Max(time, captureWaveTime);

            int minSample = (int)Math.Round(minSelectionTime * editSample.SourceSampleRate);
            int maxSample = (int)Math.Round(maxSelectionTime * editSample.SourceSampleRate);

            // The first sample show in the DMC is the initial DMC counter, it doesnt really exist.
            if (!editSample.SourceDataIsWav)
            {
                minSample--;
                maxSample--;
            }

            SetSelection(minSample, maxSample);
            MarkDirty();
        }

        private void UpdateSeekDrag(int x, int y, bool final)
        {
            dragSeekPosition = GetAbsoluteNoteIndexForPixelX(x - pianoSizeX);
            dragSeekPosition = SnapNote(dragSeekPosition);

            if (final)
                App.SeekSong(dragSeekPosition);

            MarkDirty();
        }

        private void UpdateVolumeEnvelopeDrag(int x, int y, bool final)
        {
            var halfHeight    = effectPanelSizeY * 0.5f;
            var halfHeightPad = halfHeight - waveEditor.WaveDisplayPaddingY;

            var time   = Utils.Clamp((int)Math.Round(GetWaveTimeForPixel(x - pianoSizeX) * editSample.SourceSampleRate), 0, editSample.SourceNumSamples - 1);
            var volume = Utils.Clamp(((y - headerSizeY) - halfHeight) / -halfHeightPad + 1.0f, 0.0f, 2.0f);

            // Cant move 1st and last vertex.
            if (volumeEnvelopeDragVertex != 0 &&
                volumeEnvelopeDragVertex != editSample.VolumeEnvelope.Length - 1)
            {
                editSample.VolumeEnvelope[volumeEnvelopeDragVertex].sample = time;
            }

            editSample.VolumeEnvelope[volumeEnvelopeDragVertex].volume = volume;
            editSample.SortVolumeEnvelope(ref volumeEnvelopeDragVertex);
            editSample.Process();

            if (final)
                App.UndoRedoManager.EndTransaction();

            MarkDirty();
        }

        internal void StartSlideNoteCreation(int x, int y, NoteLocation location, Note note, byte noteValue)
        {
            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[location.PatternIndex];

            if (channel.SupportsSlideNotes)
            {
                if (note != null)
                {
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
                    StartCaptureOperation(x, y, CaptureOperation.DragSlideNoteTarget, false, location.ToAbsoluteNoteIndex(Song));
                }
                else
                {
                    if (channel.SupportsInstrument(App.SelectedInstrument, false))
                    {
                        if (pattern != null)
                        {
                            App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
                        }
                        else
                        {
                            App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
                            pattern = channel.CreatePatternAndInstance(location.PatternIndex);
                        }

                        StartCaptureOperation(x, y, CaptureOperation.CreateSlideNote, true);

                        // Apply snapping.
                        captureNoteAbsoluteIdx = SnapNote(captureNoteAbsoluteIdx);
                        captureNoteLocation = Song.AbsoluteNoteIndexToNoteLocation(captureNoteAbsoluteIdx);

                        note = pattern.GetOrCreateNoteAt(captureNoteLocation.NoteIndex);
                        note.Value = noteValue;
                        note.Duration = (ushort)Song.BeatLength;
                        note.Instrument = App.SelectedInstrument;
                        note.Arpeggio = channel.SupportsArpeggios ? App.SelectedArpeggio : null;
                    }
                    else
                    {
                        App.ShowInstrumentError(channel, true);
                        return;
                    }
                }
            }
        }

        private void UpdateSlideNoteCreation(int x, int y, bool final, bool gizmo = false)
        {
            Debug.Assert(captureNoteAbsoluteIdx >= 0);

            ScrollIfNearEdge(x, y, false, true);

            var location = NoteLocation.FromAbsoluteNoteIndex(Song, captureNoteAbsoluteIdx);
            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[location.PatternIndex];

            if (GetNoteValueForCoord(x, y, out var noteValue))
            {
                var note = pattern.GetOrCreateNoteAt(location.NoteIndex);

                if (noteValue == note.Value)
                    note.SlideNoteTarget = 0;
                else
                    note.SlideNoteTarget = noteValue;

                MarkDirty();
            }

            if (final)
            {
                if (captureOperation == CaptureOperation.CreateSlideNote && !captureThresholdMet)
                    channel.PatternInstances[location.PatternIndex].GetOrCreateNoteAt(location.NoteIndex).IsSlideNote ^= true;
                MarkPatternDirty(location.PatternIndex);
                App.UndoRedoManager.EndTransaction();
            }
        }

        internal void StartDragSlideNoteGizmo(int x, int y, NoteLocation location, Note note)
        {
            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[location.PatternIndex];

            if (note != null && channel.SupportsSlideNotes)
            {
                // -0.5 since out note values have +1 in them (-1 + 0.5 = -0.5)
                var offsetY = headerAndEffectSizeY + virtualSizeY - (int)((note.SlideNoteTarget - 0.5f) * noteSizeY) - scrollY - y;
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
                StartCaptureOperation(x, y, CaptureOperation.DragSlideNoteTargetGizmo, false, location.ToAbsoluteNoteIndex(Song), 0, offsetY);
            }
        }

        private void EnterEffectValue(int x, int y, NoteLocation location, Note note)
        {
            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[location.PatternIndex];

            var val = note.GetEffectValue(selectedEffectIdx);
            var min = Note.GetEffectMinValue(Song, channel, selectedEffectIdx);
            var max = Note.GetEffectMaxValue(Song, channel, selectedEffectIdx);
            var def = Note.GetEffectDefaultValue(Song, selectedEffectIdx);
            var dlg = new ValueInputDialog(ParentWindow, new Point(left + x, top + y), EffectType.LocalizedNames[selectedEffectIdx], val, min, max, false);

            dlg.ShowDialogAsync((r) =>
            {
                if (r == DialogResult.OK)
                {
                    var effectSelected = IsEffectFrameSelected(location.ToAbsoluteNoteIndex(Song));
                    var newVal = (int)dlg.Value;

                    if (effectSelected && SelectionCoversMultiplePatterns())
                        App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
                    else
                        App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);

                    if (effectSelected)
                    {
                        TransformNotes(selectionMinX, selectionMaxX, false, true, false, (n, idx) =>
                        {
                            if (n != null && n.HasValidEffectValue(selectedEffectIdx))
                                n.SetEffectValue(selectedEffectIdx, newVal);
                            return n;
                        });
                    }
                    else
                    {
                        note.SetEffectValue(selectedEffectIdx, newVal);
                    }

                    App.UndoRedoManager.EndTransaction();
                    MarkDirty();
                }
            });
        }

        public void ClearEffectValue(NoteLocation location, bool allowSelection = false)
        {
            var pattern = Song.Channels[editChannel].PatternInstances[location.PatternIndex];
            var note = (Note)null;

            if (pattern != null && (allowSelection || pattern.TryGetNoteWithEffectAt(location.NoteIndex, selectedEffectIdx, out note)))
            {
                if (allowSelection && SelectionCoversMultiplePatterns())
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
                else
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);

                if (allowSelection)
                {
                    TransformNotes(selectionMinX, selectionMaxX, false, true, false, (n, idx) =>
                    {
                        if (n != null)
                            n.ClearEffectValue(selectedEffectIdx);
                        return n;
                    });

                    MarkSelectedPatternsDirty();
                }
                else if (note != null)
                {
                    note.ClearEffectValue(selectedEffectIdx);
                    MarkPatternDirty(location.PatternIndex);
                }

                App.UndoRedoManager.EndTransaction();
            }
        }

        private void ClearSelectedEffectValues()
        {
            if (selectedEffectIndices.Count == 0)
                return;

            var channel = Song.Channels[editChannel];

            var minPatternIdx = int.MaxValue;
            var maxPatternIdx = int.MinValue;

            foreach (var absoluteIdx in selectedEffectIndices)
            {
                var location = NoteLocation.FromAbsoluteNoteIndex(Song, absoluteIdx);

                minPatternIdx = Math.Min(minPatternIdx, location.PatternIndex);
                maxPatternIdx = Math.Max(maxPatternIdx, location.PatternIndex);
            }

            if (minPatternIdx != maxPatternIdx)
            {
                App.UndoRedoManager.BeginTransaction(
                    TransactionScope.Channel,
                    Song.Id,
                    editChannel);
            }
            else
            {
                var pattern = channel.PatternInstances[minPatternIdx];
                App.UndoRedoManager.BeginTransaction(
                    TransactionScope.Pattern,
                    pattern.Id);
            }

            foreach (var absoluteIdx in selectedEffectIndices)
            {
                var location = NoteLocation.FromAbsoluteNoteIndex(Song, absoluteIdx);
                var note = channel.GetNoteAt(location);

                if (note != null)
                    note.ClearEffectValue(selectedEffectIdx);
            }

            channel.InvalidateCumulativePatternCache(minPatternIdx, maxPatternIdx);

            for (var i = minPatternIdx; i <= maxPatternIdx; i++)
            {
                var pattern = channel.PatternInstances[i];

                if (pattern != null)
                    PatternChanged?.Invoke(pattern);
            }

            App.UndoRedoManager.EndTransaction();

            ClearSelection();
            MarkDirty();
        }

        internal void ToggleNoteAttack(NoteLocation location, Note note)
        {
            if (note.IsMusical)
            {
                var channel = Song.Channels[editChannel];
                var pattern = channel.PatternInstances[location.PatternIndex];

                if (channel.SupportsNoAttackNotes)
                {
                    var selected = IsNoteSelected(location);

                    if (selected && SelectionCoversMultiplePatterns())
                        App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
                    else
                        App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);

                    note.HasAttack ^= true;
                    var attack = note.HasAttack;

                    if (selected)
                    {
                        TransformNotes(selectionMinX, selectionMaxX, false, true, false, (n, idx) =>
                        {
                            if (n != null && n.IsMusical)
                                n.HasAttack = attack;
                            return n;
                        });

                        MarkSelectedPatternsDirty();
                    }
                    else
                    {
                        MarkPatternDirty(location.PatternIndex);
                    }

                    App.UndoRedoManager.EndTransaction();
                }
            }
        }

        internal void MapDPCMSample(byte noteValue)
        {
            if (App.Project.Samples.Count == 0)
            {
                Platform.MessageBoxAsync(ParentWindow, NoDPCMSampleMessage, NoDPCMSampleTitle, MessageBoxButtons.OK);
            }
            else
            {
                var sampleNames = new List<string>();
                foreach (var sample in App.Project.Samples)
                    sampleNames.Add(sample.Name);

                var pitchStrings = DPCMSampleRate.GetStringList(true, FamiStudio.StaticInstance.PalPlayback, true, true);

                var dlg = new PropertyDialog(ParentWindow, AssignDPCMSampleTitle, 300);
                dlg.Properties.AddDropDownList(SelectSampleToAssignLabel.Colon, sampleNames.ToArray(), sampleNames[0], null, PropertyFlags.ForceFullWidth); // 0
                dlg.Properties.AddDropDownList(PitchLabel.Colon, pitchStrings, pitchStrings[pitchStrings.Length - 1]); // 1
                dlg.Properties.AddCheckBox(LoopLabel.Colon, false); // 2
                dlg.Properties.Build();

                dlg.ShowDialogAsync((r) =>
                {
                    if (r == DialogResult.OK)
                    {
                        App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id, -1, TransactionFlags.StopAudio);
                        var sampleName = dlg.Properties.GetPropertyValue<string>(0);
                        var mapping = editInstrument.MapDPCMSample(noteValue, App.Project.GetSample(sampleName));
                        mapping.Pitch = dlg.Properties.GetSelectedIndex(1);
                        mapping.Loop = dlg.Properties.GetPropertyValue<bool>(2);
                        App.UndoRedoManager.EndTransaction();
                        DPCMSampleMapped?.Invoke(noteValue);
                    }
                });
            }
        }

        internal void StartDragDPCMSampleMapping(int x, int y, byte noteValue)
        {
            StartCaptureOperation(x, y, CaptureOperation.DragSample);
            draggedSample = null;
        }

        internal void ClearDPCMSampleMapping(byte noteValue)
        {
            App.UndoRedoManager.BeginTransaction(TransactionScope.Instrument, editInstrument.Id, -1, TransactionFlags.StopAudio);
            editInstrument.UnmapDPCMSample(noteValue);
            if (noteValue == highlightDPCMSample)
                highlightDPCMSample = -1;
            App.UndoRedoManager.EndTransaction();
            DPCMSampleUnmapped?.Invoke(noteValue);
        }

        private void SelectSingleNote(NoteLocation noteLocation, NoteLocation mouseLocation, Note note)
        {
            var channel = Song.Channels[editChannel];
            var absoluteNoteIndex = noteLocation.ToAbsoluteNoteIndex(Song);
            SetSelection(absoluteNoteIndex, absoluteNoteIndex + Math.Min(note.Duration, channel.GetDistanceToNextNote(noteLocation)) - 1);
            MarkDirty();
        }

        public void SelectPattern(int p)
        {
            var min = Song.GetPatternStartAbsoluteNoteIndex(p);
            var max = Song.GetPatternStartAbsoluteNoteIndex(p + 1) - 1;

            if (legacySelectMode)
            {
                SetSelection(min, max);
            }
            else
            {
                selectionMinX = min;
                selectionMaxX = max;

                selectedNoteIndices.Clear();
                selectedEffectIndices.Clear();

                TransformNotes(min, max, false, false, false, (note, idx) =>
                {
                    if (note != null && note.IsMusicalOrStop)
                        selectedNoteIndices.Add(idx);

                    return note;
                });

                PostProcessSelection();
                UpdateSelectedEffectsFromNotes();
            }

            MarkDirty();
        }
        
        public void SelectAll()
        {
            if (editMode == EditionMode.Arpeggio || editMode == EditionMode.Envelope)
            {
                SetSelection(0, EditEnvelope.Length - 1);
            }
            else if (editMode == EditionMode.Channel)
            {
                if (legacySelectMode)
                {
                    SetSelection(0, Song.GetPatternStartAbsoluteNoteIndex(Song.Length));
                }
                else
                {
                    var songEnd = Song.GetPatternStartAbsoluteNoteIndex(Song.Length) - 1;

                    selectionMinX = 0;
                    selectionMaxX = songEnd;

                    selectedNoteIndices.Clear();
                    selectedEffectIndices.Clear();

                    TransformNotes(0, songEnd, false, false, false, (note, idx) =>
                    {
                        if (note != null && note.IsMusicalOrStop)
                            selectedNoteIndices.Add(idx);

                        return note;
                    });

                    PostProcessSelection();
                    UpdateSelectedEffectsFromNotes();
                }
            }

            MarkDirty();
        }

        internal void DeleteSingleNote(NoteLocation noteLocation, NoteLocation mouseLocation, Note note)
        {
            var pattern = Song.Channels[editChannel].PatternInstances[noteLocation.PatternIndex];
            var dist = noteLocation.DistanceTo(Song, mouseLocation);

            App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);

            // Special case : Remove the release point if user clicks on the release.
            if (note.HasRelease && note.Release == dist)
            {
                note.Release = 0;
            }
            else
            {
                // Preserve most effect values when deleting single notes.
                note.Clear();
                note.HasNoteDelay = false;
                note.HasCutDelay = false;
            }

            if (note.IsEmpty)
                pattern.Notes.Remove(noteLocation.NoteIndex);

            MarkPatternDirty(noteLocation.PatternIndex);
            App.UndoRedoManager.EndTransaction();
        }

        private void UpdateDeleteNotes(int x, int y)
        {
            if (GetLocationForCoord(x, y, out var mouseLocation, out byte noteValue) && mouseLocation.IsInSong(Song))
            {
                var channel = Song.Channels[editChannel];
                var noteLocation = mouseLocation;
                var note = channel.FindMusicalNoteAtLocation(ref noteLocation, noteValue);

                if (note != null)
                {
                    if (!App.UndoRedoManager.HasTransactionInProgress)
                        App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);

                    var pattern = Song.Channels[editChannel].PatternInstances[noteLocation.PatternIndex];

                    // Preserve most effect values when deleting single notes.
                    note.Clear();
                    note.HasNoteDelay = false;
                    note.HasCutDelay = false;

                    if (note.IsEmpty)
                        pattern.Notes.Remove(noteLocation.NoteIndex);

                    MarkPatternDirty(noteLocation.PatternIndex);
                }
            }

            if (Platform.IsMobile)
                MarkDirty();
        }

        private void EndDeleteNotes()
        {
            if (App.UndoRedoManager.HasTransactionInProgress)
                App.UndoRedoManager.EndTransaction();
        }

        internal void ToggleReleaseNote(NoteLocation noteLocation, NoteLocation mouseLocation, Note note)
        {
            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[noteLocation.PatternIndex];

            if (channel.SupportsReleaseNotes)
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
                var release = (ushort)Math.Max(1, Song.CountNotesBetween(noteLocation, mouseLocation));
                note.Release = note.Release == release ? 0 : release;
                MarkPatternDirty(noteLocation.PatternIndex);
                App.UndoRedoManager.EndTransaction();
            }
        }

        internal void CreateOrphanStopNote(NoteLocation location)
        {
            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[location.PatternIndex];

            if (channel.SupportsStopNotes)
            {
                if (pattern == null)
                {
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
                    pattern = channel.CreatePatternAndInstance(location.PatternIndex);
                }
                else
                {
                    App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
                }

                var note = pattern.GetOrCreateNoteAt(location.NoteIndex);
                note.Clear();
                note.Value = Note.NoteStop;
                note.Duration = 1;
                MarkPatternDirty(location.PatternIndex);
                App.UndoRedoManager.EndTransaction();
            }
        }

        internal void Eyedrop(Note note)
        {
            App.UndoRedoManager.BeginTransaction(TransactionScope.Application);
            NoteEyedropped?.Invoke(note);
            App.UndoRedoManager.EndTransaction();
        }

        public void SetNoteInstrument(NoteLocation location, Note note, Instrument instrument)
        {
            var channel = Song.Channels[editChannel];

            if (channel.SupportsInstrument(instrument))
            {
                var pattern = channel.PatternInstances[location.PatternIndex];
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
                note.Instrument = instrument;
                MarkPatternDirty(pattern);
                App.UndoRedoManager.EndTransaction();
                MarkDirty();
            }
            else
            {
                App.ShowInstrumentError(channel, true);
            }
        }

        public void ReplaceSelectionInstrument(Instrument instrument, Point pos, Instrument matchInstrument = null, bool forceInSelection = false)
        {
            if (editMode == EditionMode.Channel)
            {
                Debug.Assert(!forceInSelection || IsSelectionValid());

                var channel = Song.Channels[editChannel];

                if (channel.SupportsInstrument(instrument))
                {
                    GetLocationForCoord(pos.X, pos.Y, out var location, out var noteValue);

                    Note note = null;
                    var inSelection = false;

                    if (IsSelectionValid())
                    {
                        if (legacySelectMode)
                        {
                            inSelection = IsNoteSelected(location);
                        }
                        else
                        {
                            note = channel.FindMusicalNoteAtLocation(ref location, noteValue);
                            inSelection = note != null && IsNoteSelected(location);
                        }
                    }

                    // If dragging inside the selection, replace that. (legacy)
                    if (inSelection || forceInSelection)
                    {
                        TransformNotes(selectionMinX, selectionMaxX, true, true, false, (n, idx) =>
                        {
                            var selected = legacySelectMode || selectedNoteIndices.Contains(idx);
                            if (selected && n != null && n.IsMusical && (matchInstrument == null || n.Instrument == matchInstrument))
                            {
                                n.Instrument = instrument;
                            }

                            return n;
                        });
                    }
                    else
                    {
                        // Otherwise see if a note is under the cursor.
                        note ??= channel.FindMusicalNoteAtLocation(ref location, noteValue);

                        if (note != null)
                        {
                            var pattern = channel.PatternInstances[location.PatternIndex];
                            App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
                            note.Instrument = instrument;
                            MarkPatternDirty(pattern);
                            App.UndoRedoManager.EndTransaction();
                        }
                    }
                }
                else
                {
                    App.ShowInstrumentError(channel, true);
                }
            }
        }

        public void ReplaceSelectionArpeggio(Arpeggio arpeggio, Point pos, Arpeggio matchArpeggio = null, bool forceInSelection = false)
        {
            if (editMode == EditionMode.Channel)
            {
                Debug.Assert(!forceInSelection || IsSelectionValid());

                var channel = Song.Channels[editChannel];

                if (channel.SupportsArpeggios)
                {
                    GetLocationForCoord(pos.X, pos.Y, out var location, out var noteValue);

                    Note note = null;
                    var inSelection = false;

                    if (IsSelectionValid())
                    {
                        if (legacySelectMode)
                        {
                            inSelection = IsNoteSelected(location);
                        }
                        else
                        {
                            note = channel.FindMusicalNoteAtLocation(ref location, noteValue);
                            inSelection = note != null && IsNoteSelected(location);
                        }
                    }

                    // If dragging inside the selection, replace that. (legacy)
                    if (inSelection || forceInSelection)
                    {
                        TransformNotes(selectionMinX, selectionMaxX, true, true, false, (n, idx) =>
                        {
                            var selected = legacySelectMode || selectedNoteIndices.Contains(idx);
                            if (selected && n != null && n.IsMusical && (matchArpeggio == null || n.Arpeggio == matchArpeggio))
                            {
                                n.Arpeggio = arpeggio;
                            }

                            return n;
                        });
                    }
                    else
                    {
                        // Otherwise see if a note is under the cursor.
                        note ??= channel.FindMusicalNoteAtLocation(ref location, noteValue);

                        if (note != null)
                        {
                            App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, channel.PatternInstances[location.PatternIndex].Id);
                            note.Arpeggio = arpeggio;
                            App.UndoRedoManager.EndTransaction();
                        }
                    }
                }
            }
        }

        private readonly int[] vertexOrder = new int[] { 1, 2, 0, 3 };

        private int GetWaveVolumeEnvelopeVertexIndex(int x, int y)
        {
            Debug.Assert(editMode == EditionMode.DPCM);
            Debug.Assert(vertexOrder.Length == editSample.VolumeEnvelope.Length);

            var halfHeight    = effectPanelSizeY * 0.5f;
            var halfHeightPad = halfHeight - waveEditor.WaveDisplayPaddingY;

            var threshold = DpiScaling.ScaleForWindow(Platform.IsDesktop ? 10 : 20);

            x -= pianoSizeX;
            y -= headerSizeY;

            for (int i = 0; i < 4; i++)
            {
                var idx = vertexOrder[i];

                var vx = GetPixelForWaveTime(editSample.VolumeEnvelope[idx].sample / editSample.SourceSampleRate, scrollX);
                var vy = (int)Math.Round(halfHeight - (editSample.VolumeEnvelope[idx].volume - 1.0f) * halfHeightPad);

                var dx = Math.Abs(vx - x);
                var dy = Math.Abs(vy - y);

                if (dx < threshold &&
                    dy < threshold)
                {
                    return idx;
                }
            }

            return -1;
        }

        private bool IsPointInHeader(int x, int y)
        {
            return x >= pianoSizeX && y < headerSizeY;
        }

        private bool IsPointInHeaderTopPart(int x, int y)
        {
            return (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio) && x > pianoSizeX && y > 0 && y < headerSizeY / 2;
        }

        private bool IsPointInHeaderBottomPart(int x, int y)
        {
            return (editMode == EditionMode.Envelope || editMode == EditionMode.Arpeggio) && x > pianoSizeX && y >= headerSizeY / 2 && y < headerSizeY;
        }

        private bool IsPointWhereCanResizeEnvelope(int x, int y)
        {
            var pixel0 = GetPixelXForAbsoluteNoteIndex(EditEnvelope.Length) + pianoSizeX;
            var pixel1 = pixel0 + timeline.EnvelopeResizeWidth;

            return IsPointInHeaderTopPart(x, y) && x > pixel0 && x <= pixel1;
        }

        public bool IsPointInEffectList(int x, int y)
        {
            return showEffectsPanel && editMode == EditionMode.Channel && x < pianoSizeX && y >= headerSizeY && y < headerAndEffectSizeY;
        }

        public bool IsPointInEffectPanel(int x, int y)
        {
            return showEffectsPanel && (editMode == EditionMode.Channel || editMode == EditionMode.DPCM || editMode == EditionMode.Envelope && HasRepeatEnvelope()) && x >= pianoSizeX && y >= headerSizeY && y < headerAndEffectSizeY;
        }

        private bool IsPointInNoteArea(int x, int y)
        {
            return y >= headerAndEffectSizeY && x >= pianoSizeX;
        }

        public bool IsPointInTopLeftCorner(int x, int y)
        {
            return (editMode == EditionMode.Channel || editMode == EditionMode.DPCM && !Platform.IsMobile) && y < headerSizeY && x < pianoSizeX;
        }

        public Rectangle GetToggleEffectPanelButtonRect()
        {
            var expandButtonSize = bmpExpandedSmall.ElementSize.Width;
            return new Rectangle(effectIconPosX, effectIconPosY, expandButtonSize, expandButtonSize);
        }

        public Rectangle GetSnapButtonRect()
        {
            var snapButtonSize = bmpSnap.ElementSize.Width;
            var posX = pianoSizeX - (snapButtonSize + headerIconsPosX) * 2 - 1;
            return new Rectangle(posX, headerIconsPosY, snapButtonSize, snapButtonSize);
        }

        public Rectangle GetMaximizeButtonRect()
        {
            var snapButtonSize = bmpSnap.ElementSize.Width;
            var posX = pianoSizeX - (snapButtonSize + headerIconsPosX) - 1;
            return new Rectangle(posX, headerIconsPosY, snapButtonSize, snapButtonSize);
        }

        public Rectangle GetSnapResolutionRect()
        {
            var toggleRect = GetToggleEffectPanelButtonRect();
            var snapRect   = GetSnapButtonRect();
            return new Rectangle(toggleRect.Right, toggleRect.Top + 1, snapRect.Left - toggleRect.Right - (int)DpiScaling.Window, snapRect.Height);
        }

        public bool IsPointOnSnapButton(int x, int y)
        {
            return GetSnapButtonRect().Contains(x, y);
        }

        public bool IsPointOnSnapResolution(int x, int y)
        {
            return GetSnapResolutionRect().Contains(x, y);
        }

        public void ToggleSnap()
        {
            snap = !snap;
            MarkDirty();
        }

        private double GetEffectiveSnapResolution(NoteLocation location)
        {
            var snapFactor = SnapResolutionType.Factors[snapResolution];
            var beatLength = Song.GetPatternBeatLength(location.PatternIndex);

            if (snapFactor >= 1.0f)
            {
                return snapFactor;
            }
            else
            {
                int invSnapFactor = (int)Math.Round(1.0f / snapFactor);
                // For fractional snapping, make sure the beat length can somewhat divided.
                if (invSnapFactor > beatLength)
                {
                    return 1.0 / beatLength;
                }
                else
                {
                    return snapFactor;
                }
            }
        }

        public int SnapNote(int absoluteNoteIndex, bool roundUp = false, bool forceSnap = false)
        {
            if (SnapEnabled && !SnapTemporarelyDisabled || forceSnap)
            {
                var negativeOffset = 0;
                var firstPatternLen = Song.GetPatternLength(0);

                // If we get a negative note, we will assume that the non-existant "negative"
                // patterns are infinite copies of the first pattern.
                while (absoluteNoteIndex < 0)
                {
                    absoluteNoteIndex += firstPatternLen;
                    negativeOffset    += firstPatternLen;
                }

                var location = NoteLocation.FromAbsoluteNoteIndex(Song, absoluteNoteIndex);
                
                if (location.PatternIndex >= Song.Length)
                    return absoluteNoteIndex;

                var beatLength = Song.GetPatternBeatLength(location.PatternIndex);
                var snapFactor = GetEffectiveSnapResolution(location);

                var snappedNoteIndex = location.NoteIndex;
                if (snapFactor >= 1.0)
                {
                    var numNotes = beatLength * (int)snapFactor;
                    if (numNotes == 1) 
                        return absoluteNoteIndex;
                    snappedNoteIndex = (location.NoteIndex / numNotes + (roundUp ? 1 : 0)) * numNotes;
                  }
                else
                {
                    // Subtract the base note so that snapping inside a note is always deterministic. 
                    // Otherwise, rounding errors can create a different snapping pattern every note (6-5-5-6 like Gimmick).
                    var baseNoteIdx   = location.NoteIndex / beatLength * beatLength;
                    var beatFrameIdx  = location.NoteIndex % beatLength;
                    var numSnapPoints = (int)Math.Round(1.0 / snapFactor);

                    for (int i = numSnapPoints - 1; i >= 0; i--)
                    {
                        var snapPoint = (int)Math.Round(i / (double)numSnapPoints * beatLength);
                        if (beatFrameIdx >= snapPoint)
                        {
                            if (roundUp)
                                snapPoint = (int)Math.Round((i + 1) / (double)numSnapPoints * beatLength);
                            snappedNoteIndex = baseNoteIdx + snapPoint;
                            break;
                        }
                    }
                }

                snappedNoteIndex -= negativeOffset;

                if (!roundUp)
                    snappedNoteIndex = Math.Min(Song.GetPatternLength(location.PatternIndex) - 1, snappedNoteIndex);

                return Song.GetPatternStartAbsoluteNoteIndex(location.PatternIndex, snappedNoteIndex);
            }
            else
            {
                return absoluteNoteIndex;
            }
        }

        internal NoteLocation SnapNote(NoteLocation location, bool roundUp = false, bool forceSnap = false)
        {
            return NoteLocation.FromAbsoluteNoteIndex(Song, SnapNote(location.ToAbsoluteNoteIndex(Song), roundUp, forceSnap));
        }

        private int GetBestSnapFactorForNote(NoteLocation location, Note note)
        {
            if (note.IsMusical)
            {
                var beatLength = Song.GetPatternBeatLength(location.PatternIndex);
                var noteDuration = GetVisualNoteDuration(location, note);
                var factor = noteDuration / (float)beatLength;

                for (var i = SnapResolutionType.Min; i <= SnapResolutionType.Max; i++)
                {
                    var testFactor = (float)SnapResolutionType.Factors[i];

                    if (testFactor >= factor || Utils.IsNearlyEqual(testFactor, factor))
                        return i;
                }

                return SnapResolutionType.Max;
            }

            return -1;
        }


        private void UpdateAltZoom(int x, int y)
        {
            var deltaY = y - captureMouseY;

            if (Math.Abs(deltaY) > 50)
            {
                ZoomAtLocation(x, deltaY < 0.0f ? 2.0f : 0.5f);
                captureMouseY = y;
            }
        }

        internal bool CaptureOperationRequiresNoteHighlight
        {
            get
            {
                var op = captureOperation;
                return
                op == CaptureOperation.ResizeSelectionNoteEnd ||
                op == CaptureOperation.ResizeNoteEnd ||
                op == CaptureOperation.ResizeSelectionNoteStart ||
                op == CaptureOperation.ResizeNoteStart ||
                op == CaptureOperation.MoveNoteRelease ||
                op == CaptureOperation.DragSelection ||
                op == CaptureOperation.DragNote;
            }
        }

        private CaptureOperation GetHighlightedEffectCaptureOperationForCoord(int x, int y)
        {
            Debug.Assert(editMode == EditionMode.Channel);

            if (GetEffectNoteForCoord(x, y, out var location))
            {
                var note = Song.Channels[editChannel].GetNoteAt(location);

                if (note != null && note.HasValidEffectValue(selectedEffectIdx))
                    return CaptureOperation.ChangeEffectValue;
            }

            return CaptureOperation.None;
        }

        internal CaptureOperation GetHighlightedNoteCaptureOperationForCoord(int x, int y)
        {
            Debug.Assert(editMode == EditionMode.Channel);

            var note = GetNoteForCoord(x, y, out var mouseLocation, out var noteLocation, out var noteDuration);
            if (note != null)
            {
                if (note.IsMusical)
                {
                    var minAbsoluteNoteIdx = noteLocation.ToAbsoluteNoteIndex(Song);
                    var maxAbsoluteNoteIdx = noteLocation.ToAbsoluteNoteIndex(Song) + noteDuration;

                    var minNoteCoordX = GetPixelXForAbsoluteNoteIndex(minAbsoluteNoteIdx);
                    var maxNoteCoordX = GetPixelXForAbsoluteNoteIndex(maxAbsoluteNoteIdx);

                    x -= pianoSizeX;

                    if (x > maxNoteCoordX - noteResizeMargin)
                        return IsNoteSelected(noteLocation) ? CaptureOperation.ResizeSelectionNoteEnd : CaptureOperation.ResizeNoteEnd;
                    if (x < minNoteCoordX + noteResizeMargin)
                        return IsNoteSelected(noteLocation) ? CaptureOperation.ResizeSelectionNoteStart : CaptureOperation.ResizeNoteStart;

                    if (note.HasRelease && Song.CountNotesBetween(noteLocation, mouseLocation) == note.Release)
                        return IsNoteSelected(noteLocation) ? CaptureOperation.MoveSelectionNoteRelease : CaptureOperation.MoveNoteRelease;
                }

                return IsNoteSelected(noteLocation) ? CaptureOperation.DragSelection : CaptureOperation.DragNote;
            }

            return CaptureOperation.None;
        }

        private void UpdateCursor()
        {
            var pt = ScreenToControl(CursorPosition);
            var noteIdx = GetAbsoluteNoteIndexForPixelX(pt.X - pianoSizeX);

            if (EditEnvelope != null && EditEnvelope.CanResize && IsPointWhereCanResizeEnvelope(pt.X, pt.Y) && captureOperation != CaptureOperation.Select || captureOperation == CaptureOperation.ResizeEnvelope)
            {
                Cursor = Cursors.SizeWE;
            }
            else if (captureOperation == CaptureOperation.ChangeEffectValue ||
                     captureOperation == CaptureOperation.ChangeEnvelopeRepeatValue ||
                     HasRepeatEnvelope() && IsPointInEffectPanel(pt.X, pt.Y))
            {
                Cursor = Cursors.SizeNS;
            }
            else if ((EditEnvelope != null && IsPointInNoteArea(pt.X, pt.Y) && IsNoteSelected(noteIdx)) || captureOperation == CaptureOperation.ChangeEnvelopeValue)
            {
                Cursor = Cursors.SizeNS;
            }
            else if (ModifierKeys.IsControlDown && (captureOperation == CaptureOperation.DragNote || captureOperation == CaptureOperation.DragSelection))
            {
                Cursor = Cursors.CopyCursor;
            }
            else if (captureOperation == CaptureOperation.DeleteNotes)
            {
                Cursor = Cursors.Eraser;
            }
            else if (editMode == EditionMode.Channel && Settings.EyeDropNoteShortcut.IsKeyDown(ParentWindow))
            {
                Cursor = Cursors.Eyedrop;
            }
            else
            {
                if (editMode == EditionMode.Channel && captureOperation == CaptureOperation.None)
                {
                    if (IsPointInEffectPanel(pt.X, pt.Y))
                    {
                        var captureOp = GetHighlightedEffectCaptureOperationForCoord(pt.X, pt.Y);

                        switch (captureOp)
                        {
                            case CaptureOperation.ChangeEffectValue:
                                Cursor = Cursors.SizeNS;
                                break;
                            default:
                                Cursor = Cursors.Default;
                                break;
                        }
                    }
                    // The note-area case is now handled by NoteArea.UpdateCursor() instead - this method
                    // keeps running here (via captured-pointer continuation) for as long as a PianoRoll-owned
                    // capture (e.g. a note drag) is in progress, so re-computing it here too would immediately
                    // stomp on the Default cursor NoteArea just set once that capture ends.
                }
                else
                {
                    if (captureOperation == CaptureOperation.DragNote || captureOperation == CaptureOperation.DragSelection)
                        Cursor = Cursors.Move;
                    else
                        Cursor = Cursors.Default;
                }
            }
        }

        public override void OnContainerPointerEnterNotify(Control control, EventArgs e)
        {
            while (control != null && string.IsNullOrEmpty(control.ToolTip))
                control = control.ParentContainer;

            App.SetToolTip(control?.ToolTip ?? "");
        }

        public override void OnContainerPointerMoveNotify(Control control, PointerEventArgs e)
        {
            base.OnContainerPointerMoveNotify(control, e);

            if (control != piano)
                return;

            var p = WindowToControl(control.ControlToWindow(e.Position));
            var middle = e.Middle || (e.Left && ModifierKeys.IsAltDown && Settings.AltLeftForMiddle);

            if (middle)
                DoScroll(p.X - mouseLastX, p.Y - mouseLastY);

            SetMouseLastPos(p.X, p.Y);
        }

        protected override void OnPointerMove(PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                OnTouchMove(e);
                return;
            }

            bool middle = e.Middle || (e.Left && ModifierKeys.IsAltDown && Settings.AltLeftForMiddle);

            UpdateCursor();
            UpdateCaptureOperation(e.X, e.Y);
            UpdateHover(e);

            if (middle)
                DoScroll(e.X - mouseLastX, e.Y - mouseLastY);

            //UpdateToolTip(e); // TODO: This should be OnPointerEnter when NoteArea is its own thing. It won't need to be OnPointerMove depending on how highlighting notes is handled.
            SetMouseLastPos(e.X, e.Y);
            MarkDirty(); // TODO : This is bad.

            App.SequencerShowExpansionIcons = false;
        }

        protected override void OnPointerLeave(EventArgs e)
        {
            ClearHover();
        }

        private void UpdateHover(PointerEventArgs e)
        {
            if (Platform.IsDesktop)
            {
                var newHoverNote  = -1;
                var newHoverNoteIndex = GetAbsoluteNoteIndexForPixelX(e.X - pianoSizeX);
                var newHoverNoteCount = 1;

                if (editMode == EditionMode.Channel)
                {
                    GetLocationForCoord(e.X, e.Y, out var location, out var noteValue, true);
                    newHoverNote  = noteValue;
                    newHoverNoteIndex = location.ToAbsoluteNoteIndex(Song);

                    // This is super lame, advance until we find the next snapping boundary.
                    // We cant just advance by (beat length) * (snap precision) because we have
                    // a bunch of crazy rules in there.
                    if (SnapEnabled)
                    {
                        var newHoverNoteIndex2 = newHoverNoteIndex + 1;

                        for (int i = 1; ; i++)
                        {
                            var newAbsIndex = SnapNote(newHoverNoteIndex + i);
                            if (newAbsIndex != newHoverNoteIndex)
                            {
                                newHoverNoteIndex2 = newAbsIndex;
                                break;
                            }
                        }

                        newHoverNoteCount = newHoverNoteIndex2 - newHoverNoteIndex;
                    }
                }

                piano.HoverNote = newHoverNote;
                SetAndMarkDirty(ref hoverNoteIndex,     newHoverNoteIndex);
                SetAndMarkDirty(ref hoverNoteCount,     newHoverNoteCount);
            }
        }

        private void ClearHover()
        {
            if (Platform.IsDesktop)
            {
                piano.HoverNote = -1;
                SetAndMarkDirty(ref hoverNoteIndex, -1);
            }
        }

        public void ShowSnapResolutionContextMenu()
        {
            var options = new ContextMenuOption[SnapResolutionType.Max - SnapResolutionType.Min + 3];

            options[0] = new ContextMenuOption(SnapEnableContext, $"{SnapEnableContextTooltip} <Shift><S>", () => { snap = !snap; }, () => snap ? ContextMenuCheckState.Checked : ContextMenuCheckState.Unchecked );
            options[1] = new ContextMenuOption(SnapEffectsContext, SnapEffectsContextTooltip, () => { snapEffects = !snapEffects; }, () => snapEffects ? ContextMenuCheckState.Checked : ContextMenuCheckState.Unchecked);

            for (var i = SnapResolutionType.Min; i <= SnapResolutionType.Max; i++)
            {
                var j = i; // Important, copy for lamdba.
                var name = SnapResolutionType.Names[i];
                var plural = SnapResolutionType.Factors[i] > 1.0;
                var text = (plural ? SnapToBeatsContext : SnapToBeatContext).Format(name);
                var tooltip = (plural ? SnapToBeatsContextTooltip : SnapToBeatContextTooltip).Format(name);

                if (SnapResolutionType.KeyboardShortcuts[i] != Keys.Unknown)
                    tooltip += $" <Alt>{SnapResolutionType.KeyboardShortcuts[i] - Keys.D0}";

                options[i + 2] = new ContextMenuOption(text, tooltip, () => { snapResolution = j; }, () => snapResolution == j ? ContextMenuCheckState.Radio : ContextMenuCheckState.None, i == 0 ? ContextMenuSeparator.Before : ContextMenuSeparator.None);
            }

            App.ShowContextMenuAsync(options);
        }

        private bool HandleMouseUpEnvelope(PointerEventArgs e)
        {
            return e.Right && HandleContextMenuEnvelope(e.X, e.Y);
        }

        private bool HandleMouseUpDPCMVolumeEnvelope(PointerEventArgs e)
        {
            return e.Right && HandleContextMenuWave(e.X, e.Y);
        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            if (e.IsTouchEvent)
            {
                OnTouchUp(e);
                return;
            }

            bool middle = e.Middle;
            bool doMouseUp = false;

            if (middle)
            {
                panning = false;
            }
            else
            {
                doMouseUp = captureOperation == CaptureOperation.None;
                EndCaptureOperation(e.X, e.Y);
            }

            UpdateCursor();

            if (doMouseUp)
            {
                if (editMode == EditionMode.Envelope ||
                    editMode == EditionMode.Arpeggio)
                {
                    if (HandleMouseUpEnvelope(e)) goto Handled;
                }

                if (editMode == EditionMode.DPCM)
                {
                    if (HandleMouseUpDPCMVolumeEnvelope(e)) goto Handled;
                }

                return;

            Handled:
                MarkDirty();
            }
        }

        private void ZoomAtLocation(int x, float scale)
        {
            if (scale == 1.0f)
                return;

            // When continuously following, zoom at the seek bar location.
            if (continuouslyFollowing)
                x = (int)(Width * Settings.FollowPercent);

            Debug.Assert(Platform.IsMobile || scale == 0.5f || scale == 2.0f);

            var pixelX = x - pianoSizeX;
            var absoluteX = pixelX + scrollX;
            var prevNoteSizeX = noteSizeX;

            zoom *= scale;
            zoom = Utils.Clamp(zoom, minZoom, maxZoom);

            Debug.Assert(Platform.IsMobile || Utils.Frac(Math.Log(zoom, 2.0)) == 0.0);

            // This will update the noteSizeX.
            UpdateRenderCoords();

            absoluteX = (int)Math.Round(absoluteX * (noteSizeX / (double)prevNoteSizeX));
            scrollX = absoluteX - pixelX;

            ClampScroll();
            MarkDirty();
        }

        private void ZoomVerticallyAtLocation(int y, float scale)
        {
            if (scale == 1.0f)
                return;

            Debug.Assert(Platform.IsMobile);

            var pixelY = y - headerAndEffectSizeY;
            var absoluteY = pixelY + scrollY;
            var prevNoteSizeY = noteSizeY;

            zoomY *= scale;
            zoomY = Utils.Clamp(zoomY, MinZoomY, MaxZoomY);

            // This will update the noteSizeX.
            UpdateRenderCoords();

            absoluteY = (int)Math.Round(absoluteY * (noteSizeY / (double)prevNoteSizeY));
            scrollY = absoluteY - pixelY;

            ClampScroll();
            MarkDirty();
        }

        private bool HandleMouseWheelZoom(PointerEventArgs e)
        {
            if (e.X > pianoSizeX)
            {
                if (Settings.TrackPadControls && !ModifierKeys.IsControlDown && !ModifierKeys.IsAltDown)
                {
                    if (ModifierKeys.IsShiftDown)
                        scrollX -= Utils.SignedCeil(e.ScrollY);
                    else
                        scrollY -= Utils.SignedCeil(e.ScrollY);

                    ClampScroll();
                    return true;
                }
                else if (editMode != EditionMode.DPCMMapping)
                {
                    ZoomAtLocation(e.X, e.ScrollY < 0.0f ? 0.5f : 2.0f);
                    return true;
                }
            }

            return false;
        }

        private bool HandleMouseWheelSnapResolution(PointerEventArgs e)
        {
            if (editMode == EditionMode.Channel && (IsPointOnSnapResolution(e.X, e.Y) || IsPointOnSnapButton(e.X, e.Y)))
            {
                snapResolution = Utils.Clamp(snapResolution + (e.ScrollY > 0 ? 1 : -1), SnapResolutionType.Min, SnapResolutionType.Max);
                return true;
            }

            return false;
        }

        protected override void OnMouseWheel(PointerEventArgs e)
        {
            if (HandleMouseWheelZoom(e)) goto Handled;
            if (HandleMouseWheelSnapResolution(e)) goto Handled;

            return;

        Handled:
            MarkDirty();
        }

        protected override void OnMouseHorizontalWheel(PointerEventArgs e)
        {
            scrollX += Utils.SignedCeil(e.ScrollX);
            ClampScroll();
            MarkDirty();
        }

        public override void OnContainerMouseWheelNotify(Control control, PointerEventArgs e)
        {
            // e's coordinates are relative to whichever child control (NoteArea, a button in
            // EffectPanel, etc.) actually received the wheel event, not to PianoRoll itself.
            var pos = WindowToControl(control.ControlToWindow(e.Position));
            var buttons = (e.Left ? PointerEventArgs.ButtonLeft : 0) | (e.Right ? PointerEventArgs.ButtonRight : 0) | (e.Middle ? PointerEventArgs.ButtonMiddle : 0);
            var translated = new PointerEventArgs(buttons, pos.X, pos.Y, false, e.ScrollX, e.ScrollY);

            if (translated.ScrollX != 0)
                OnMouseHorizontalWheel(translated);
            else
                OnMouseWheel(translated);
        }

        public void UpdateFollowMode(bool force = false)
        {
            continuouslyFollowing = false;

            if ((App.IsPlaying || force) && App.FollowModeEnabled && Settings.FollowSync != Settings.FollowSyncSequencer && !panning && 
                captureOperation == CaptureOperation.None && editMode == EditionMode.Channel && !window.IsAsyncDialogInProgress && !window.IsOutOfProcessDialogInProgress)
            {
                var frame = App.CurrentFrame;
                var seekX = GetPixelXForAbsoluteNoteIndex(frame);

                if (Settings.FollowMode == Settings.FollowModeJump)
                {
                    var maxX = Width - pianoSizeX;
                    if (seekX < 0 || seekX > maxX)
                        scrollX = GetPixelXForAbsoluteNoteIndex(frame, false);
                }
                else
                {
                    continuouslyFollowing = EnsureSeekBarVisible();
                }

                ClampScroll();
            }
        }

        private void TickFling(float delta)
        {
            if (flingVelX != 0.0f ||
                flingVelY != 0.0f)
            {
                var deltaPixelX = (int)Math.Round(flingVelX * delta);
                var deltaPixelY = (int)Math.Round(flingVelY * delta);

                if ((deltaPixelX != 0 || deltaPixelY != 0) && DoScroll(deltaPixelX, deltaPixelY))
                {
                    flingVelX *= (float)Math.Exp(delta * -6.0f);
                    flingVelY *= (float)Math.Exp(delta * -6.0f);
                }
                else
                {
                    flingVelX = 0.0f;
                    flingVelY = 0.0f;
                }
            }
        }

        public override void Tick(float delta)
        {
            if (App == null)
                return;

            Debug.Assert((!window.IsAsyncDialogInProgress && !window.IsOutOfProcessDialogInProgress) || captureOperation == CaptureOperation.None);

            UpdateCaptureOperation(mouseLastX, mouseLastY, 1.0f, true);
            UpdateFollowMode();
            TickFling(delta);
        }

        public bool GetEffectNoteForCoord(int x, int y, out NoteLocation location)
        {
            if (x > pianoSizeX && y > headerSizeY && y < headerAndEffectSizeY)
            {
                var absoluteNoteIndex = GetAbsoluteNoteIndexForPixelX(x - pianoSizeX);
                location = NoteLocation.FromAbsoluteNoteIndex(Song, absoluteNoteIndex);
                if (location.PatternIndex < Song.Length)
                    return true;
            }

            location = NoteLocation.Invalid;
            return false;
        }

        internal bool GetNoteValueForCoord(int x, int y, out byte noteValue)
        {
            var rawNoteValue = ((y - headerAndEffectSizeY) + scrollY) / noteSizeY;
            noteValue = (byte)(NumNotes - Utils.Clamp(rawNoteValue, 0, NumNotes - 1));

            // Allow to go outside the window when a capture is in progress.
            var captureInProgress = captureOperation != CaptureOperation.None;
            return x > pianoSizeX && x < width && ((y > headerAndEffectSizeY && !captureInProgress) || (rawNoteValue >= 0 && captureInProgress));
        }

        internal bool GetLocationForCoord(int x, int y, out NoteLocation location, out byte noteValue, bool allowSnap = false)
        {
            var absoluteNoteIndex = Utils.Clamp(GetAbsoluteNoteIndexForPixelX(x - pianoSizeX), 0, Song.GetPatternStartAbsoluteNoteIndex(Song.Length));

            if (allowSnap)
                absoluteNoteIndex = SnapNote(absoluteNoteIndex);

            location = Song.AbsoluteNoteIndexToNoteLocation(absoluteNoteIndex);
            noteValue = (byte)(NumNotes - Utils.Clamp((y + scrollY - headerAndEffectSizeY) / noteSizeY, 0, NumNotes));

            return (x > pianoSizeX && x < width && y > headerAndEffectSizeY && location.PatternIndex < Song.Length);
        }

        internal int GetVisualNoteDuration(NoteLocation location, Note note)
        {
            var duration = note.Duration;

            var distToNext = Song.Channels[editChannel].GetDistanceToNextNote(location);
            if (distToNext >= 0)
                duration = Math.Min(duration, distToNext);

            return duration;
        }

        internal int GetVisualNoteDuration(int absIndex, Note note)
        {
            return GetVisualNoteDuration(NoteLocation.FromAbsoluteNoteIndex(Song, absIndex), note);
        }

        internal Note GetNoteForCoord(int x, int y, out NoteLocation mouseLocation, out NoteLocation noteLocation, out int duration)
        {
            Debug.Assert(editMode == EditionMode.Channel);

            if (GetLocationForCoord(x, y, out mouseLocation, out var noteValue))
            {
                noteLocation = mouseLocation;
                var note = Song.Channels[editChannel].FindMusicalNoteAtLocation(ref noteLocation, noteValue);

                if (note != null)
                {
                    duration = (int)note.Duration;

                    var distToNext = Song.Channels[editChannel].GetDistanceToNextNote(noteLocation);
                    if (distToNext >= 0)
                        duration = Math.Min(duration, distToNext);

                    return note;
                }
            }

            mouseLocation = NoteLocation.Invalid;
            noteLocation  = NoteLocation.Invalid;
            duration = -1;
            return null;
        }

        public bool GetEnvelopeValueForCoord(int x, int y, out int idx, out sbyte value)
        {
            if (Platform.IsDesktop)
            {
                var maxValue = 64 / (int)envelopeValueZoom - 1;
                value = (sbyte)(maxValue - (int)Math.Min((y + scrollY - headerAndEffectSizeY - 1) / envelopeValueSizeY, 128));
            }
            else
            {
                Envelope.GetMinMaxValueForType(editInstrument, editEnvelope, out int min, out int max);
                value = (sbyte)Math.Floor((max - min + 1) - ((y - headerAndEffectSizeY) + scrollY) / envelopeValueSizeY + min);
            }

            idx = GetAbsoluteNoteIndexForPixelX(x - pianoSizeX);

            return x > pianoSizeX;
        }

#if DEBUG
        public void ValidateIntegrity()
        {
            Debug.Assert(editMode != EditionMode.Channel || editChannel == App.SelectedChannelIndex);
        }
#endif

        private void SerializeIntSet(ProjectBuffer buffer, HashSet<int> set)
        {
            var count = set.Count;
            buffer.Serialize(ref count);

            if (buffer.IsWriting)
            {
                foreach (var value in set.OrderBy(i => i))
                {
                    var v = value;
                    buffer.Serialize(ref v);
                }
            }
            else
            {
                set.Clear();

                for (var i = 0; i < count; i++)
                {
                    var value = 0;
                    buffer.Serialize(ref value);
                    set.Add(value);
                }
            }
        }

        public void Serialize(ProjectBuffer buffer)
        {
            int editModeInt = (int)editMode;
            buffer.Serialize(ref editModeInt);
            editMode = (EditionMode)editModeInt;

            buffer.Serialize(ref editChannel);
            buffer.Serialize(ref editInstrument);
            buffer.Serialize(ref editEnvelope);
            buffer.Serialize(ref editArpeggio);
            buffer.Serialize(ref editSample);
            buffer.Serialize(ref envelopeValueZoom);
            buffer.Serialize(ref envelopeValueOffset);

            if (Settings.RestoreViewOnUndoRedo || buffer.IsWriting)
            {
                buffer.Serialize(ref scrollX);
                buffer.Serialize(ref scrollY);
                buffer.Serialize(ref zoom);
            }
            else
            {
                var dummyScroll = 0;
                var dummyZoom = 0.0f;
                buffer.Serialize(ref dummyScroll);
                buffer.Serialize(ref dummyScroll);
                buffer.Serialize(ref dummyZoom);
            }

            buffer.Serialize(ref selectedEffectIdx);
            buffer.Serialize(ref showEffectsPanel);
            buffer.Serialize(ref maximized);
            buffer.Serialize(ref selectionMinX);
            buffer.Serialize(ref selectionMaxX);
            buffer.Serialize(ref relativeEffectScaling);
            
            SerializeIntSet(buffer, selectedNoteIndices);
            SerializeIntSet(buffer, selectedEffectIndices);
            SerializeIntSet(buffer, selectedEnvelopeIndices);

            if (Platform.IsMobile)
            {
                buffer.Serialize(ref highlightRepeatEnvelope);
                buffer.Serialize(ref highlightNoteAbsIndex);
                buffer.Serialize(ref highlightDPCMSample);
            }

            if (buffer.IsReading)
            {
                BuildSupportEffectList();
                UpdateTimelineEditMode();
                UpdateRenderCoords();
                ClampScroll();
                MarkDirty();
                ReleasePointer();

                captureOperation = CaptureOperation.None;
                panning = false;
            }
        }
    }

    public class SnapResolutionType
    {
        public const int Min         = 0;
        public const int Max         = 10;
        public const int Beat        = 7;
        public const int QuarterBeat = 4;

        public static readonly double[] Factors = new[]
        {
            1.0 / 16.0,
            1.0 / 12.0,
            1.0 / 8.0,
            1.0 / 6.0,
            1.0 / 4.0,
            1.0 / 3.0,
            1.0 / 2.0,
            1.0,
            2.0,
            3.0,
            4.0
        };

        public static readonly string[] Names = new string[]
        {
            "1/16",
            "1/12",
            "1/8",
            "1/6",
            "1/4",
            "1/3",
            "1/2",
            "1",
            "2",
            "3",
            "4"
        };

        public static readonly Keys[] KeyboardShortcuts = new Keys[]
        {
            Keys.Unknown,
            Keys.Unknown,
            Keys.D4,
            Keys.Unknown,
            Keys.D3,
            Keys.Unknown,
            Keys.D2,
            Keys.D1,
            Keys.Unknown,
            Keys.Unknown,
            Keys.Unknown
        };
    }
}
