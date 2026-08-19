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

namespace Nofun.Module.VMGPCaps
{
    public enum VideoCapsFlag
    {
        Render3D = 0x1000,
        ChangingOrientation = 0x2000,
        All = Render3D | ChangingOrientation
    };

    /// <summary>
    /// Screen output format, reported in the least significant 8 bits of
    /// the video capability flags. Values match VCAPS_* in vmgpvfmt.h of
    /// the Mophun SDK.
    /// </summary>
    public enum VideoScreenFormat
    {
        Gray2 = 0,
        Gray4 = 1,
        Gray16 = 2,
        Indexed2 = 3,
        Indexed4 = 4,
        Indexed16 = 5,
        Indexed256 = 6,
        Rgb332 = 7,
        Rgb565 = 8,
        Rgb555 = 9,
        Rgb888 = 10,
        Rgba8888 = 11,
        Rgb444 = 12
    };
}