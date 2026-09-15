using System;
using System.Diagnostics;

namespace FamiStudio
{
    class ChannelStateFds : ChannelState
    {
        private byte    modDelayCounter;
        private ushort  modDepth;
        private ushort  modSpeed;
        private int     prevPeriodHi;
        private int     waveIndex = -1;
        private int     masterVolume;
        private sbyte[] prevModTable = new sbyte[32];

        private static readonly byte[] dacLevelMapping =
        {
            0,  1,  2,  3,  4,  5,  6,  7,  8,  9,  10, 11, 12, 13, 14, 16, 15, 17, 18, 19, 20, 21, 22, 24, 23, 25, 26, 28, 27, 32, 29, 33,
            30, 34, 36, 35, 31, 37, 38, 40, 41, 39, 42, 44, 43, 48, 45, 46, 49, 50, 47, 52, 51, 53, 54, 56, 57, 55, 58, 60, 59, 61, 62, 63
        };

        public ChannelStateFds(IPlayerInterface player, int apuIdx, int channelIdx, int tuning, bool pal) : base(player, apuIdx, channelIdx, tuning, pal)
        {
        }

        protected override void LoadInstrument(Instrument instrument)
        {
            if (instrument != null)
            {
                Debug.Assert(instrument.IsFds);

                if (instrument.IsFds)
                {
                    var mod = instrument.Envelopes[EnvelopeType.FdsModulation].BuildFdsModulationTable();

                    //Debug.Assert(wav.Length == 0x40);
                    Debug.Assert(mod.Length == 0x20);

                    waveIndex = -1; // Force reload on instrument change.
                    masterVolume = instrument.FdsMasterVolume;
                    ConditionalLoadWave();

                    // Only write the modulation table if the mod envelope has actually changed. 
                    // ASM does this using a pointer, since identical envelopes are merged.
                    for (var i = 0; i < mod.Length; i++)
                    {
                        if (mod[i] != prevModTable[i])
                        {
                            WriteRegister(NesApu.FDS_MOD_HI, 0x80);
                            WriteRegister(NesApu.FDS_SWEEP_BIAS, 0x00);

                            for (int j = 0; j < 0x20; ++j)
                                WriteRegister(NesApu.FDS_MOD_TABLE, mod[j] & 0xff, 16); // 16 cycles to mimic ASM loop.

                            break;
                        }
                    }

                    prevModTable = mod;
                }
            }
        }

        private void ConditionalLoadWave()
        {
            var newWaveIndex = envelopeIdx[EnvelopeType.WaveformRepeat];

            if (newWaveIndex != waveIndex)
            {
                var wav = envelopes[EnvelopeType.FdsWaveform]?.GetChunk(newWaveIndex);
                if (wav != null)
                {
                    // We read the table from end to start to mimic the ASM code (saves cycles).
                    SkipCycles(2); // LDY
                    for (int i = 0x3F; i >= 0; i -= 2)
                    {
                        var s0 = wav[i];
                        var s1 = wav[i - 1];

                        // Workaround DAC issue that causes non-monotonic output levels.
                        if (note.Instrument.FdsFixDac)
                        {
                            s0 = dacLevelMapping[s0];
                            s1 = dacLevelMapping[s1];
                        }

                        // Toggle write every 2 iterations. ASM does this for smooth cycling between
                        // waveforms. We write twice between write toggling and iterate half the times 
                        // to save CPU cycles. 38 skipped cycles to mimic ASM loop (37 if BPL exits loop).
                        SkipCycles(3); // Read volume.
                        WriteRegister(NesApu.FDS_VOL, 0x80 | masterVolume, 4);
                        WriteRegister(NesApu.FDS_WAV_START + i,     s0 & 0xff, 12); // +7 for LDA and DEY
                        WriteRegister(NesApu.FDS_WAV_START + i - 1, s1 & 0xff, 10); // +5 for LDA
                        WriteRegister(NesApu.FDS_VOL, masterVolume, i > 1 ? 9 : 8);      // +5 for DEY and BPL (4 on BPL exit)
                    }

                    waveIndex = newWaveIndex;
                }
            }
        }

