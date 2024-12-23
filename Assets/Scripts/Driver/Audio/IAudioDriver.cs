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

using System;
using System.IO;
using Nofun.Module.VMGPCaps;

namespace Nofun.Driver.Audio
{
    public interface IAudioDriver
    {
        public ISound PlaySound(SoundType type, Span<byte> data, bool loop);

        public uint Capabilities { get; }

        public SoundConfig SoundConfig { get; }

        public bool InitializePCMPlay();

        public IPcmSound LoadPCMSound(Span<byte> data, int priority, int frequency, int channelCount,
            int bitsPerSample, bool isAdpcm);

        public IMusic LoadMusic(Stream musicData);
    }
}