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

using System;
using System.Runtime.InteropServices;

namespace Nofun.Driver.Graphics
{
    public static partial class DataConvertor
    {
        private static byte Expand4To8(int value)
        {
            return (byte)((value << 4) | value);
        }

        private static byte Expand5To8(int value)
        {
            return (byte)((value << 3) | (value >> 2));
        }

        /// <summary>
        /// Convert 16-bit-per-pixel word formats (RGB555, RGB444, ARGB1555)
        /// to ARGB8888.
        /// </summary>
        public static byte[] Word16ToARGB8888(Span<byte> data, int width, int height, TextureFormat format, bool zeroAsTransparent)
        {
            Span<ushort> pixels = MemoryMarshal.Cast<byte, ushort>(data);
            byte[] result = new byte[width * height * 4];

            for (int i = 0; i < width * height; i++)
            {
                ushort pixel = pixels[i];

                if (zeroAsTransparent && (pixel == 0))
                {
                    continue;
                }

                byte a = 255, r, g, b;

                switch (format)
                {
                    case TextureFormat.RGB444:
                        r = Expand4To8((pixel >> 8) & 0xF);
                        g = Expand4To8((pixel >> 4) & 0xF);
                        b = Expand4To8(pixel & 0xF);
                        break;

                    case TextureFormat.ARGB1555:
                        a = (byte)(((pixel >> 15) & 1) != 0 ? 255 : 0);
                        goto case TextureFormat.RGB555;

                    case TextureFormat.RGB555:
                        r = Expand5To8((pixel >> 10) & 0x1F);
                        g = Expand5To8((pixel >> 5) & 0x1F);
                        b = Expand5To8(pixel & 0x1F);
                        break;

                    default:
                        throw new ArgumentException($"Not a 16-bit word format: {format}");
                }

                result[i * 4] = a;
                result[i * 4 + 1] = r;
                result[i * 4 + 2] = g;
                result[i * 4 + 3] = b;
            }

            return result;
        }
    }
}