        private void ResetModulation()
        {
            WriteRegister(NesApu.FDS_MOD_LO, 0x00);
            WriteRegister(NesApu.FDS_SWEEP_BIAS, 0x00);
            WriteRegister(NesApu.FDS_MOD_HI, 0x80);
            WriteRegister(NesApu.FDS_SWEEP_ENV, 0x80);
        }

        public override int GetEnvelopeFrame(int envIdx)
        {
            if (envIdx == EnvelopeType.FdsWaveform)
            {
                return waveIndex * 64 + NesApu.GetFdsWavePos(apuIdx);
            }
            else
            {
                return base.GetEnvelopeFrame(envIdx);
            }
        }

        public override void UpdateAPU()
        {
            var modDepthEffect = false;
            var modSpeedEffect = false;

            if (note.HasFdsModDepth) 
            { 
                modDepth = note.FdsModDepth; 
                modDepthEffect = true; 
            }

            if (note.HasFdsModSpeed) 
            { 
                modSpeed = note.FdsModSpeed; 
                modSpeedEffect = true; 
            }

            if (note.IsStop)
            {
                // Keep last volume if option is checked.
                if (note.Instrument != null && note.Instrument.FdsHoldVolume)
                    WriteRegister(NesApu.FDS_VOL, 0x80 | masterVolume);
                else
                    WriteRegister(NesApu.FDS_VOL_ENV, 0x80); // Zero volume
                ResetModulation();
            }
            else if (note.IsMusical)
            {
                ConditionalLoadWave();

                var period = GetPeriod();
                var volume = GetVolume();
                var instrument = note.Instrument;

                var periodHi = (period >> 8) & 0x0f;
                prevPeriodHi = periodHi;

                WriteRegister(NesApu.FDS_FREQ_HI, periodHi);
                WriteRegister(NesApu.FDS_FREQ_LO, (period >> 0) & 0xff);
                WriteRegister(NesApu.FDS_VOL_ENV, 0x80 | volume);
                WriteRegister(NesApu.FDS_VOL, masterVolume); // Ensure we clear this if the last volume was held.

                if (noteTriggered)
                {
                    // TODO: We used to set the modulation value here, but that's bad.
                    // https://www.nesdev.org/wiki/FDS_audio#Mod_frequency_high_($4087)
                    // WriteRegister(NesApu.FDS_MOD_HI, 0x80);
                    // WriteRegister(NesApu.FDS_SWEEP_BIAS, 0);

                    if (instrument != null)
                    {
                        Debug.Assert(instrument.IsFds);
                        modDelayCounter = instrument.FdsModDelay;
                        
                        // If there was a mod depth/speed this frame, it takes over the instrument.
                        if (!modDepthEffect)
                            modDepth = instrument.FdsModDepth;
                        if (!modSpeedEffect)
                            modSpeed = instrument.FdsModSpeed;
                    }
                    else
                    {
                        modDelayCounter = 0;
                    }
                }

                if (modDelayCounter == 0)
                {
                    var finalModSpeed = instrument != null && instrument.FdsAutoMod && modDepth > 0 ?
                        (ushort)Math.Min(period * instrument.FdsAutoModNumer / instrument.FdsAutoModDenom, 0xfff) : 
                        modSpeed;

                    WriteRegister(NesApu.FDS_MOD_HI, (finalModSpeed >> 8) & 0xff);
                    WriteRegister(NesApu.FDS_MOD_LO, (finalModSpeed >> 0) & 0xff);
                    WriteRegister(NesApu.FDS_SWEEP_ENV, 0x80 | modDepth);
                }
                else
                {
                    ResetModulation();
                    modDelayCounter--;
                }
            }

            base.UpdateAPU();
        }

        protected override void ResetPhase()
        {
            SkipCycles(6); // lda/ora
            WriteRegister(NesApu.FDS_FREQ_HI, prevPeriodHi | 0x80);
            SkipCycles(2); // and
            WriteRegister(NesApu.FDS_FREQ_HI, prevPeriodHi);
        }
    }
}
