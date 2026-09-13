using System;
using System.Collections.Generic;
using System.Diagnostics;
using RenderInfo       = FamiStudio.PianoRoll.RenderInfo;
using EditionMode      = FamiStudio.PianoRoll.EditionMode;
using NoteAttackState  = FamiStudio.PianoRoll.NoteAttackState;
using Gizmo            = FamiStudio.PianoRoll.Gizmo;
using GizmoAction      = FamiStudio.PianoRoll.GizmoAction;
using ScaleType        = FamiStudio.PianoRoll.ScaleType;
using CaptureOperation = FamiStudio.PianoRoll.CaptureOperation;

namespace FamiStudio
{
    public class NoteArea : Control
    {
        private PianoRoll pianoRoll;

        private EditionMode editMode                       => pianoRoll.EditMode;
        private Song Song                                   => pianoRoll.Song;
        private int virtualSizeY                            => pianoRoll.VirtualSizeY;
        private int octaveSizeY                             => pianoRoll.OctaveSizeY;
        private int scrollY                                 => pianoRoll.ViewScrollY;
        private int noteSizeY                               => pianoRoll.NoteSizeY;
        private float noteSizeX                             => pianoRoll.NoteSizeX;
        private int pianoSizeX                              => pianoRoll.PianoSizeX;
        private int headerAndEffectSizeY                    => pianoRoll.HeaderAndEffectSizeY;
        private int editChannel                             => pianoRoll.EditChannel;
        private int selectedEffectIdx                       => pianoRoll.SelectedEffectIdx;
        private bool showEffectsPanel                       => pianoRoll.ShowEffectsPanel;
        private int effectIconPosY                          => pianoRoll.EffectIconPosY;
        private float effectBitmapScale                     => pianoRoll.EffectBitmapScale;
        private TextureAtlasRef[] bmpEffects                => pianoRoll.BmpEffects;
        private TextureAtlasRef bmpEffectFrame              => pianoRoll.BmpEffectFrame;
        private int bigTextPosX                             => pianoRoll.BigTextPosX;
        private int bigTextPosY                             => pianoRoll.BigTextPosY;
        private int tooltipTextPosX                         => pianoRoll.TooltipTextPosX;
        private int tooltipTextPosY                         => pianoRoll.TooltipTextPosY;
        private int dpcmTextPosX                            => pianoRoll.DpcmTextPosX;
        private int mouseLastX                              => pianoRoll.MouseLastX;
        private int mouseLastY                              => pianoRoll.MouseLastY;
        private float[] mobileEraseGeometry                 => pianoRoll.MobileEraseGeometry;
        private int highlightDPCMSample                     => pianoRoll.HighlightDPCMSample;
        private int highlightNoteAbsIndex
        {
            get => pianoRoll.HighlightNoteAbsoluteIndex;
            set => pianoRoll.SetHighlightNoteAbsIndex(value);
        }
        private Instrument editInstrument                   => pianoRoll.EditInstrument;
        private DPCMSampleMapping draggedSample             => pianoRoll.DraggedSample;
        private long videoForceDisplayChannelMask           => pianoRoll.VideoForceDisplayChannelMask;
        private int[] videoChannelTranspose                 => pianoRoll.VideoChannelTranspose;
        private int releaseNoteSizeY                        => pianoRoll.ReleaseNoteSizeY;
        private float[] slideNoteGeometry                   => pianoRoll.SlideNoteGeometry;
        private int attackIconPosX                          => pianoRoll.AttackIconPosX;
        private int noteAttackSizeX                         => pianoRoll.NoteAttackSizeX;
        private Color attackColor                           => pianoRoll.AttackColor;
        private Color attackBrushForceDisplayColor          => pianoRoll.AttackBrushForceDisplayColor;
        private float[][] stopNoteGeometry                  => pianoRoll.StopNoteGeometry;
        private float[][] stopReleaseNoteGeometry           => pianoRoll.StopReleaseNoteGeometry;
        private float[][] releaseNoteGeometry               => pianoRoll.ReleaseNoteGeometry;
        private int fontSmallCharSizeX                      => pianoRoll.FontSmallCharSizeX;
        private bool legacySelectMode                       => pianoRoll.LegacySelectMode;
        private Color selectionHighlightColor               => pianoRoll.SelectionHighlightColor;
        private int minPixelDistForLines                    => pianoRoll.MinPixelDistForLines;
        private int rootNoteIdx                             => pianoRoll.RootNoteIdx;
        private int scaleTypeIndex                          => pianoRoll.ScaleTypeIndex;
        private string noteTooltip                          => pianoRoll.NoteTooltip;

        private int captureMouseAbsoluteIdx                 => pianoRoll.CaptureMouseAbsoluteIdx;
        private int captureNoteAbsoluteIdx                  => pianoRoll.CaptureNoteAbsoluteIdx;
        private NoteLocation captureNoteLocation            => pianoRoll.CaptureNoteLocation;
        private int captureNoteValue                        => pianoRoll.CaptureNoteValue;
        private int captureSelectionMinY                    => pianoRoll.CaptureSelectionMinY;
        private int captureSelectionMaxY                    => pianoRoll.CaptureSelectionMaxY;
        private bool captureThresholdMet                    => pianoRoll.CaptureThresholdMet;
        private CaptureOperation captureOperation            => pianoRoll.ActiveCaptureOperation;

        private bool IsOwnedCapture =>
            captureOperation == CaptureOperation.CreateNote ||
            captureOperation == CaptureOperation.DragNote ||
            captureOperation == CaptureOperation.DragSelection ||
            captureOperation == CaptureOperation.ResizeNoteStart ||
            captureOperation == CaptureOperation.ResizeSelectionNoteStart ||
            captureOperation == CaptureOperation.ResizeNoteEnd ||
            captureOperation == CaptureOperation.ResizeSelectionNoteEnd ||
            captureOperation == CaptureOperation.MoveNoteRelease ||
            captureOperation == CaptureOperation.MoveSelectionNoteRelease ||
            pianoRoll.IsDeleteNotesCapture ||
            captureOperation == CaptureOperation.CreateSlideNote ||
            captureOperation == CaptureOperation.DragSlideNoteTarget ||
            captureOperation == CaptureOperation.DragSlideNoteTargetGizmo;

        private int selectionMinX { get => pianoRoll.SelectionMinXField; set => pianoRoll.SelectionMinXField = value; }
        private int selectionMaxX { get => pianoRoll.SelectionMaxXField; set => pianoRoll.SelectionMaxXField = value; }
        private int selectionMinY { get => pianoRoll.SelectionMinYField; set => pianoRoll.SelectionMinYField = value; }
        private int selectionMaxY { get => pianoRoll.SelectionMaxYField; set => pianoRoll.SelectionMaxYField = value; }

        private HashSet<int> selectedNoteIndices            => pianoRoll.SelectedNoteIndicesSet;
        private HashSet<int> selectedEffectIndices          => pianoRoll.SelectedEffectIndicesSet;

        private SortedList<int, Note> dragNotes { get => pianoRoll.DragNotes; set => pianoRoll.DragNotes = value; }
        private SortedList<int, int> dragEffects            => pianoRoll.DragEffects;
        private int dragFrameMin { get => pianoRoll.DragFrameMin; set => pianoRoll.DragFrameMin = value; }
        private int dragFrameMax { get => pianoRoll.DragFrameMax; set => pianoRoll.DragFrameMax = value; }
        private int dragLastNoteValue { get => pianoRoll.DragLastNoteValue; set => pianoRoll.DragLastNoteValue = value; }

        LocalizedString EditingChannelLabel;
        LocalizedString EditingInstrumentDPCMLabel;
        LocalizedString DPCMInstrumentUsageLabel;
        LocalizedString DPCMBankUsageLabel;
        LocalizedString PitchLabel;
        LocalizedString LoopingLabel;
        LocalizedString DMCInitialValueLabel;

        LocalizedString ArpeggioTooltip;
        LocalizedString ResizeNotesTooltip;
        LocalizedString MoveReleasePointTooltip;
        LocalizedString MoveNotesTooltip;
        LocalizedString CreateNoteTooltip;
        LocalizedString SetReleasePointTooltip;
        LocalizedString SlideNoteTooltip;
        LocalizedString ToggleAttackTooltip;
        LocalizedString InstrumentEyedropTooltip;
        LocalizedString SetNoteInstrumentTooltip;
        LocalizedString OrTooltip;
        LocalizedString DeleteNoteTooltip;
        LocalizedString AddStopNoteTooltip;
        LocalizedString PanTooltip;

        public NoteArea(PianoRoll pianoRoll)
        {
            this.pianoRoll = pianoRoll;
            Localization.Localize(this);
            supportsDoubleClick = true;
            supportsLongPress   = true;
        }

        public void UpdateLayout()
        {
            Move(pianoRoll.PianoSizeX, pianoRoll.HeaderAndEffectSizeY, pianoRoll.Width - pianoRoll.PianoSizeX - pianoRoll.ScrollBarThickness, pianoRoll.Height - pianoRoll.HeaderAndEffectSizeY - pianoRoll.ScrollBarThickness);
        }


        internal bool StartNoteCreation(int x, int y, NoteLocation location, byte noteValue, bool capturePointer = true)
        {
            var channel = Song.Channels[editChannel];

            if (channel.SupportsInstrument(App.SelectedInstrument, false))
            {
                // We should clear any selection when making a new note.
                if (!legacySelectMode)
                    ClearSelection();

                App.PlayInstrumentNote(noteValue, false, false);
                StartCaptureOperation(x, y, CaptureOperation.CreateNote, true, capturePointer: capturePointer);
                UpdateNoteCreation(x, y, true, false);
                return true;
            }
            else
            {
                App.ShowInstrumentError(channel, true);
                return false;
            }
        }

        internal void UpdateNoteCreation(int x, int y, bool first, bool last)
        {
            ScrollIfNearEdge(x, y);
            GetLocationForCoord(x, y, out var location, out _);

            if (!first)
            {
                // Need to cancel the transaction every time since the start pattern may change.
                App.UndoRedoManager.RestoreTransaction(false);
                App.UndoRedoManager.AbortTransaction();
            }

            var minLocation = SnapNote(NoteLocation.Min(location, captureNoteLocation), false);
            var maxLocation = SnapNote(NoteLocation.Max(location, captureNoteLocation), true);

            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[minLocation.PatternIndex];
            var minAbsoluteNoteIndex = minLocation.ToAbsoluteNoteIndex(Song);
            var maxAbsoluteNoteIndex = maxLocation.ToAbsoluteNoteIndex(Song);

            highlightNoteAbsIndex = minAbsoluteNoteIndex;

            if (pattern == null)
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
                pattern = channel.CreatePatternAndInstance(minLocation.PatternIndex);
            }
            else
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
            }

            var note = pattern.GetOrCreateNoteAt(minLocation.NoteIndex);

            note.Value = (byte)captureNoteValue;
            note.Instrument = App.SelectedInstrument;
            note.Arpeggio = Song.Channels[editChannel].SupportsArpeggios ? App.SelectedArpeggio : null;
            note.Duration = Math.Max(1, maxAbsoluteNoteIndex) - minAbsoluteNoteIndex;

