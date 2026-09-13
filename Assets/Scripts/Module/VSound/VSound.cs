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

using Nofun.Driver.Audio;
using Nofun.Module.VMStream;
using Nofun.Util.Logging;
using Nofun.VM;
using System;
using System.Runtime.InteropServices;

namespace Nofun.Module.VSound
{
    [Module]
    public partial class VSound : IDisposable
    {
        private const int SND_OK = 0;
        private const int SND_ERR = -1;

        private const int SNDLOAD_STREAM = 0;
        private const int SNDLOAD_RESOURCE = 1;
        private const int SNDLOAD_FILE = 2;

        private readonly System.Collections.Generic.HashSet<int> soundEffects = new();
        private int nextSoundEffectId = 1;

        private const int SND_MAXVOLUME = 127;
        private const int SND_MINVOLUME = 0;
        private const int SND_MAXPAN = 127;

        private VMSystem system;
        private SimpleObjectManager<IPcmSound> soundManager;

        public VSound(VMSystem system)
        {
            this.system = system;
            this.soundManager = new();
        }

        [ModuleCall]
        int vSoundInit()
        {
            if (!system.AudioDriver.InitializePCMPlay())
            {
                return SND_ERR;
            }

            return SND_OK;
        }

        [ModuleCall]
        private int vSoundGetHandle(VMPtr<byte> soundData)
        {
            Span<NativeSoundHeader> soundHeader = soundData.Cast<NativeSoundHeader>().AsSpan(system.Memory, 1);
            Span<byte> soundDataPtr = (soundData + Marshal.SizeOf<NativeSoundHeader>()).AsSpan(system.Memory, (int)soundHeader[0].bodySize);

            try
            {
                IPcmSound soundFromDriver = system.AudioDriver.LoadPCMSound(soundDataPtr, soundHeader[0].priority,
                    (int)soundHeader[0].frequency, soundHeader[0].channelCount,
                    soundHeader[0].bitsPerSample, soundHeader[0].format == (uint)NativeVSndFormat.ADPCM,
                    (int)soundHeader[0].loopStartFrame, (int)soundHeader[0].loopEndFrame);

                return soundManager.Add(soundFromDriver);
            }
            catch (Exception ex)
            {
                Logger.Error(LogClass.VMGPSound, $"Error while creating PCM sound object: {ex}");
                return SND_ERR;
            }
        }

        [ModuleCall]
        private int vSoundLoad(int handle, int flags)
        {
            IVMHostStream stream = null;
            bool ownsStream = false;

            try
            {
                switch (flags)
                {
                    case SNDLOAD_STREAM:
                        stream = system.VMStreamModule.GetStream(handle);
                        break;

                    case SNDLOAD_RESOURCE:
                        stream = system.VMStreamModule.Open("",
                            (uint)StreamFlags.Read | (uint)StreamType.Resource | (uint)(handle << 16));
                        ownsStream = true;
                        break;

                    case SNDLOAD_FILE:
                        stream = system.VMStreamModule.Open(new VMString((uint)handle).Get(system.Memory),
                            (uint)StreamFlags.Read | (uint)StreamType.File);
                        ownsStream = true;
                        break;

                    default:
                        Logger.Error(LogClass.VMGPSound, $"Unknown sound load flags: {flags}");
                        return SND_ERR;
                }

                if (stream == null)
                {
                    return SND_ERR;
                }

                int headerSize = Marshal.SizeOf<NativeSoundHeader>();
                byte[] headerBytes = new byte[headerSize];

                if (stream.Read(headerBytes, null) != headerSize)
                {
                    return SND_ERR;
                }

                NativeSoundHeader header = MemoryMarshal.Cast<byte, NativeSoundHeader>(headerBytes)[0];

                byte[] body = new byte[header.bodySize];
                if (stream.Read(body, null) != body.Length)
                {
                    return SND_ERR;
                }

                IPcmSound soundFromDriver = system.AudioDriver.LoadPCMSound(body, header.priority,
                    (int)header.frequency, header.channelCount, header.bitsPerSample,
                    header.format == (uint)NativeVSndFormat.ADPCM,
                    (int)header.loopStartFrame, (int)header.loopEndFrame);

                return soundManager.Add(soundFromDriver);
            }
            catch (Exception ex)
            {
                Logger.Error(LogClass.VMGPSound, $"Error while loading sound: {ex}");
                return SND_ERR;
            }
            finally
            {
                if (ownsStream && (stream != null))
                {
                    stream.OnClose();
                }
            }
        }

        [ModuleCall]
        private int vSoundUpload(int handle)
        {
            // Sounds are fully loaded host-side already, there is no dedicated
            // hardware sound memory to upload to.
            return (soundManager.Get(handle) != null) ? SND_OK : SND_ERR;
        }

        [ModuleCall]
        private int vSoundCreateEffect(VMPtr<Any> effects, int count)
        {
            // DSP effects are not audible in the emulator. Track the ids so
            // the rest of the effect API stays consistent for the game.
            int id = nextSoundEffectId++;
            soundEffects.Add(id);

            Logger.Trace(LogClass.VMGPSound, $"Sound effect {id} created (effects are not audible)");
            return id;
        }

