/*
 * (C) 2026 Radrat Softworks
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
using Nofun.Driver.Audio.Converter;
using System;

namespace Nofun.Driver.Unity.Audio
{
    // A PCM sound mixed in software by the audio driver's filter callback.
    //
    // Decoding to float once and resampling per callback (rather than handing
    // the data to an AudioSource) is what makes the header loop points, loop
    // counts, pan, master volume and live frequency changes possible; racers
    // for example pitch their engine sample every frame through the frequency
    // control while it loops.
    public class PcmVoice : IPcmSound
    {
        private readonly AudioDriver driver;
        private readonly object gate = new();

        private readonly float[] samples;
        private readonly int channels;
        private readonly int frameCount;
        private readonly int loopStart;
        private readonly int loopEnd;

        private double position;
        private float frequency;
        private float volume = 1.0f;
        private float pan = 0.0f;
        private int loopsRemaining;
        private bool playing;
        private bool paused;

        public int Priority { get; set; }

        public PcmVoice(AudioDriver driver, ReadOnlySpan<byte> data, int frequency, int channels, int bitsPerSample,
            bool isAdpcm, int loopStartFrame, int loopEndFrame, int priority)
        {
            this.driver = driver;
            this.channels = Math.Max(1, channels);
            this.frequency = Math.Max(1, frequency);

            samples = Decode(data, bitsPerSample, isAdpcm);
            frameCount = samples.Length / this.channels;

            // Loop points are in frames; an unset or out-of-range end means the
            // whole sample loops.
            loopStart = Math.Clamp(loopStartFrame, 0, Math.Max(0, frameCount - 1));
            loopEnd = ((loopEndFrame <= loopStart) || (loopEndFrame > frameCount)) ? frameCount : loopEndFrame;

            Priority = priority;
        }

        private static float[] Decode(ReadOnlySpan<byte> data, int bitsPerSample, bool isAdpcm)
        {
            if (isAdpcm)
            {
                short[] decoded = ADPCMToPcm.Convert(data.ToArray());
                float[] result = new float[decoded.Length];
                for (int i = 0; i < decoded.Length; i++)
                {
                    result[i] = decoded[i] / 32768.0f;
                }

                return result;
            }

            switch (bitsPerSample)
            {
                case 8:
                {
                    // 8-bit samples are unsigned with silence at 128.
                    float[] result = new float[data.Length];
                    for (int i = 0; i < data.Length; i++)
                    {
                        result[i] = (data[i] - 128) / 128.0f;
                    }

                    return result;
                }

                case 16:
                {
                    // 16-bit samples are signed little endian.
                    float[] result = new float[data.Length / 2];
                    for (int i = 0; i < result.Length; i++)
                    {
                        short sample = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
                        result[i] = sample / 32768.0f;
                    }

                    return result;
                }

                default:
                    throw new ArgumentException($"Sample bits must be 8 or 16 for PCM audio, got {bitsPerSample}!");
            }
        }

        // Mix this voice into an interleaved float buffer running at outputRate.
        public void Render(float[] dest, int destChannels, int outputRate, float masterVolume)
        {
            lock (gate)
            {
                if (!playing || paused || (frameCount == 0))
                {
                    return;
                }

                double step = frequency / (double)outputRate;
                float gain = volume * masterVolume;
                float leftGain = gain * ((pan > 0.0f) ? (1.0f - pan) : 1.0f);
                float rightGain = gain * ((pan < 0.0f) ? (1.0f + pan) : 1.0f);

                int destFrames = dest.Length / destChannels;

                for (int i = 0; i < destFrames; i++)
                {
                    int limit = (loopsRemaining != 0) ? loopEnd : frameCount;

                    if (position >= limit)
                    {
                        if (loopsRemaining != 0)
                        {
                            if (loopsRemaining > 0)
                            {
                                loopsRemaining--;
                            }

                            position = loopStart + (position - limit);
                            if ((position < loopStart) || (position >= loopEnd))
                            {
                                position = loopStart;
                            }
                        }
                        else
                        {
                            playing = false;
                            position = 0;
                            break;
                        }
                    }

                    int frame = (int)position;
                    float fraction = (float)(position - frame);
                    int nextFrame = Math.Min(frame + 1, frameCount - 1);

                    float left;
                    float right;

                    if (channels >= 2)
                    {
                        left = Lerp(samples[frame * channels], samples[nextFrame * channels], fraction);
                        right = Lerp(samples[frame * channels + 1], samples[nextFrame * channels + 1], fraction);
                    }
                    else
                    {
                        left = Lerp(samples[frame], samples[nextFrame], fraction);
                        right = left;
                    }

                    dest[i * destChannels] += left * leftGain;
                    if (destChannels > 1)
                    {
                        dest[i * destChannels + 1] += right * rightGain;
                    }

                    position += step;
                }
            }
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        public void Play(int loopCount = 0)
        {
            lock (gate)
            {
                loopsRemaining = loopCount;
                position = 0;
                paused = false;
                playing = true;
            }
        }

        public void Stop()
        {
            lock (gate)
            {
                playing = false;
                paused = false;
                position = 0;
            }
        }

        public void Pause()
        {
            lock (gate)
            {
                if (playing)
                {
                    paused = true;
                }
            }
        }

        public void Resume()
        {
            lock (gate)
            {
                paused = false;
            }
        }

        public void StopLooping()
        {
            lock (gate)
            {
                loopsRemaining = 0;
            }
        }

        public bool IsPlaying
        {
            get
            {
                lock (gate)
                {
                    return playing && !paused;
                }
            }
        }

        public float Volume
        {
            get => volume;
            set => volume = Math.Clamp(value, 0.0f, 1.0f);
        }

        public float Frequency
        {
            get => frequency;
            set
            {
                if (value > 0.0f)
                {
                    frequency = value;
                }
            }
        }

        public float Pan
        {
            get => pan;
            set => pan = Math.Clamp(value, -1.0f, 1.0f);
        }

        public int Position
        {
            get
            {
                lock (gate)
                {
                    return (int)position;
                }
            }
            set
            {
                lock (gate)
                {
                    position = Math.Clamp(value, 0, Math.Max(0, frameCount - 1));
                }
            }
        }

        public void Dispose()
        {
            Stop();
            driver.RemovePcmVoice(this);
        }
    }
}
