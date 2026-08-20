/*
 * (C) 2023 Radrat Softworks
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Nofun.VM;
using Nofun.Driver.Audio;
using System;
using Nofun.Util.Logging;

namespace Nofun.Module.VMGP
{
    [Module]
    public partial class VMGP
    {
        private ISound currentSound;
        private IPcmSound beepSound;

        private const int BeepSampleRate = 22050;
        private const short BeepAmplitude = 8192;

        [ModuleCall]
        private void vBeep(uint frequency, uint durationInMs)
        {
            if ((frequency == 0) || (durationInMs == 0))
            {
                return;
            }

            try
            {
                int sampleCount = (int)(BeepSampleRate * durationInMs / 1000);
                if (sampleCount == 0)
                {
                    return;
                }

                // Synthesize a square wave tone
                int halfPeriod = Math.Max(1, (int)(BeepSampleRate / (frequency * 2)));
                byte[] samples = new byte[sampleCount * 2];

                for (int i = 0; i < sampleCount; i++)
                {
                    short value = (((i / halfPeriod) & 1) == 0) ? BeepAmplitude : (short)-BeepAmplitude;

                    samples[i * 2] = (byte)value;
                    samples[i * 2 + 1] = (byte)(value >> 8);
                }

                beepSound?.Stop();
                beepSound = system.AudioDriver.LoadPCMSound(samples, 0, BeepSampleRate, 1, 16, false);
                beepSound.Play();
            }
            catch (Exception ex)
            {
                Logger.Error(LogClass.VMGPSound, $"Beep failed: {ex}");
            }
        }

        [ModuleCall]
        private int vPlayResource(VMPtr<byte> data, uint length, uint flags)
        {
            if ((flags & (uint)SoundFlag.Stop) != 0)
            {
                currentSound?.Stop();
                return 1;
            }

            if (currentSound != null)
            {
                currentSound.Stop();
                currentSound = null;
            }

            SoundType soundType = (SoundType)(flags & 0xF);
            bool loop = (((flags & (uint)SoundFlag.Loop) != 0) ? true : false);

            Span<byte> dataRead;

            if ((flags & (uint)SoundFlag.Stream) != 0)
            {
                int streamHandle = (int)data.address;
                VMStream.IVMHostStream stream = system.VMStreamModule.GetStream(streamHandle);

                if (stream == null)
                {
                    Logger.Error(LogClass.VMGPSound, $"No stream with handle {streamHandle} found, resource play failed!");
                    return 0;
                }

                dataRead = new byte[length];
                if (stream.Read(dataRead, null) != length)
                {
                    Logger.Error(LogClass.VMGPSound, $"Failed to to read {length} bytes resource data from stream, resource play failed!");
                    return 0;
                }
            }
            else
            {
                dataRead = data.AsSpan(system.Memory, (int)length);
            }

            currentSound = system.AudioDriver.PlaySound(soundType, dataRead, loop);
            return 1;
        }
    }
}