        [ModuleCall]
        private int vSoundModifyEffect(VMPtr<Any> effects, int id, int index, int count)
        {
            return soundEffects.Contains(id) ? SND_OK : SND_ERR;
        }

        [ModuleCall]
        private int vSoundDestroyEffect(int id)
        {
            return soundEffects.Remove(id) ? SND_OK : SND_ERR;
        }

        [ModuleCall]
        private int vSoundSetEffect(int handle, int id)
        {
            return ((soundManager.Get(handle) != null) && soundEffects.Contains(id)) ? SND_OK : SND_ERR;
        }

        [ModuleCall]
        private int vSoundDispose()
        {
            foreach (var sound in soundManager)
            {
                sound.Dispose();
            }

            soundManager.Reset();
            return SND_OK;
        }

        [ModuleCall]
        private int vSoundDisposeHandle(int handle)
        {
            if (handle <= 0)
            {
                return SND_OK;
            }

            soundManager.Remove(handle);
            return SND_OK;
        }

        private bool DoesControlRequireInstance(NativeSoundControlCode control)
        {
            return (control != NativeSoundControlCode.MasterVolume) && (control != NativeSoundControlCode.ChannelCount);
        }

        [ModuleCall]
        private int vSoundCtrl(int handle, NativeSoundControlCode control)
        {
            return vSoundCtrlEx(handle, control, 0);
        }

        [ModuleCall]
        private int vSoundCtrlEx(int handle, NativeSoundControlCode control, int parameters)
        {
            IPcmSound soundPcm = soundManager.Get(handle);
            if (soundPcm == null)
            {
                if (DoesControlRequireInstance(control))
                {
                    Logger.Error(LogClass.VSound, $"Sound handle={handle} is invalid! Control {control} failed!");
                    return SND_ERR;
                }
            }

            switch (control)
            {
                case NativeSoundControlCode.Play:
                    soundPcm.Play(LoopCountFromPlayParameter(parameters));
                    break;

                case NativeSoundControlCode.Stop:
                    soundPcm.Stop();
                    break;

                case NativeSoundControlCode.Pause:
                    soundPcm.Pause();
                    break;

                case NativeSoundControlCode.Resume:
                    soundPcm.Resume();
                    break;

                case NativeSoundControlCode.Volume:
                    if (parameters == -1)
                    {
                        return (int)Math.Round(soundPcm.Volume * SND_MAXVOLUME);
                    }

                    soundPcm.Volume = Math.Clamp(parameters, SND_MINVOLUME, SND_MAXVOLUME) / (float)SND_MAXVOLUME;
                    break;

                case NativeSoundControlCode.MasterVolume:
                    if (parameters == -1)
                    {
                        return (int)Math.Round(system.AudioDriver.PcmMasterVolume * SND_MAXVOLUME);
                    }

                    system.AudioDriver.PcmMasterVolume = Math.Clamp(parameters, SND_MINVOLUME, SND_MAXVOLUME) / (float)SND_MAXVOLUME;
                    break;

                case NativeSoundControlCode.Freq:
                    if (parameters == -1)
                    {
                        return (int)soundPcm.Frequency;
                    }

                    if (parameters > 0)
                    {
                        soundPcm.Frequency = parameters;
                    }
                    break;

                case NativeSoundControlCode.Pan:
                    if (parameters == -1)
                    {
                        return (int)Math.Round(soundPcm.Pan * SND_MAXPAN);
                    }

                    soundPcm.Pan = Math.Clamp(parameters, -SND_MAXPAN, SND_MAXPAN) / (float)SND_MAXPAN;
                    break;

                case NativeSoundControlCode.Status:
                    return soundPcm.IsPlaying ? 1 : 0;

                case NativeSoundControlCode.StopLooping:
                    soundPcm.StopLooping();
                    break;

                case NativeSoundControlCode.Position:
                    if (parameters == -1)
                    {
                        return soundPcm.Position;
                    }

                    soundPcm.Position = parameters;
                    break;

                case NativeSoundControlCode.Priority:
                    if (parameters == -1)
                    {
                        return soundPcm.Priority;
                    }

                    soundPcm.Priority = parameters;
                    break;

                case NativeSoundControlCode.ChannelCount:
                    return system.AudioDriver.SoundConfig.numMixerChannels;

                default:
                    Logger.Error(LogClass.VSound, $"Unimplemented sound control command: {control}");
                    return SND_ERR;
            }

            return SND_OK;
        }

        // The play parameter carries the loop count in its low 16 bits: 0 plays
        // once, 0xFFFF loops until stopped, anything else repeats that many
        // extra times.
        private static int LoopCountFromPlayParameter(int parameters)
        {
            int loopCount = parameters & 0xFFFF;
            return (loopCount == 0xFFFF) ? -1 : loopCount;
        }

        public void Dispose()
        {
            foreach (var sound in soundManager)
            {
                sound.Dispose();
            }
        }
    }
}