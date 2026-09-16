using System;
using System.Diagnostics;

namespace FamiStudio
{
    public class ChannelStateVrc6Saw : ChannelState
    {
        int prevPeriodHi;

        public ChannelStateVrc6Saw(IPlayerInterface player, int apuIdx, int channelType, int tuning, bool pal) : base(player, apuIdx, channelType, tuning, pal)
        {
        }

        public override void UpdateAPU()
        {
            if (note.IsStop)
            {
                WriteRegister(NesApu.VRC6_SAW_VOL, 0x00);
            }
            else if (note.IsMusical)
            {
                var period = GetPeriod();
                var volume = GetVolume();
                var instrument = note.Instrument;

                var periodHi = ((period >> 8) & 0x0f);
                prevPeriodHi = periodHi;

                Debug.Assert(instrument == null || instrument.IsVrc6);

                if (instrument == null || !instrument.Vrc6SawFullVolume)
                {
                    var sawMasterVolume = instrument != null ? instrument.Vrc6SawMasterVolume : Vrc6SawMasterVolumeType.Full;
                    volume <<= (2 - sawMasterVolume);
                }

                WriteRegister(NesApu.VRC6_SAW_LO, ((period >> 0) & 0xff));
                WriteRegister(NesApu.VRC6_SAW_HI, periodHi | 0x80);
                WriteRegister(NesApu.VRC6_SAW_VOL, volume);
            }

            base.UpdateAPU();
        }

        protected override void ResetPhase()
        {
            // Clear and set the hi-bit of B002 to reset phase.
            SkipCycles(6); // tax/lda
            WriteRegister(NesApu.VRC6_SAW_HI, prevPeriodHi);
            SkipCycles(2); // ora
            WriteRegister(NesApu.VRC6_SAW_HI, prevPeriodHi | 0x80);
            SkipCycles(2); // txa
        }
    }
}
