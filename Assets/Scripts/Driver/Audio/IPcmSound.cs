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

namespace Nofun.Driver.Audio
{
    public interface IPcmSound : IDisposable
    {
        // loopCount: 0 plays once, a positive value repeats that many extra
        // times, a negative value loops until stopped.
        void Play(int loopCount = 0);
        void Stop();
        void Pause();
        void Resume();

        // Finish the current pass and stop instead of wrapping again.
        void StopLooping();

        bool IsPlaying { get; }

        // 0..1
        float Volume { get; set; }

        // Playback rate in Hz, changeable while playing.
        float Frequency { get; set; }

        // -1 (left) .. 1 (right)
        float Pan { get; set; }

        // Playback position in frames.
        int Position { get; set; }

        int Priority { get; set; }
    }
}