            if (last)
            {
                MarkPatternDirty(pattern);
                App.StopOrReleaseIntrumentNote();
                App.UndoRedoManager.EndTransaction();
            }
        }

        internal void StartNoteDrag(int x, int y, CaptureOperation captureOp, NoteLocation location, Note note, bool capturePointer = true)
        {
            var dragSelection = 
                captureOp == CaptureOperation.DragSelection || 
                captureOp == CaptureOperation.ResizeSelectionNoteStart;

            var minPatternIdx = Song.PatternIndexFromAbsoluteNoteIndex(selectionMinX);
            var maxPatternIdx = Song.PatternIndexFromAbsoluteNoteIndex(selectionMaxX);
            var multiplePatterns = dragSelection && minPatternIdx != maxPatternIdx;

            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[location.PatternIndex];

            if (multiplePatterns)
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);

            StartCaptureOperation(x, y, captureOp, true, location.ToAbsoluteNoteIndex(Song), capturePointer: capturePointer);

            if (dragSelection)
            {
                dragNotes = GetSparseSelectedNotes(selectionMinX);

                // Sometimes notes are longer than they appear, having been interrupted by a subequent one.
                // We don't want notes having longer tails than what is visual during a drag, it feels janky.
                if (!legacySelectMode)
                {
                    foreach (var kv in dragNotes)
                    {
                        var dragNote = kv.Value;
                        if (dragNote != null && dragNote.IsMusical)
                        {
                            var sourceLocation = NoteLocation.FromAbsoluteNoteIndex(Song, kv.Key);
                            var visualDuration = GetVisualNoteDuration(sourceLocation, dragNote);

                            dragNote.Duration = (ushort)Math.Max(1, visualDuration);

                            if (dragNote.HasRelease && dragNote.Release >= dragNote.Duration)
                            {
                                dragNote.Release =  Math.Max(0, dragNote.Duration - 1);
                            }
                        }
                    }
                }

                dragFrameMin = selectionMinX;
                dragFrameMax = selectionMaxX;

                dragEffects.Clear();

                if (!legacySelectMode && selectedEffectIdx >= 0)
                {
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
            }
            else
            {
                var absPrevNoteIdx = location.ToAbsoluteNoteIndex(Song);

                dragFrameMin = absPrevNoteIdx;
                dragFrameMax = absPrevNoteIdx;

                dragNotes.Clear();
                dragNotes[absPrevNoteIdx] = note.Clone();
            }

            dragLastNoteValue = -1;

            if (captureThresholdMet)
                UpdateNoteDrag(x, y, false);
        }

        internal void UpdateNoteDrag(int x, int y, bool final)
        {
            Debug.Assert(
                App.UndoRedoManager.HasTransactionInProgress && (
                    App.UndoRedoManager.UndoScope == TransactionScope.Pattern ||
                    App.UndoRedoManager.UndoScope == TransactionScope.Channel));

            var channel = Song.Channels[editChannel];

            App.UndoRedoManager.RestoreTransaction(false);

            ScrollIfNearEdge(x, y, true, true);
            GetLocationForCoord(x, y, out var location, out var noteValue);

            //var deltaPosX = x - captureMouseX;

            var currentMouseAbsoluteIdx = location.ToAbsoluteNoteIndex(Song);
            var deltaAbsoluteIdx = currentMouseAbsoluteIdx - captureMouseAbsoluteIdx;
            var resizeStart = captureOperation == CaptureOperation.ResizeNoteStart || captureOperation == CaptureOperation.ResizeSelectionNoteStart;
            var resizeNote = channel.GetNoteAt(captureNoteLocation);
            var deltaNoteIdx = 0;

            if (!resizeStart)
            {
                var newCaptureNoteAbsNoteIndex = captureNoteAbsoluteIdx + currentMouseAbsoluteIdx - captureMouseAbsoluteIdx;
                var deltaNoteIdxSnapRoundDown  = SnapNote(newCaptureNoteAbsNoteIndex, false) - captureNoteAbsoluteIdx;
                var deltaNoteIdxSnapRoundUp    = SnapNote(newCaptureNoteAbsNoteIndex, true)  - captureNoteAbsoluteIdx;

                if (deltaAbsoluteIdx < 0)
                    deltaNoteIdx = Math.Max(deltaNoteIdxSnapRoundDown, deltaNoteIdxSnapRoundUp);
                else
                    deltaNoteIdx = Math.Min(deltaNoteIdxSnapRoundDown, deltaNoteIdxSnapRoundUp);
            }
            else 
            {
                // The snapped position of the mouse is the new note start.
                var snappedLocation = NoteLocation.Min(SnapNote(captureNoteLocation.Advance(Song, resizeNote.Duration - 1)), SnapNote(location));
                deltaNoteIdx = snappedLocation.ToAbsoluteNoteIndex(Song) - captureNoteAbsoluteIdx;
            }

            // Don't allow snapping to move stuff in the opposite side of the mouse movement. Feels janky.
            if (Math.Sign(deltaAbsoluteIdx) != Math.Sign(deltaNoteIdx))
            {
                deltaNoteIdx = 0;
            }

            var deltaDuration = resizeStart ? -deltaNoteIdx : 0;
            var deltaNoteValue = resizeStart ? 0 : noteValue - captureNoteValue;
            var newDragFrameMin = dragFrameMin + deltaNoteIdx;
            var newDragFrameMax = dragFrameMax + deltaNoteIdx;

            if (final && deltaNoteIdx == 0 && deltaNoteValue == 0)
            {
                App.UndoRedoManager.AbortTransaction();
            }
            else
            {
                highlightNoteAbsIndex = captureNoteLocation.Advance(Song, deltaNoteIdx).ToAbsoluteNoteIndex(Song);

                // When we cross pattern boundaries, we will have to promote the current transaction
                // from pattern to channel.
                if (App.UndoRedoManager.UndoScope == TransactionScope.Pattern)
                {
                    var initialPatternMinIdx = Song.PatternIndexFromAbsoluteNoteIndex(dragFrameMin);
                    var initialPatternMaxIdx = Song.PatternIndexFromAbsoluteNoteIndex(dragFrameMax);
                    Debug.Assert(initialPatternMinIdx == initialPatternMaxIdx);

                    var newPatternMinIdx = Song.PatternIndexFromAbsoluteNoteIndex(newDragFrameMin);
                    var newPatternMaxIdx = Song.PatternIndexFromAbsoluteNoteIndex(newDragFrameMax);

                    bool multiplePatterns = newPatternMinIdx != initialPatternMinIdx ||
                                            newPatternMaxIdx != initialPatternMinIdx;

                    if (multiplePatterns)
                        PromoteTransaction(TransactionScope.Channel, Song.Id, editChannel);
                }

                var copy = ModifierKeys.IsControlDown;
                var modernDrag = captureOperation == CaptureOperation.DragSelection && !legacySelectMode;
                var keepFx     = captureOperation != CaptureOperation.DragSelection || modernDrag;

                // If not copying, delete original notes.
                if (!copy)
                {
                    // For modern selection mode, we need to move any selected effects.
                    if (modernDrag)
                    {
                        foreach (var kv in dragEffects)
                        {
                            var oldLocation = NoteLocation.FromAbsoluteNoteIndex(Song, kv.Key);
                            var pattern = channel.PatternInstances[oldLocation.PatternIndex];

                            if (pattern == null)
                                continue;

                            var oldNote = channel.GetNoteAt(oldLocation);
                            if (oldNote == null)
                                continue;

                            oldNote.ClearEffectValue(selectedEffectIdx);

                            if (oldNote.IsEmpty)
                                pattern.DeleteNotesBetween(oldLocation.NoteIndex, oldLocation.NoteIndex + 1);
                        }
                    }

                    foreach (var kv in dragNotes)
                    {
                        channel.DeleteNotesBetween(kv.Key, kv.Key + 1, keepFx);
                    }
                }

                // Clear where the new notes are going to be.
                if (legacySelectMode)
                {
                    channel.DeleteNotesBetween(newDragFrameMin, newDragFrameMax + 1, keepFx);
                }
                else
                {
                    // Only clear areas where a new note lands for modern selection.
                    foreach (var kv in dragNotes)
                    {
                        var frame = kv.Key + deltaNoteIdx;
                        if (frame < 0 || frame >= Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                            continue;

                        var dragNote = kv.Value;
                        var duration = 1;

                        if (dragNote != null && dragNote.IsMusical)
                        {
                            var sourceLocation = NoteLocation.FromAbsoluteNoteIndex(Song, kv.Key);
                            duration = GetVisualNoteDuration(sourceLocation, dragNote);
                        }

                        var endFrame = Math.Min(frame + duration, Song.GetPatternStartAbsoluteNoteIndex(Song.Length));
                        channel.DeleteNotesBetween(frame, endFrame, keepFx);
                    }
                }

                foreach (var kv in dragNotes)
                {
                    var frame = kv.Key + deltaNoteIdx;

                    if (frame < 0 || frame >= Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                        continue;

                    var newLocation = NoteLocation.FromAbsoluteNoteIndex(Song, frame);
                    var pattern = channel.PatternInstances[newLocation.PatternIndex];

                    if (pattern == null)
                    {
                        Debug.Assert(App.UndoRedoManager.UndoScope == TransactionScope.Channel);
                        pattern = channel.CreatePatternAndInstance(newLocation.PatternIndex);
                    }

                    if (keepFx)
                    {
                        var oldNote = kv.Value;

                        if (oldNote.IsMusical)
                        {
                            var newNote = pattern.GetOrCreateNoteAt(newLocation.NoteIndex);

                            newNote.Value = (byte)Utils.Clamp(oldNote.Value + deltaNoteValue, Note.MusicalNoteMin, Note.MusicalNoteMax);
                            newNote.Instrument = oldNote.Instrument;
                            newNote.Arpeggio = oldNote.Arpeggio;
                            newNote.SlideNoteTarget = (byte)(oldNote.IsSlideNote ? Utils.Clamp(oldNote.SlideNoteTarget + deltaNoteValue, Note.MusicalNoteMin, Note.MusicalNoteMax) : 0);
                            newNote.Flags = oldNote.Flags;
                            newNote.Duration = (ushort)Math.Max(1, oldNote.Duration + deltaDuration);
                            newNote.Release = oldNote.Release;

                            if (oldNote.HasRelease && !newNote.HasRelease && newNote.Duration > 1)
                                newNote.Release = newNote.Duration - 1;
                        }
                        else if (oldNote.IsStop)
                        {
                            var newNote = pattern.GetOrCreateNoteAt(newLocation.NoteIndex);
                            newNote.Value = Note.NoteStop;
                            newNote.Duration = 1;
                        }
                    }
                    else
                    {
                        var movedNote = kv.Value.Clone();
                        if (movedNote.IsMusical)
                        {
                            movedNote.Value = (byte)Utils.Clamp(movedNote.Value + deltaNoteValue, Note.MusicalNoteMin, Note.MusicalNoteMax);
                            movedNote.SlideNoteTarget = (byte)(movedNote.IsSlideNote ? Utils.Clamp(movedNote.SlideNoteTarget + deltaNoteValue, Note.MusicalNoteMin, Note.MusicalNoteMax) : 0);
                        }

                        pattern.SetNoteAt(newLocation.NoteIndex, movedNote);
                    }
                }

                // Move effects if any are selected.
                if (captureOperation == CaptureOperation.DragSelection && !legacySelectMode)
                {
                    foreach (var kv in dragEffects)
                    {
                        var frame = kv.Key + deltaNoteIdx;

                        if (frame < 0 || frame >= Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                            continue;
                        
                        var newLocation = NoteLocation.FromAbsoluteNoteIndex(Song, frame);
                        var pattern = channel.PatternInstances[newLocation.PatternIndex];

                        if (pattern == null)
                        {
                            if (App.UndoRedoManager.UndoScope == TransactionScope.Pattern)
                            {
                                PromoteTransaction(
                                    TransactionScope.Channel,
                                    Song.Id,
                                    editChannel);
                            }

                            pattern = channel.CreatePatternAndInstance(newLocation.PatternIndex);
                        }

                        var effectNote = pattern.GetOrCreateNoteAt(newLocation.NoteIndex);

                        effectNote.SetEffectValue(selectedEffectIdx, kv.Value);
                    }
                }

                if (captureOperation == CaptureOperation.DragSelection || captureOperation == CaptureOperation.ResizeSelectionNoteStart)
                {
                    selectionMinX = Utils.Clamp(newDragFrameMin, 0, Song.GetPatternStartAbsoluteNoteIndex(Song.Length) - 1);
                    selectionMaxX = Utils.Clamp(newDragFrameMax, 0, Song.GetPatternStartAbsoluteNoteIndex(Song.Length) - 1);
                    selectionMinY = Utils.Clamp(captureSelectionMinY + deltaNoteValue, Note.MusicalNoteMin, Note.MusicalNoteMax);
                    selectionMaxY = Utils.Clamp(captureSelectionMaxY + deltaNoteValue, Note.MusicalNoteMin, Note.MusicalNoteMax);

                    if (!legacySelectMode)
                    {
                        selectedNoteIndices.Clear();

                        foreach (var kv in dragNotes)
                        {
                            var absoluteIdx = kv.Key + deltaNoteIdx;
                            if (absoluteIdx >= 0 && absoluteIdx < Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                            {
                                selectedNoteIndices.Add(absoluteIdx - selectionMinX);
                            }
                        }

                        selectedEffectIndices.Clear();

                        if (captureOperation == CaptureOperation.DragSelection)
                        {
                            foreach (var kv in dragEffects)
                            {
                                var absoluteIdx = kv.Key + deltaNoteIdx;
                                if (absoluteIdx >= 0 && absoluteIdx < Song.GetPatternStartAbsoluteNoteIndex(Song.Length))
                                {
                                    selectedEffectIndices.Add(absoluteIdx);
                                }
                            }
                        }
                    }
                }

                if (dragLastNoteValue != noteValue && (captureOperation == CaptureOperation.DragNote))
                {
                    var dragNote = (Note)null;
                    foreach (var n in dragNotes)
                    {
                        dragNote = n.Value;
                        break;
                    }

                    // Itch.io request.
                    bool disableDragSound = Settings.NoDragSoungWhenPlaying && App.IsPlaying;

                    // No sound feedback on stop/release notes.
                    if (dragNote != null && dragNote.IsMusical && !disableDragSound)
                    {
                        // If we are adding a new note or if the threshold has not been met, we need
                        // to play the selected note from the project explorer, otherwise we need to 
                        // play the instrument from the selected note.
                        if (!captureThresholdMet)
                        {
                            App.PlayInstrumentNote(noteValue, false, false);
                        }
                        else
                        {
                            foreach (var n in dragNotes)
                            {
                                App.PlayInstrumentNote(noteValue, false, false, true, n.Value.Instrument, n.Value.Arpeggio);
                                break;
                            }
                        }
                    }

                    dragLastNoteValue = noteValue;
                }
            }

            if (final)
            {
                int p0, p1;

                p0 = Song.PatternIndexFromAbsoluteNoteIndex(dragFrameMin + 0);
                p1 = Song.PatternIndexFromAbsoluteNoteIndex(dragFrameMax + 1);
                for (int p = p0; p <= p1 && p < Song.Length; p++)
                    pianoRoll.RaisePatternChanged(channel.PatternInstances[p]);
                channel.InvalidateCumulativePatternCache(p0, p1);
                p0 = Song.PatternIndexFromAbsoluteNoteIndex(dragFrameMin + deltaNoteIdx + 0);
                p1 = Song.PatternIndexFromAbsoluteNoteIndex(dragFrameMax + deltaNoteIdx + 1);
                for (int p = p0; p <= p1 && p < Song.Length; p++)
                    pianoRoll.RaisePatternChanged(channel.PatternInstances[p]);
                channel.InvalidateCumulativePatternCache(p0, p1);

                App.Project.ValidateIntegrity();
                if (App.UndoRedoManager.HasTransactionInProgress)
                    App.UndoRedoManager.EndTransaction();
                App.StopInstrument();
            }

            MarkDirty();
        }

        internal void StartNoteResizeEnd(int x, int y, CaptureOperation captureOp, NoteLocation location, int offsetX = 0, bool capturePointer = true)
        {
            var pattern = Song.Channels[editChannel].PatternInstances[location.PatternIndex];
            var dragSelection = captureOp == CaptureOperation.ResizeSelectionNoteEnd;
            var multiplePatterns = dragSelection &&
                                   Song.PatternIndexFromAbsoluteNoteIndex(selectionMinX) !=
                                   Song.PatternIndexFromAbsoluteNoteIndex(selectionMaxX);

            if (multiplePatterns)
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
            else
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);

            StartCaptureOperation(x, y, captureOp, true, location.ToAbsoluteNoteIndex(Song), offsetX, capturePointer: capturePointer);
        }

        internal void UpdateNoteResizeEnd(int x, int y, bool final)
        {
            var channel = Song.Channels[editChannel];

            // HACK : When tapping quickly on a resize gizmo, ignore the 
            // change since we are likely trying to draw a note after.
            if (Platform.IsMobile && !captureThresholdMet)
            {
                App.UndoRedoManager.AbortTransaction();
                return;
            }

            App.UndoRedoManager.RestoreTransaction(false);

            var selection = captureOperation == CaptureOperation.ResizeSelectionNoteEnd;
            var min = selection ? selectionMinX : captureNoteAbsoluteIdx;
            var max = selection ? selectionMaxX : captureNoteAbsoluteIdx;

            // Since we may be be dragging from the "visual" duration which may be shorter than
            // the real duration, we truncate them right away.
            TransformNotes(min, max, false, final, false, (note, idx) =>
            {
                if (note != null && note.IsMusical)
                {
                    var distToNext = channel.GetDistanceToNextNote(NoteLocation.FromAbsoluteNoteIndex(Song, min + idx));
                    if (distToNext >= 0)
                        note.Duration = (ushort)Utils.Clamp(note.Duration, 1, distToNext);
                }

                return note;
            });

            ScrollIfNearEdge(x, y);
            GetLocationForCoord(x, y, out var location, out var noteValue);

            // We are resizing the highlighted note, but apply the delta to all other notes.
            var resizeNote = channel.GetNoteAt(captureNoteLocation);
            var resizeNoteEnd = captureNoteAbsoluteIdx + resizeNote.Duration;
            var snappedLocation = NoteLocation.Max(SnapNote(location, Platform.IsDesktop), SnapNote(captureNoteLocation, true));
            var deltaNoteIdx = snappedLocation.ToAbsoluteNoteIndex(Song) - resizeNoteEnd;
            var processedNotes = new HashSet<Note>();

            TransformNotes(min, max, false, final, false, (note, idx) =>
            {
                var selected = !selection || legacySelectMode || selectedNoteIndices.Contains(idx);
                if (selected && note != null)
                {
                    if (note.IsMusical && !processedNotes.Contains(note))
                    {
                        // HACK : Try to preserve releases.
                        var hadRelease = note.HasRelease;
                        note.Duration = (ushort)Math.Max(1, note.Duration + deltaNoteIdx);
                        if (hadRelease && !note.HasRelease && note.Duration > 1)
                            note.Release = note.Duration - 1;
                    }
                }

                processedNotes.Add(note);
                return note;
            });

            if (final)
            {
                App.UndoRedoManager.EndTransaction();
            }
        }

        internal void StartMoveNoteRelease(int x, int y, CaptureOperation op, NoteLocation location, bool capturePointer = true)
        {
            var minPatternIdx = Song.PatternIndexFromAbsoluteNoteIndex(selectionMinX);
            var maxPatternIdx = Song.PatternIndexFromAbsoluteNoteIndex(selectionMaxX);
            var pattern = Song.Channels[editChannel].PatternInstances[location.PatternIndex];

            if (minPatternIdx != maxPatternIdx)
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Channel, Song.Id, editChannel);
            }
            else
            {
                App.UndoRedoManager.BeginTransaction(TransactionScope.Pattern, pattern.Id);
            }

            StartCaptureOperation(x, y, op, false, location.ToAbsoluteNoteIndex(Song), capturePointer: capturePointer);
        }

        internal void UpdateMoveNoteRelease(int x, int y, bool final)
        {
            GetLocationForCoord(x, y, out var location, out var noteValue, true);

            var selection = captureOperation == CaptureOperation.MoveSelectionNoteRelease;

            if (selection)
            {
                App.UndoRedoManager.RestoreTransaction(false);
            }

            var channel = Song.Channels[editChannel];
            var pattern = channel.PatternInstances[captureNoteLocation.PatternIndex];

            // Move the release for the highlighted note.
            var highlightedNote = pattern.Notes[captureNoteLocation.NoteIndex];
            var newRelease = Song.CountNotesBetween(captureNoteLocation, location);
            var delta = newRelease - highlightedNote.Release;
            highlightedNote.Release = (ushort)Utils.Clamp(newRelease, 1, highlightedNote.Duration - 1);
            channel.InvalidateCumulativePatternCache(pattern);

            // Then apply same delta to every other selected note.
            if (selection)
            { 
                var min = selection ? selectionMinX : captureNoteAbsoluteIdx;
                var max = selection ? selectionMaxX : captureNoteAbsoluteIdx;
                var processedNotes = new HashSet<Note>();

                TransformNotes(min, max, false, final, false, (note, idx) =>
                {
                    if (note != null && note != highlightedNote && note.IsMusical && note.HasRelease && !processedNotes.Contains(note))
                        note.Release = Utils.Clamp(note.Release + delta, 1, note.Duration - 1);

                    processedNotes.Add(note);
                    return note;
                });
            }

            if (final)
            {
                App.UndoRedoManager.EndTransaction();
            }

            MarkDirty();
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            base.OnPointerDown(e);

            if (!pianoRoll.IsNoCapture && (e.Left || e.Right))
                return;

            UpdateCursor();

            var middle = e.Middle || (e.Left && ModifierKeys.IsAltDown && Settings.AltLeftForMiddle);
            if (middle)
            {
                var panPos = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.StartPan(panPos.X, panPos.Y);
                return;
            }

            // Touch handling here is scoped to note gizmos only for now (resize/move-release/slide).
            // Touch-drag of a note's body, double-tap-delete and long-press are still owned by PianoRoll.
            if (e.IsTouchEvent)
            {
                if (editMode == EditionMode.Channel)
                {
                    var touchPos = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                    if (HandleNoteGizmos(touchPos.X, touchPos.Y))
                        MarkDirty();
                }

                return;
            }

            if (editMode == EditionMode.DPCMMapping)
            {
                var dpcmPos = pianoRoll.WindowToControl(ControlToWindow(e.Position));

                if (e.Left && pianoRoll.GetLocationForCoord(dpcmPos.X, dpcmPos.Y, out _, out byte dpcmNoteValue))
                {
                    var mapping = editInstrument.GetDPCMMapping(dpcmNoteValue);

                    if (mapping == null)
                    {
                        pianoRoll.MapDPCMSample(dpcmNoteValue);
                    }
                    else if (ModifierKeys.IsShiftDown)
                    {
                        pianoRoll.ClearDPCMSampleMapping(dpcmNoteValue);
                    }
                    else
                    {
                        pianoRoll.StartDragDPCMSampleMapping(dpcmPos.X, dpcmPos.Y, dpcmNoteValue);
                    }

                    MarkDirty();
                }

                return;
            }

            if (editMode != EditionMode.Channel)
                return;

            var pos = pianoRoll.WindowToControl(ControlToWindow(e.Position));

            bool left  = e.Left;
            bool right = e.Right;

            if (left && HandleNoteGizmos(pos.X, pos.Y))
            {
                MarkDirty();
                return;
            }

            if (pianoRoll.GetLocationForCoord(pos.X, pos.Y, out var mouseLocation, out byte noteValue))
            {
                if (mouseLocation.PatternIndex >= Song.Length)
                    return;

                var channel = Song.Channels[editChannel];
                var noteLocation = mouseLocation;
                var note = channel.FindMusicalNoteAtLocation(ref noteLocation, noteValue);

                if (left)
                {
                    var delete  = ModifierKeys.IsShiftDown;
                    var release = Settings.ReleaseNoteShortcut.IsKeyDown(ParentWindow);
                    var stop    = Settings.StopNoteShortcut.IsKeyDown(ParentWindow);
                    var slide   = Settings.SlideNoteShortcut.IsKeyDown(ParentWindow);
                    var attack  = Settings.AttackShortcut.IsKeyDown(ParentWindow);
                    var eyedrop = Settings.EyeDropNoteShortcut.IsKeyDown(ParentWindow);
                    var setInst = Settings.SetNoteInstrumentShortcut.IsKeyDown(ParentWindow);

                    if (delete)
                    {
                        if (note != null)
                            pianoRoll.DeleteSingleNote(noteLocation, mouseLocation, note);
                        CapturePointer();
                        StartCaptureOperation(pos.X, pos.Y, CaptureOperation.DeleteNotes, capturePointer: false);
                    }
                    else if (slide)
                    {
                        CapturePointer();
                        if (!pianoRoll.StartSlideNoteCreation(pos.X, pos.Y, noteLocation, note, noteValue, false))
                            ReleasePointer();
                    }
                    else if (attack && note != null)
                    {
                        pianoRoll.ToggleNoteAttack(noteLocation, note);
                    }
                    else if (setInst && note != null)
                    {
                        pianoRoll.SetNoteInstrument(noteLocation, note, App.SelectedInstrument);
                    }
                    else if (eyedrop && note != null)
                    {
                        pianoRoll.Eyedrop(note);
                    }
                    else if (release && note != null)
                    {
                        pianoRoll.ToggleReleaseNote(noteLocation, mouseLocation, note);
                    }
                    else if (stop)
                    {
                        pianoRoll.CreateOrphanStopNote(mouseLocation);
                    }
                    else
                    {
                        if (note != null)
                        {
                            var captureOp = pianoRoll.GetHighlightedNoteCaptureOperationForCoord(pos.X, pos.Y);

                            if (captureOp == CaptureOperation.DragSelection ||
                                captureOp == CaptureOperation.DragNote ||
                                captureOp == CaptureOperation.ResizeNoteStart ||
                                captureOp == CaptureOperation.ResizeSelectionNoteStart)
                            {
                                CapturePointer();
                                StartNoteDrag(pos.X, pos.Y, captureOp, noteLocation, note, false);
                            }
                            else if (captureOp == CaptureOperation.ResizeNoteEnd ||
                                     captureOp == CaptureOperation.ResizeSelectionNoteEnd)
                            {
                                CapturePointer();
                                StartNoteResizeEnd(pos.X, pos.Y, captureOp, noteLocation, capturePointer: false);
                            }
                            else if (
                                captureOp == CaptureOperation.MoveNoteRelease ||
                                captureOp == CaptureOperation.MoveSelectionNoteRelease)
                            {
                                CapturePointer();
                                StartMoveNoteRelease(pos.X, pos.Y, captureOp, noteLocation, false);
                            }
                        }
                        else
                        {
                            CapturePointer();
                            if (!StartNoteCreation(pos.X, pos.Y, noteLocation, noteValue, false))
                                ReleasePointer();
                        }
                    }
                }
                else if (right && note == null)
                {
                    e.DelayRightClick(); // Need to wait to tell if its a context menu or selection.
                }

                MarkDirty();
            }
        }

        protected override void OnPointerDownDelayed(PointerEventArgs e)
        {
            base.OnPointerDownDelayed(e);

            if (e.Right && editMode == EditionMode.Channel)
            {
                CapturePointer();

                var pos = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.StartTimelineSelection(pos.X, pos.Y);
            }
        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            base.OnPointerUp(e);

            if (e.IsTouchEvent)
            {
                if (IsOwnedCapture)
                {
                    var touchPos = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                    pianoRoll.EndTimelineCapture(touchPos.X, touchPos.Y);
                }

                return;
            }

            if (editMode == EditionMode.DPCMMapping)
            {
                if (e.Right && pianoRoll.IsNoCapture)
                {
                    var dpcmPos = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                    if (pianoRoll.HandleContextMenuDPCMMapping(dpcmPos.X, dpcmPos.Y))
                        MarkDirty();
                }

                return;
            }

            if (editMode != EditionMode.Channel)
                return;

            if (e.Right && !pianoRoll.TimelineCaptureThresholdMet)
            {
                var pos = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                if (pianoRoll.HandleContextMenuChannelNote(pos.X, pos.Y))
                    MarkDirty();
            }

            if (pianoRoll.IsTimelineColumnSelectionCapture || IsOwnedCapture)
            {
                var pos = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.EndTimelineCapture(pos.X, pos.Y);
            }

            UpdateNoteTooltip(e);
        }

        protected override void OnTouchClick(PointerEventArgs e)
        {
            base.OnTouchClick(e);

            if (editMode != EditionMode.Channel)
                return;

            var pos = pianoRoll.WindowToControl(ControlToWindow(e.Position));

            if (HandleTouchClickChannelNote(pos.X, pos.Y))
                MarkDirty();
        }

        protected override void OnMouseDoubleClick(PointerEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (e.IsTouchEvent)
                return;

            if (editMode == EditionMode.DPCMMapping)
            {
                var dpcmPos = pianoRoll.WindowToControl(ControlToWindow(e.Position));

                if (pianoRoll.GetLocationForCoord(dpcmPos.X, dpcmPos.Y, out _, out byte noteValue))
                {
                    var mapping = editInstrument.GetDPCMMapping(noteValue);
                    if (mapping != null)
                        pianoRoll.ClearDPCMSampleMapping(noteValue);

                    MarkDirty();
                }

                return;
            }

            if (editMode != EditionMode.Channel)
                return;

            var pos = pianoRoll.WindowToControl(ControlToWindow(e.Position));

            if (e.Left && pianoRoll.GetLocationForCoord(pos.X, pos.Y, out var mouseLocation, out byte channelNoteValue) && mouseLocation.IsInSong(Song))
            {
                var channel = Song.Channels[editChannel];
                var noteLocation = mouseLocation;
                var note = channel.FindMusicalNoteAtLocation(ref noteLocation, channelNoteValue);

                if (note != null)
                {
                    pianoRoll.AbortCaptureOperation();
                    pianoRoll.DeleteSingleNote(noteLocation, mouseLocation, note);
                    CapturePointer();
                    StartCaptureOperation(pos.X, pos.Y, CaptureOperation.DeleteNotes, capturePointer: false);
                }

                MarkDirty();
            }
        }

        protected override void OnPointerMove(PointerEventArgs e)
        {
            base.OnPointerMove(e);

            UpdateCursor();
            UpdateHoverNote(e);

            if (pianoRoll.IsTimelineColumnSelectionCapture || IsOwnedCapture)
            {
                var pos = pianoRoll.WindowToControl(ControlToWindow(e.Position));
                pianoRoll.UpdateTimelineCapture(pos.X, pos.Y);
            }

            UpdateNoteTooltip(e);
        }

        private void UpdateHoverNote(PointerEventArgs e)
        {
            if (!Platform.IsDesktop || editMode != EditionMode.Channel)
                return;

            var pos = pianoRoll.WindowToControl(ControlToWindow(e.Position));

            pianoRoll.GetLocationForCoord(pos.X, pos.Y, out var location, out var noteValue, true);
            var newHoverNoteIndex = location.ToAbsoluteNoteIndex(Song);
            var newHoverNoteCount = 1;

            // This is super lame, advance until we find the next snapping boundary.
            // We cant just advance by (beat length) * (snap precision) because we have
            // a bunch of crazy rules in there.
            if (pianoRoll.SnapEnabled)
            {
                var newHoverNoteIndex2 = newHoverNoteIndex + 1;

                for (int i = 1; ; i++)
                {
                    var newAbsIndex = pianoRoll.SnapNote(newHoverNoteIndex + i);
                    if (newAbsIndex != newHoverNoteIndex)
                    {
                        newHoverNoteIndex2 = newAbsIndex;
                        break;
                    }
                }

                newHoverNoteCount = newHoverNoteIndex2 - newHoverNoteIndex;
            }

            pianoRoll.SetPianoHoverNote(noteValue, newHoverNoteIndex, newHoverNoteCount);
        }

        private void UpdateCursor()
        {
            if (editMode != EditionMode.Channel)
                return;

            if (ModifierKeys.IsControlDown && (pianoRoll.ActiveCaptureOperation == CaptureOperation.DragNote || pianoRoll.ActiveCaptureOperation == CaptureOperation.DragSelection))
            {
                Cursor = Cursors.CopyCursor;
            }
            else if (pianoRoll.IsDeleteNotesCapture)
            {
                Cursor = Cursors.Eraser;
            }
            else if (Settings.EyeDropNoteShortcut.IsKeyDown(ParentWindow))
            {
                Cursor = Cursors.Eyedrop;
            }
            else if (pianoRoll.IsNoCapture)
            {
                var localPt = ScreenToControl(CursorPosition);
                var gizmos = GetNoteGizmos(out _, out _);

                if (gizmos != null)
                {
                    foreach (var gizmo in gizmos)
                    {
                        if (gizmo.Rect.Contains(localPt.X, localPt.Y))
                        {
                            Cursor = Cursors.SizeNS;
                            return;
                        }
                    }
                }

                var pos = pianoRoll.ScreenToControl(CursorPosition);
                var captureOp = pianoRoll.GetHighlightedNoteCaptureOperationForCoord(pos.X, pos.Y);

                switch (captureOp)
                {
                    case CaptureOperation.ResizeNoteStart:
                    case CaptureOperation.ResizeSelectionNoteStart:
                    case CaptureOperation.ResizeNoteEnd:
                    case CaptureOperation.ResizeSelectionNoteEnd:
                    case CaptureOperation.MoveNoteRelease:
                    case CaptureOperation.MoveSelectionNoteRelease:
                        Cursor = Cursors.SizeWE;
                        break;
                    case CaptureOperation.DragNote:
                    case CaptureOperation.DragSelection:
                        Cursor = Cursors.Move;
                        break;
                    default:
                        Cursor = Cursors.Default;
                        break;
                }
            }
            else if (pianoRoll.ActiveCaptureOperation == CaptureOperation.DragNote || pianoRoll.ActiveCaptureOperation == CaptureOperation.DragSelection)
            {
                Cursor = Cursors.Move;
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        protected override void OnPointerEnter(EventArgs e)
        {
            base.OnPointerEnter(e);
            var localPt = ScreenToControl(CursorPosition);
            var pe = new PointerEventArgs(0, localPt.X, localPt.Y);
            UpdateHoverNote(pe);
            UpdateNoteTooltip(pe);
        }

        protected override void OnPointerLeave(EventArgs e)
        {
            base.OnPointerLeave(e);
            pianoRoll.ClearPianoHoverNote();
            App.SetToolTip("");
        }

        private void UpdateNoteTooltip(PointerEventArgs e)
        {
            if (editMode != EditionMode.Channel || !pianoRoll.IsNoCapture && !pianoRoll.IsSelectCapture)
                return;

            var pos = pianoRoll.WindowToControl(ControlToWindow(e.Position));

            var tooltip = "";
            var newNoteTooltip = "";

            if (pianoRoll.GetLocationForCoord(pos.X, pos.Y, out var location, out byte noteValue))
            {
                newNoteTooltip = $"{Note.GetFriendlyName(noteValue)} [{location.PatternIndex+1:D3} : {location.NoteIndex:D3}]";

                var channel = Song.Channels[editChannel];
                var note = channel.FindMusicalNoteAtLocation(ref location, noteValue);

                if (note != null)
                {
                    if (note.Instrument != null)
                        newNoteTooltip += $" ({note.Instrument.Name})";
                    if (note.IsArpeggio)
                        newNoteTooltip += $" ({ArpeggioTooltip}: {note.Arpeggio.Name})";
                }

                // Main click action.
                var captureOp = pianoRoll.GetHighlightedNoteCaptureOperationForCoord(pos.X, pos.Y);
                var tooltipList = new List<string>();

                switch (captureOp)
                {
                    case CaptureOperation.ResizeNoteStart:
                    case CaptureOperation.ResizeSelectionNoteStart:
                    case CaptureOperation.ResizeNoteEnd:
                    case CaptureOperation.ResizeSelectionNoteEnd:
                        tooltipList.Add($"<MouseLeft><Drag> {ResizeNotesTooltip}");
                        break;
                    case CaptureOperation.MoveNoteRelease:
                        tooltipList.Add($"<MouseLeft><Drag> {MoveReleasePointTooltip}");
                        break;
                    case CaptureOperation.DragNote:
                    case CaptureOperation.DragSelection:
                        tooltipList.Add($"<MouseLeft><Drag> {MoveNotesTooltip}");
                        break;
                    default:
                        tooltipList.Add($"<MouseLeft><Drag> {CreateNoteTooltip}");
                        break;
                }

                if (note != null)
                {
                    if (channel.SupportsReleaseNotes && captureOp != CaptureOperation.MoveNoteRelease && Settings.ReleaseNoteShortcut.IsShortcutValid(0))
                        tooltipList.Add($"{Settings.ReleaseNoteShortcut.TooltipString}<MouseLeft> {SetReleasePointTooltip}");
                    if (channel.SupportsSlideNotes && Settings.SlideNoteShortcut.IsShortcutValid(0))
                        tooltipList.Add($"{Settings.SlideNoteShortcut.TooltipString}<MouseLeft><Drag> {SlideNoteTooltip}");
                    if (note.IsMusical)
                    {
                        if (Settings.AttackShortcut.IsShortcutValid(0))
                            tooltipList.Add($"{Settings.AttackShortcut.TooltipString}<MouseLeft> {ToggleAttackTooltip}");
                        if (Settings.EyeDropNoteShortcut.IsShortcutValid(0))
                            tooltipList.Add($"{Settings.EyeDropNoteShortcut.TooltipString}<MouseLeft> {InstrumentEyedropTooltip}");
                        if (Settings.SetNoteInstrumentShortcut.IsShortcutValid(0))
                            tooltipList.Add($"{Settings.SetNoteInstrumentShortcut.TooltipString}<MouseLeft> {SetNoteInstrumentTooltip}");
                    }
                    tooltipList.Add($"<MouseLeft><MouseLeft> {OrTooltip} <Shift><MouseLeft> {DeleteNoteTooltip}");
                }
                else
                {
                    if (channel.SupportsStopNotes && Settings.StopNoteShortcut.IsShortcutValid(0))
                        tooltipList.Add($"{Settings.StopNoteShortcut.TooltipString}<MouseLeft> {AddStopNoteTooltip}");
                }

                tooltipList.Add($"<MouseWheel> {PanTooltip}");

                if (tooltipList.Count >= 3)
                {
                    var array = tooltipList.ToArray();
                    var numFirstLine = array.Length / 2;
                    tooltip = string.Join(" - ", array, 0, numFirstLine) + "\n" + string.Join(" - ", array, numFirstLine, array.Length - numFirstLine);
                }
                else
                {
                    tooltip = string.Join(" - ", tooltipList);
                }
            }

            // We only display frames in modern select mode during selection, for means of measurement.
            if (pianoRoll.LegacySelectMode ? pianoRoll.IsSelectionValid() : pianoRoll.IsSelectCapture)
            {
                if (newNoteTooltip.Length > 0)
                    newNoteTooltip += " ";

                var numSelected = pianoRoll.LegacySelectMode ? pianoRoll.SelectionMaxX - pianoRoll.SelectionMinX + 1 : pianoRoll.CaptureMarqueeMaxX - pianoRoll.CaptureMarqueeMinX + 1;

                newNoteTooltip += $"{numSelected}{(Song.Project.UsesFamiTrackerTempo ? " note" : " frame")}" + (numSelected == 1 ? "" : "s") + " selected";
            }

            App.SetToolTip(tooltip);

            if (noteTooltip != newNoteTooltip)
                pianoRoll.SetNoteTooltip(newNoteTooltip);
        }

        private int GetPixelXForAbsoluteNoteIndex(int n, bool scroll = true) => pianoRoll.GetPixelXForAbsoluteNoteIndex(n, scroll);
        private int GetPixelYForNoteValue(int note) => pianoRoll.GetPixelYForNoteValue(note);
        private float GetSeekFrameToDraw() => pianoRoll.GetSeekFrameToDraw();
        private Color GetSeekBarColor() => pianoRoll.SeekBarColor;
        private bool HasHighlightedNote() => pianoRoll.HasHighlightedNote();
        private Note GetNoteForCoord(int x, int y, out NoteLocation mouseLocation, out NoteLocation noteLocation, out int duration) => pianoRoll.GetNoteForCoord(x, y, out mouseLocation, out noteLocation, out duration);
        private List<Gizmo> GetNoteGizmos(out Note note, out NoteLocation location) => pianoRoll.GetNoteGizmos(out note, out location);
        private bool IsGizmoHighlighted(Gizmo g, int offsetY) => pianoRoll.IsGizmoHighlighted(g, offsetY);
        private bool IsNoteSelected(NoteLocation location, int duration = 0) => pianoRoll.IsNoteSelected(location, duration);
        private bool GetNoteValueForCoord(int x, int y, out byte noteValue) => pianoRoll.GetNoteValueForCoord(x, y, out noteValue);
        private bool GetLocationForCoord(int x, int y, out NoteLocation location, out byte noteValue, bool allowSnap = false) => pianoRoll.GetLocationForCoord(x, y, out location, out noteValue, allowSnap);
        private void DrawSelectionRect(CommandList c, int h, bool effectsPanel = false, bool header = false) => pianoRoll.DrawSelectionRect(c, h, effectsPanel, header);
        private void ClearSelection() => pianoRoll.ClearSelection();
        private SortedList<int, Note> GetSparseSelectedNotes(int offset = 0, bool musicalOnly = false) => pianoRoll.GetSparseSelectedNotes(offset, musicalOnly);
        private int GetVisualNoteDuration(NoteLocation location, Note note) => pianoRoll.GetVisualNoteDuration(location, note);
        private int GetVisualNoteDuration(int absIndex, Note note) => pianoRoll.GetVisualNoteDuration(absIndex, note);
        private void MarkPatternDirty(int patternIdx) => pianoRoll.MarkPatternDirty(patternIdx);
        private void MarkPatternDirty(Pattern pattern) => pianoRoll.MarkPatternDirty(pattern);
        private void PromoteTransaction(TransactionScope scope, int objectId = -1, int subIdx = -1) => pianoRoll.PromoteTransaction(scope, objectId, subIdx);
        private void ScrollIfNearEdge(int x, int y, bool scrollHorizontal = true, bool scrollVertical = false) => pianoRoll.ScrollIfNearEdge(x, y, scrollHorizontal, scrollVertical);
        private int SnapNote(int absoluteNoteIndex, bool roundUp = false, bool forceSnap = false) => pianoRoll.SnapNote(absoluteNoteIndex, roundUp, forceSnap);
        private NoteLocation SnapNote(NoteLocation location, bool roundUp = false, bool forceSnap = false) => pianoRoll.SnapNote(location, roundUp, forceSnap);
        private void StartCaptureOperation(int x, int y, CaptureOperation op, bool allowSnap = false, int noteIdx = -1, int offsetX = 0, int offsetY = 0, bool capturePointer = true) => pianoRoll.StartCaptureOperation(x, y, op, allowSnap, noteIdx, offsetX, offsetY, capturePointer);
        private void TransformNotes(int minAbsoluteNoteIdx, int maxAbsoluteNoteIdx, bool doTransaction, bool doPatternChangeEvent, bool createMissingPatterns, Func<Note, int, Note> function) => pianoRoll.TransformNotes(minAbsoluteNoteIdx, maxAbsoluteNoteIdx, doTransaction, doPatternChangeEvent, createMissingPatterns, function);
        private bool IsNoteSelected(int absoluteNoteIdx) => pianoRoll.IsNoteSelected(absoluteNoteIdx);
        private Note CreateSingleNote(int x, int y) => pianoRoll.CreateSingleNote(x, y);

        private bool HandleNoteGizmos(int x, int y)
        {
            var gizmos = GetNoteGizmos(out var gizmoNote, out var gizmoNoteLocation);
            if (gizmos != null)
            {
                var absNoteLocation = gizmoNoteLocation.ToAbsoluteNoteIndex(Song);
                foreach (var g in gizmos)
                {
                    if (g.Rect.Contains(x - pianoSizeX, y - headerAndEffectSizeY))
                    {
                        CapturePointer();

                        switch (g.Action)
                        {
                            case GizmoAction.ResizeNote:
                                StartNoteResizeEnd(x, y, IsNoteSelected(absNoteLocation) ? CaptureOperation.ResizeSelectionNoteEnd : CaptureOperation.ResizeNoteEnd, gizmoNoteLocation, g.OffsetX, false);
                                break;
                            case GizmoAction.MoveRelease:
                                StartMoveNoteRelease(x, y, IsNoteSelected(absNoteLocation) ? CaptureOperation.MoveSelectionNoteRelease : CaptureOperation.MoveNoteRelease, gizmoNoteLocation, false);
                                break;
                            case GizmoAction.MoveSlide:
                                if (!pianoRoll.StartDragSlideNoteGizmo(x, y, gizmoNoteLocation, gizmoNote, false))
                                    ReleasePointer();
                                break;
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        private bool HandleTouchClickChannelNote(int x, int y)
        {
            if (pianoRoll.GetLocationForCoord(x, y, out var mouseLocation, out byte noteValue))
            {
                if (mouseLocation.PatternIndex >= Song.Length)
                    return true;

                var channel = Song.Channels[editChannel];
                var noteLocation = mouseLocation;
                var note = channel.FindMusicalNoteAtLocation(ref noteLocation, noteValue);

                if (note == null)
                {
                    CreateSingleNote(x, y);
                    pianoRoll.SetLastNoteCreateTime();
                }
                else
                {
                    var absIdx = noteLocation.ToAbsoluteNoteIndex(Song);
                    highlightNoteAbsIndex = highlightNoteAbsIndex == absIdx ? -1 : absIdx;
                }

                return true;
            }

            return false;
        }

        private bool IsNoteOffScale(int key)
        {
            // Apply offset.
            key = key >= rootNoteIdx ? key - rootNoteIdx : key + 12 - rootNoteIdx;

            return (ScaleType)scaleTypeIndex switch
            {
                ScaleType.Minor          => key == 1 || key == 4 || key == 6 || key == 9 || key == 11,
                ScaleType.Dorian         => key == 1 || key == 4 || key == 6 || key == 8 || key == 11,
                ScaleType.Phrygian       => key == 2 || key == 4 || key == 6 || key == 9 || key == 11,
                ScaleType.Lydian         => key == 1 || key == 3 || key == 5 || key == 8 || key == 10,
                ScaleType.Mixolydian     => key == 1 || key == 3 || key == 6 || key == 8 || key == 11,
                ScaleType.Locrian        => key == 2 || key == 4 || key == 7 || key == 9 || key == 11,
                ScaleType.MelodicMinor   => key == 1 || key == 4 || key == 6 || key == 8 || key == 10,
                ScaleType.HarmonicMinor  => key == 1 || key == 4 || key == 6 || key == 9 || key == 10,
                ScaleType.DoubleHarmonic => key == 2 || key == 3 || key == 6 || key == 9 || key == 10,

                // Major scale is default.
                _ => IsBlackKey(key),
            };
        }

        private bool IsBlackKey(int key)
        {
            return key == 1 || key == 3 || key == 6 || key == 8 || key == 10;
        }

        private Color GetNoteColor(Channel channel, int noteValue, Instrument instrument, int alphaDim = 255)
        {
            var color = Theme.LightGreyColor1;

            if (channel.Type == ChannelType.Dpcm && Settings.DpcmColorMode == Settings.ColorModeSample && instrument != null)
            {
                var mapping = instrument.GetDPCMMapping(noteValue);
                if (mapping != null && mapping.Sample != null)
                    color = mapping.Sample.Color;
            }
            else if (instrument != null)
            {
                color = instrument.Color;
            }

            return Color.FromArgb(alphaDim, color);
        }

        private bool ShouldDrawLines(Song song, int patternIdx, int numNotes)
        {
            var x0 = GetPixelXForAbsoluteNoteIndex(song.GetPatternStartAbsoluteNoteIndex(patternIdx));
            var x1 = GetPixelXForAbsoluteNoteIndex(song.GetPatternStartAbsoluteNoteIndex(patternIdx) + numNotes);

            return (x1 - x0) >= minPixelDistForLines;
        }

        private void RenderNotes(RenderInfo r)
        {
            var song = Song;
            var maxX = editMode == EditionMode.Channel ? GetPixelXForAbsoluteNoteIndex(song.GetPatternStartAbsoluteNoteIndex(r.maxVisiblePattern)) : Width;

            // Draw the note backgrounds
            for (int i = r.minVisibleOctave; i < r.maxVisibleOctave; i++)
            {
                int octaveBaseY = (virtualSizeY - octaveSizeY * i) - scrollY;
                for (int j = 0; j < 12; j++)
                {
                    int y = octaveBaseY - j * noteSizeY;

                    // Scales (default C Major for all but channel).
                    if (editMode == EditionMode.Channel)
                    {
                        if (!IsNoteOffScale(j))
                            r.b.FillRectangle(0, y - noteSizeY, maxX, y, Theme.DarkGreyColor4);
                    }
                    else
                    {
                        if (!IsBlackKey(j))
                            r.b.FillRectangle(0, y - noteSizeY, maxX, y, Theme.DarkGreyColor4);
                    }
                }
            }

            DrawSelectionRect(r.b, Height);

            if (editMode == EditionMode.Channel ||
                editMode == EditionMode.VideoRecording)
            {
                // Draw the vertical bars.
                for (int p = r.minVisiblePattern; p < r.maxVisiblePattern; p++)
                {
                    var patternLen = song.GetPatternLength(p);
                    var beatLength = song.GetPatternBeatLength(p);

                    if (song.UsesFamiStudioTempo)
                    {
                        var noteLength = song.GetPatternNoteLength(p);
                        var drawNotes = ShouldDrawLines(song, p, noteLength);
                        var drawFrames = drawNotes && ShouldDrawLines(song, p, 1);

                        // Needed to get dashed lines to scroll properly.
                        for (int i = p == 0 ? 1 : 0; i < patternLen; i++)
                        {
                            int x = GetPixelXForAbsoluteNoteIndex(song.GetPatternStartAbsoluteNoteIndex(p) + i);

                            if (i % beatLength == 0)
                                r.b.DrawLine(x, 0, x, Height, Theme.BlackColor, i == 0 ? 3 : 1);
                            else if (drawNotes && i % noteLength == 0)
                                r.b.DrawLine(x, 0, x, Height, Theme.DarkGreyColor1);
                            else if (drawFrames && editMode != EditionMode.VideoRecording)
                                r.b.DrawLine(x, -scrollY, x, virtualSizeY - scrollY, Theme.DarkGreyColor1, 1, false, true);
                        }
                    }
                    else
                    {
                        var drawNotes = ShouldDrawLines(song, p, 1);

                        for (int i = p == 0 ? 1 : 0; i < patternLen; i++)
                        {
                            int x = GetPixelXForAbsoluteNoteIndex(song.GetPatternStartAbsoluteNoteIndex(p) + i);

                            if (i % beatLength == 0)
                                r.b.DrawLine(x, 0, x, Height, Theme.BlackColor, i == 0 ? 3 : 1);
                            else if (drawNotes)
                                r.b.DrawLine(x, 0, x, Height, Theme.DarkGreyColor2);
                        }
                    }
                }

                // Horizontal black lines.
                for (int i = r.minVisibleOctave; i < r.maxVisibleOctave; i++)
                {
                    int octaveBaseY = (virtualSizeY - octaveSizeY * i) - scrollY;
                    for (int j = 0; j < 12; j++)
                    {
                        int y = octaveBaseY - j * noteSizeY;
                        if (i * 12 + j != PianoRoll.NumNotes || editMode == EditionMode.VideoRecording)
                            r.b.DrawLine(0, y, maxX, y, Theme.BlackColor);
                    }
                }

                r.b.DrawLine(maxX, 0, maxX, Height, Theme.BlackColor, 3);

                if (editMode != EditionMode.VideoRecording)
                {
                    int seekX = GetPixelXForAbsoluteNoteIndex((int)GetSeekFrameToDraw());
                    r.c.DrawLine(seekX, 0, seekX, Height, GetSeekBarColor(), 3);
                }

                // Highlight note under mouse.
                var highlightNote = (Note)null;
                var highlightLocation = NoteLocation.Invalid;
                var highlightReleased = false;
                var highlightLastNoteValue = Note.NoteInvalid;
                var highlightLastInstrument = (Instrument)null;
                var highlightAttackState = NoteAttackState.Attack;

                if (editMode != EditionMode.VideoRecording)
                {
                    if (Platform.IsMobile)
                    {
                        if (HasHighlightedNote())
                        {
                            highlightLocation = NoteLocation.FromAbsoluteNoteIndex(song, highlightNoteAbsIndex);
                            highlightNote = song.Channels[editChannel].GetNoteAt(highlightLocation);
                        }
                    }
                    else if (!ParentWindow.IsAsyncDialogInProgress && !ParentWindow.IsOutOfProcessDialogInProgress)
                    {
                        if (HasHighlightedNote() && pianoRoll.CaptureOperationRequiresNoteHighlight)
                        {
                            highlightLocation = NoteLocation.FromAbsoluteNoteIndex(song, highlightNoteAbsIndex);
                            highlightNote = song.Channels[editChannel].GetNoteAt(highlightLocation);
                        }
                        else if (pianoRoll.IsNoCapture)
                        {
                            var pt = pianoRoll.ScreenToControl(CursorPosition);
                            highlightNote = GetNoteForCoord(pt.X, pt.Y, out _, out highlightLocation, out _);
                        }
                    }
                }

                var ghostChannelMask = App != null ? App.ForceDisplayChannelMask : 0;
                var maxEffectPosY = 0;

                // Render the active channel last.
                var channelsToRender = new int[song.Channels.Length];
                for (int c = 0; c < song.Channels.Length; c++)
                    channelsToRender[c] = c;

                Utils.Swap(ref channelsToRender[editChannel], ref channelsToRender[channelsToRender.Length - 1]);

                // Note drawing.
                foreach (var c in channelsToRender)
                {
                    var channel = song.Channels[c];
                    var channelBitMask = 1L << c;
                    var isActiveChannel = c == editChannel || (videoForceDisplayChannelMask & channelBitMask) != 0;

                    if (isActiveChannel || (ghostChannelMask & channelBitMask) != 0)
                    {
                        var drawImplicitStopNotes =
                            isActiveChannel &&
                            editMode == EditionMode.Channel &&
                            (Settings.ShowImplicitStopNotes && Song.UsesFamiTrackerTempo);

                        var min = new NoteLocation(r.minVisiblePattern, 0);
                        var max = new NoteLocation(r.maxVisiblePattern, 0);

                        var lastNoteValue  = Note.MusicalNoteC4;
                        var lastInstrument = (Instrument)null;

                        // Always start rendering from the last note that had an attack.
                        var lastNoteLocation = channel.GetCachedLastMusicalNoteWithAttackLocation(min.PatternIndex - 1);
                        if (lastNoteLocation.IsValid)
                        {
                            min.PatternIndex = Math.Min(min.PatternIndex, lastNoteLocation.PatternIndex);

                            var note = channel.GetNoteAt(lastNoteLocation);
                            lastNoteValue  = note.Value;
                            lastInstrument = note.Instrument;
                        }

                        var released = false;

                        for (var it = channel.GetSparseNoteIterator(min, max); !it.Done; it.Next())
                        {
                            var note = it.Note;
                            var noteAttackState = NoteAttackState.Attack;

                            // Release notes are no longer supported in the piano roll.
                            Debug.Assert(!note.IsRelease);

                            if (note.IsMusical)
                            {
                                // We'll display an empty attack when the user tries to disable in a
                                // situation where its not supported.
                                noteAttackState = note.HasAttack ?
                                    NoteAttackState.Attack :
                                    Channel.CanDisableAttack(channel.Type, lastInstrument, note.Instrument) ?
                                        NoteAttackState.NoAttack :
                                        NoteAttackState.NoAttackError;

                                if (noteAttackState != NoteAttackState.NoAttack)
                                    released = false;

                                lastNoteValue  = note.Value;
                                lastInstrument = note.Instrument;
                            }

                            if (isActiveChannel && it.Location == highlightLocation)
                            {
                                highlightReleased = released;
                                highlightLastNoteValue = lastNoteValue;
                                highlightLastInstrument = lastInstrument;
                                highlightAttackState = noteAttackState;
                            }

                            if (note.IsMusical)
                            {
                                RenderNote(r, it.Location, note, song, c, it.DistanceToNextCut, drawImplicitStopNotes, isActiveChannel, false, released, noteAttackState);
                            }
                            else if (note.IsStop)
                            {
                                RenderNoteReleaseOrStop(r, note, GetNoteColor(channel, lastNoteValue, lastInstrument, isActiveChannel ? 255 : 50), it.Location.ToAbsoluteNoteIndex(Song), lastNoteValue, false, IsNoteSelected(it.Location, 1), isActiveChannel, true, released);
                            }

                            if (note.HasRelease && note.Release < Math.Min(note.Duration, it.DistanceToNextCut))
                            {
                                released = true;
                            }
                        }

                        if (isActiveChannel && highlightNote != null)
                        {
                            if (highlightNote.IsMusical)
                            {
                                RenderNote(r, highlightLocation, highlightNote, song, c, channel.GetDistanceToNextNote(highlightLocation), drawImplicitStopNotes, true, true, highlightReleased, highlightAttackState);
                            }
                            else if (highlightNote.IsStop)
                            {
                                RenderNoteReleaseOrStop(r, highlightNote, GetNoteColor(channel, highlightLastNoteValue, highlightLastInstrument, isActiveChannel ? 255 : 50), highlightLocation.ToAbsoluteNoteIndex(Song), highlightLastNoteValue, true, false, true, true, highlightReleased);
                            }
                        }
                    }
                }

                // Draw effect icons at the top.
                if (editMode != EditionMode.VideoRecording)
                {
                    var effectIconSizeX = DpiScaling.ScaleCustom(bmpEffects[0].ElementSize.Width, effectBitmapScale);

                    var channel = song.Channels[editChannel];
                    for (int p = r.minVisiblePattern; p < r.maxVisiblePattern; p++)
                    {
                        var pattern = channel.PatternInstances[p];

                        if (pattern == null)
                            continue;

                        var patternLen = song.GetPatternLength(p);

                        foreach (var kv in pattern.Notes)
                        {
                            var time = kv.Key;
                            var note = kv.Value;

                            if (time >= patternLen)
                                break;

                            if (note.HasAnyEffect)
                            {
                                // TODO: Iterate on the bits of the effect mask.
                                var effectPosY = 0;
                                for (int fx = 0; fx < Note.EffectCount; fx++)
                                {
                                    if (note.HasValidEffectValue(fx))
                                    {
                                        // These 2 effects usually come in a pair, so let's draw only 1 icon.
                                        if (fx == Note.EffectVibratoDepth && note.HasValidEffectValue(Note.EffectVibratoSpeed))
                                            continue;
                                        if (fx == Note.EffectVolumeSlide)
                                            continue;

                                        var drawOpaque = !showEffectsPanel || fx == selectedEffectIdx || fx == Note.EffectVibratoDepth && selectedEffectIdx == Note.EffectVibratoSpeed || fx == Note.EffectVibratoSpeed && selectedEffectIdx == Note.EffectVibratoDepth;

                                        var iconX = GetPixelXForAbsoluteNoteIndex(channel.Song.GetPatternStartAbsoluteNoteIndex(p, time)) + (int)(noteSizeX / 2) - effectIconSizeX / 2;
                                        var iconY = effectPosY + effectIconPosY;

                                        r.f.DrawTextureAtlas(bmpEffectFrame, iconX, iconY, effectBitmapScale, drawOpaque ? Theme.LightGreyColor1 : Theme.MediumGreyColor1);
                                        r.f.DrawTextureAtlas(bmpEffects[fx], iconX, iconY, effectBitmapScale, Theme.LightGreyColor1.Transparent(drawOpaque ? 255 : 100));
                                        effectPosY += effectIconSizeX + effectIconPosY + 1;
                                    }
                                }
                                maxEffectPosY = Math.Max(maxEffectPosY, effectPosY);
                            }
                        }
                    }
                }

                if (editMode == EditionMode.Channel)
                {
                    var gizmos = GetNoteGizmos(out var gizmoNote, out _);
                    if (gizmos != null)
                    {
                        foreach (var g in gizmos)
                        {
                            var highlighted = IsGizmoHighlighted(g, headerAndEffectSizeY);
                            var fillColor = GetNoteColor(Song.Channels[editChannel], gizmoNote.Value, gizmoNote.Instrument);
                            var lineColor = highlighted ? Color.White : Color.Black;

                            if (g.FillImage != null)
                                r.f.DrawTextureAtlas(g.FillImage, g.Rect.X, g.Rect.Y, g.Rect.Width / (float)g.FillImage.ElementSize.Width, fillColor);
                            r.f.DrawTextureAtlas(g.Image, g.Rect.X, g.Rect.Y, g.Rect.Width / (float)g.FillImage.ElementSize.Width, lineColor);

                            if (highlighted && !string.IsNullOrEmpty(g.GizmoText))
                                r.f.DrawText(g.GizmoText, r.fonts.FontSmall, g.Rect.X - g.Rect.Width / 8, g.Rect.Y, Theme.WhiteColor, TextFlags.MiddleRight, 0, g.Rect.Height);
                        }
                    }

                    if (Platform.IsMobile && pianoRoll.IsDeleteNotesCapture)
                    {
                        var touchX = mouseLastX - pianoSizeX;
                        var touchY = mouseLastY - headerAndEffectSizeY;
                        r.f.PushTranslation(touchX, touchY);
                        r.f.FillGeometry(mobileEraseGeometry, new Color(Theme.LightGreyColor1.R, Theme.LightGreyColor1.G, Theme.LightGreyColor1.B, 128), false);
                        r.f.PopTransform();
                    }

                    var channelType = song.Channels[editChannel].Type;
                    var channelName = song.Channels[editChannel].NameWithExpansion;

                    r.f.DrawText(EditingChannelLabel.Format(channelName), r.fonts.FontVeryLarge, bigTextPosX, maxEffectPosY > 0 ? maxEffectPosY : bigTextPosY, Theme.LightGreyColor1);
                }
            }
            else if (App.Project != null) // Happens if DPCM panel is open and importing an NSF.
            {
                // Horizontal black lines.
                for (int i = r.minVisibleOctave; i < r.maxVisibleOctave; i++)
                {
                    int octaveBaseY = (virtualSizeY - octaveSizeY * i) - scrollY;
                    for (int j = 0; j < 12; j++)
                    {
                        int y = octaveBaseY - j * noteSizeY;
                        if (i * 12 + j != PianoRoll.NumNotes)
                            r.b.DrawLine(0, y, maxX, y, Theme.BlackColor);
                    }
                }

                foreach (var kv in editInstrument.SamplesMapping)
                {
                    var note = kv.Key;
                    var mapping = kv.Value;
                    if (mapping != null && mapping.Sample != null)
                    {
                        var y = virtualSizeY - note * noteSizeY - scrollY;
                        var highlighted = note == highlightDPCMSample;

                        r.c.PushTranslation(0, y);
                        r.c.FillAndDrawRectangleGradient(0, 0, Width, noteSizeY, mapping.Sample.Color, mapping.Sample.Color.Scaled(0.8f), highlighted ? Theme.WhiteColor : Theme.BlackColor, true, noteSizeY, highlighted ? 3 : 1, highlighted, highlighted);

                        string text = $"{mapping.Sample.Name} - {PitchLabel}: {DPCMSampleRate.GetString(true, FamiStudio.StaticInstance.PalPlayback, true, true, mapping.Pitch)}";
                        if (mapping.Loop) text += $", {LoopingLabel}";
                        if (mapping.OverrideDmcInitialValue) text += $" , {DMCInitialValueLabel} = {mapping.DmcInitialValueDiv2}";

                        r.c.DrawText(text, r.fonts.FontSmall, dpcmTextPosX, 0, Theme.BlackColor, TextFlags.MiddleLeft, 0, noteSizeY);
                        r.c.PopTransform();
                    }
                }

                DPCMSample dragSample = null;

                if (pianoRoll.IsDragSampleCapture && draggedSample != null)
                {
                    dragSample = draggedSample.Sample;
                }
                else if (pianoRoll.IsNoCapture && App.DraggedSample != null)
                {
                    dragSample = App.DraggedSample;
                }

                if (dragSample != null)
                {
                    var pt = Platform.IsDesktop ? pianoRoll.ScreenToControl(CursorPosition) : new Point(mouseLastX, mouseLastY);

                    if (GetNoteValueForCoord(pt.X, pt.Y, out var noteValue))
                    {
                        var y = virtualSizeY - noteValue * noteSizeY - scrollY;
                        r.c.PushTranslation(0, y);
                        r.c.FillAndDrawRectangleGradient(0, 0, Width, noteSizeY, dragSample.Color, dragSample.Color.Scaled(0.8f), Theme.WhiteColor, true, noteSizeY, 3, true, true);
                        r.c.PopTransform();
                    }
                }
                else if (Platform.IsDesktop && pianoRoll.IsNoCapture)
                {
                    var pt = pianoRoll.ScreenToControl(CursorPosition);

                    if (GetLocationForCoord(pt.X, pt.Y, out _, out var highlightNoteValue))
                    {
                        var mapping = editInstrument.GetDPCMMapping(highlightNoteValue);
                        if (mapping != null)
                        {
                            var y = virtualSizeY - highlightNoteValue * noteSizeY - scrollY;

                            r.c.PushTranslation(0, y);
                            r.c.DrawRectangle(0, 0, Width, noteSizeY, Theme.WhiteColor, 3, true, true);
                            r.c.PopTransform();
                        }
                    }
                }

                var textY = bigTextPosY;
                r.f.DrawText(EditingInstrumentDPCMLabel.Format(editInstrument.Name), r.fonts.FontVeryLarge, bigTextPosX, bigTextPosY, Theme.LightGreyColor1);
                textY += r.fonts.FontVeryLarge.LineHeight;
                r.f.DrawText(DPCMInstrumentUsageLabel.Format(editInstrument.GetTotalMappedSampleSize(), Project.MaxMappedSampleSize), r.fonts.FontMedium, bigTextPosX, textY, Theme.LightGreyColor1);
                textY += r.fonts.FontMedium.LineHeight;

                for (int i = 0; i < Project.MaxDPCMBanks; i++)
                {
                    var bankSize = App.Project.GetBankSize(i);
                    if (bankSize > 0)
                    {
                        r.f.DrawText(DPCMBankUsageLabel.Format(bankSize, i), r.fonts.FontMedium, bigTextPosX, textY, Theme.LightGreyColor1);
                        textY += r.fonts.FontMedium.LineHeight;
                    }
                }
            }
        }

        private void RenderNoteBody(RenderInfo r, Note note, Color color, int time, int noteLen, int transpose, bool outline, bool selected, bool activeChannel, bool released, bool isFirstPart, int slideDuration, NoteAttackState attackState)
        {
            var noteValue = note.Value + transpose;
            var x = GetPixelXForAbsoluteNoteIndex(time);
            var y = GetPixelYForNoteValue(noteValue);
            var sy = released ? releaseNoteSizeY : noteSizeY;
            var activeChannelInt = activeChannel ? 0 : 1;

            if (!outline && isFirstPart && slideDuration >= 0)
            {
                // We will get zero for notes that start a slide and have an immediate delayed cut.
                var duration = Math.Max(1, slideDuration);
                var slideSizeX = duration;
                var slideSizeY = note.SlideNoteTarget + transpose - noteValue;

                r.c.PushTransform(x, y + (slideSizeY > 0 ? 0 : noteSizeY), GetPixelXForAbsoluteNoteIndex(slideSizeX, false), -slideSizeY);
                r.c.FillGeometry(slideNoteGeometry, Color.FromArgb(50, color), true);
                r.c.PopTransform();
            }

            if (released)
                y += noteSizeY / 2 - releaseNoteSizeY / 2;

            r.c.PushTranslation(x, y);

            int sx = GetPixelXForAbsoluteNoteIndex(noteLen, false);
            int noteTextPosX = attackIconPosX + 1;

            if (!outline)
            {
                r.c.FillRectangleGradient(0, activeChannelInt, sx, sy, color, color.Scaled(0.8f), true, sy);

                if (selected && !legacySelectMode)
                    r.c.FillRectangleGradient(0, activeChannelInt, sx, sy,  selectionHighlightColor, selectionHighlightColor.Scaled(0.8f), true, sy);
            }

            if (activeChannel)
            {
                r.c.DrawRectangle(0, 0, sx, sy, outline ? Theme.WhiteColor : (selected ? legacySelectMode ? Theme.LightGreyColor1 : Theme.WhiteColor : Theme.BlackColor), selected || outline ? 3 : 1, selected || outline, selected || outline);
            }

            if (!outline)
            {
                if (activeChannel && isFirstPart && attackState != NoteAttackState.NoAttack && sx > noteAttackSizeX + attackIconPosX * 2 + 2)
                {
                    if (attackState == NoteAttackState.NoAttackError)
                    {
                        r.c.DrawRectangle(attackIconPosX + 1, attackIconPosX + 1, attackIconPosX + noteAttackSizeX, sy - attackIconPosX - 1, activeChannel ? attackColor : attackBrushForceDisplayColor);
                    }
                    else
                    {
                        r.c.FillRectangle(attackIconPosX + 1, attackIconPosX + 1, attackIconPosX + noteAttackSizeX + 1, sy - attackIconPosX, activeChannel ? attackColor : attackBrushForceDisplayColor);
                    }
                    noteTextPosX += noteAttackSizeX + attackIconPosX + 2;
                }

                if (activeChannel && !released && editMode == EditionMode.Channel && note.IsMusical && r.fonts.FontSmall.Size < noteSizeY)
                {
                    var label = note.FriendlyName;
                    if ((sx - noteTextPosX) > (label.Length + 1) * fontSmallCharSizeX)
                        r.c.DrawText(note.FriendlyName, r.fonts.FontSmall, noteTextPosX, 1, Theme.BlackColor, TextFlags.Middle, 0, noteSizeY);
                }

                if (note.Arpeggio != null)
                {
                    var offsets = note.Arpeggio.GetChordOffsets();
                    foreach (var offset in offsets)
                    {
                        r.c.PushTranslation(0, offset * -noteSizeY);
                        r.c.FillRectangle(0, 1, sx, sy, Color.FromArgb(activeChannel ? 50 : 25, note.Arpeggio.Color));
                        r.c.PopTransform();
                    }
                }
            }

            r.c.PopTransform();
        }

        private void RenderNoteReleaseOrStop(RenderInfo r, Note note, Color color, int time, int value, bool outline, bool selected, bool activeChannel, bool stop, bool released)
        {
            int x = GetPixelXForAbsoluteNoteIndex(time);
            int y = GetPixelYForNoteValue(value);
            var geo = stop ? (released ? stopReleaseNoteGeometry : stopNoteGeometry) : releaseNoteGeometry;

            r.c.PushTransform(x, y, noteSizeX, 1);
            if (!outline)
            {
                r.c.FillGeometryGradient(geo[activeChannel ? 0 : 1], color, color.Scaled(0.8f), noteSizeY);

                if (selected && !legacySelectMode)
                    r.c.FillGeometryGradient(geo[activeChannel ? 0 : 1], selectionHighlightColor, selectionHighlightColor.Scaled(0.8f), noteSizeY);
            }
            if (activeChannel)
                r.c.DrawGeometry(geo[0], outline ? Theme.WhiteColor : (selected ? legacySelectMode ? Theme.LightGreyColor1 : Theme.WhiteColor : Theme.BlackColor), outline || selected ? 3 : 1, true);
            r.c.PopTransform();

            r.c.PushTranslation(x, y);
            if (!outline && note.Arpeggio != null)
            {
                var offsets = note.Arpeggio.GetChordOffsets();
                foreach (var offset in offsets)
                {
                    r.c.PushTransform(0, offset * -noteSizeY, noteSizeX, 1);
                    r.c.FillGeometry(geo[1], Color.FromArgb(activeChannel ? 50 : 25, note.Arpeggio.Color), true);
                    r.c.PopTransform();
                }
            }
            r.c.PopTransform();
        }

        private void RenderNote(RenderInfo r, NoteLocation location, Note note, Song song, int channelIndex, int distanceToNextNote, bool drawImplicityStopNotes, bool isActiveChannel, bool highlighted, bool released, NoteAttackState attackState)
        {
            Debug.Assert(note.IsMusical);

            if (distanceToNextNote < 0)
                distanceToNextNote = (int)ushort.MaxValue;

            var channel = song.Channels[channelIndex];
            var absoluteIndex = location.ToAbsoluteNoteIndex(Song);
            var nextAbsoluteIndex = absoluteIndex + distanceToNextNote;
            var duration = Math.Min(distanceToNextNote, note.Duration);
            var slideDuration = note.IsSlideNote && note.Value != note.SlideNoteTarget ? channel.GetSlideNoteDuration(location) : -1;
            var color = GetNoteColor(channel, note.Value, note.Instrument, isActiveChannel ? 255 : 50);
            var selected = isActiveChannel && IsNoteSelected(location, duration);
            var transpose = videoChannelTranspose != null ? videoChannelTranspose[channelIndex] : 0;

            // Draw first part, from start to release point.
            if (note.HasRelease)
            {
                RenderNoteBody(r, note, color, absoluteIndex, Math.Min(note.Release, duration), transpose, highlighted, selected, isActiveChannel, released, true, slideDuration, attackState);
                absoluteIndex += note.Release;
                duration -= note.Release;

                if (duration > 0)
                {
                    RenderNoteReleaseOrStop(r, note, color, absoluteIndex, note.Value + transpose, highlighted, selected, isActiveChannel, false, released);
                    absoluteIndex++;
                    duration--;
                }

                released = true;
            }

            // Then second part, after release to stop note.
            if (duration > 0)
            {
                RenderNoteBody(r, note, color, absoluteIndex, duration, transpose, highlighted, selected, isActiveChannel, released, !note.HasRelease, slideDuration, attackState);
                absoluteIndex += duration;

                if (drawImplicityStopNotes && absoluteIndex < nextAbsoluteIndex && !highlighted)
                {
                    RenderNoteReleaseOrStop(r, note, Color.FromArgb(128, color), absoluteIndex, note.Value + transpose, highlighted, selected, isActiveChannel, true, released);
                }
            }
        }

        private void RenderNoteArea(RenderInfo r)
        {
            r.c.PushClipRegion(0, 0, Width, Height);

            if (editMode == EditionMode.Channel ||
                editMode == EditionMode.VideoRecording ||
                editMode == EditionMode.DPCMMapping)
            {
                RenderNotes(r);
            }

            if (!string.IsNullOrEmpty(noteTooltip) && editMode != EditionMode.DPCM)
            {
                var textWidth = Width - tooltipTextPosX;
                if (textWidth > 0)
                    r.f.DrawText(noteTooltip, r.fonts.FontLarge, 0, Height - tooltipTextPosY, Theme.LightGreyColor1, TextFlags.Right, textWidth);
            }

            r.c.PopClipRegion();
        }

        protected override void OnRender(Graphics g)
        {
            base.OnRender(g);

            var r = new RenderInfo();

            var minVisibleNoteIdx = Math.Max(pianoRoll.GetAbsoluteNoteIndexForPixelX(0), 0);
            var maxVisibleNoteIdx = Math.Min(pianoRoll.GetAbsoluteNoteIndexForPixelX(pianoRoll.Width) + 1, Song.GetPatternStartAbsoluteNoteIndex(Song.Length));

            r.g = g;
            r.fonts = Fonts;
            r.b = g.BackgroundCommandList;
            r.c = g.DefaultCommandList;
            r.f = g.ForegroundCommandList;

            var minNote = editMode == EditionMode.VideoRecording ? -10000 : 0;
            var maxNote = editMode == EditionMode.VideoRecording ?  10000 : PianoRoll.NumNotes;

            r.maxVisibleNote = PianoRoll.NumNotes - Utils.Clamp((int)Math.Floor(scrollY / (float)noteSizeY), minNote, maxNote);
            r.minVisibleNote = PianoRoll.NumNotes - Utils.Clamp((int)Math.Ceiling((scrollY + Height) / (float)noteSizeY), minNote, maxNote);

            r.maxVisibleOctave = (int)Math.Ceiling(r.maxVisibleNote / 12.0f);
            r.minVisibleOctave = (int)Math.Floor(r.minVisibleNote / 12.0f);

            r.minVisiblePattern = Utils.Clamp(Song.PatternIndexFromAbsoluteNoteIndex(minVisibleNoteIdx), 0, Song.Length);
            r.maxVisiblePattern = Utils.Clamp(Song.PatternIndexFromAbsoluteNoteIndex(maxVisibleNoteIdx) + 1, 0, Song.Length);

            RenderNoteArea(r);
        }
    }
}
