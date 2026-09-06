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

using Nofun.Driver.Graphics;
using Nofun.Util;
using System;
using System.Collections.Generic;

namespace Nofun.Module.VMGP
{
    // Bitmap system font behind vSelectFont, vTextOut and vTextExtent.
    //
    // The original handsets drew system text with crisp one-bit pixel fonts, and
    // games laid their menus out around those glyphs. Rendering a vector font into
    // the small guest screen instead gave soft, misaligned glyphs. This keeps three
    // fixed pixel faces (small, normal, large), trims each glyph to its used
    // columns for proportional spacing, synthesizes bold by widening strokes one
    // pixel to the right, and draws the effect flags (outline, shadows, underline)
    // the way the handsets did: extra passes in the background colour.
    public class SystemFont
    {
        private const int GlyphCount = 256;
        private const int GlyphsPerAtlasRow = 16;
        private const int HandsetPixelsPerInch = 130;

        private struct Glyph
        {
            public int column;
            public int width;
            public int advance;
        }

        private class Variant
        {
            public SystemFontData.Face face;
            public int cellWidth;
            public int atlasWidth;
            public int atlasHeight;
            public Glyph[] glyphs;
            public byte[] pixels;
            public ITexture atlas;
        }

        private readonly Dictionary<int, Variant> variants = new();
        private readonly List<int> glyphPosition = new() { 0, 0 };

        private Variant current;
        private uint currentFlags;

        public SystemFont()
        {
            Select((uint)SystemFontSize.Normal, (uint)SystemFontStyle.Normal);
        }

        public void Select(uint size, uint flags)
        {
            SystemFontData.Face face = PickFace(size);
            bool bold = BitUtil.FlagSet(flags, SystemFontStyle.Bold);
            bool monospace = BitUtil.FlagSet(flags, SystemFontStyle.Monospace);

            int key = (face.cellHeight << 2) | (bold ? 2 : 0) | (monospace ? 1 : 0);
            if (!variants.TryGetValue(key, out current))
            {
                current = BuildVariant(face, bold, monospace);
                variants.Add(key, current);
            }

            currentFlags = flags;
        }

        // Width in the low 16 bits and line height in the high 16 bits, matching
        // what vTextExtent returns.
        public int Extent(string text)
        {
            int width = 0;
            foreach (char c in text)
            {
                width += current.glyphs[GlyphIndex(c)].advance;
            }

            return (ushort)width | ((ushort)current.face.cellHeight << 16);
        }

        public void Draw(IGraphicDriver driver, int x, int y, string text, SColor foreColor, SColor backColor)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (current.atlas == null)
            {
                current.atlas = driver.CreateTexture(current.pixels, current.atlasWidth, current.atlasHeight, 1, TextureFormat.ARGB8888);
                current.pixels = null;
            }

            switch ((SystemFontStyle)(currentFlags & (uint)SystemFontStyle.EffectMask))
            {
                case SystemFontStyle.OutlineEffect:
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if ((dx != 0) || (dy != 0))
                            {
                                DrawRun(driver, x + dx, y + dy, text, backColor);
                            }
                        }
                    }
                    break;

                case SystemFontStyle.ShadowLowerRightEffect:
                    DrawRun(driver, x + 1, y + 1, text, backColor);
                    break;

                case SystemFontStyle.ShadowLowerLeftEffect:
                    DrawRun(driver, x - 1, y + 1, text, backColor);
                    break;

                case SystemFontStyle.ShadowUpperRightEffect:
                    DrawRun(driver, x + 1, y - 1, text, backColor);
                    break;

                case SystemFontStyle.ShadowUpperLeftEffect:
                    DrawRun(driver, x - 1, y - 1, text, backColor);
                    break;
            }

            int width = DrawRun(driver, x, y, text, foreColor);

            if (BitUtil.FlagSet(currentFlags, SystemFontStyle.Underline))
            {
                int underlineY = y + current.face.ascent + 1;
                driver.FillRect(x, underlineY, x + width, underlineY + 1, foreColor);
            }
        }

        private int DrawRun(IGraphicDriver driver, int x, int y, string text, SColor color)
        {
            int startX = x;

            foreach (char c in text)
            {
                int code = GlyphIndex(c);
                Glyph glyph = current.glyphs[code];

                if (glyph.width > 0)
                {
                    glyphPosition[0] = (code % GlyphsPerAtlasRow) * current.cellWidth + glyph.column;
                    glyphPosition[1] = (code / GlyphsPerAtlasRow) * current.face.cellHeight;

                    driver.DrawText(x, y, glyph.width, current.face.cellHeight, glyphPosition, current.atlas,
                        TextDirection.Horizontal, color);
                }

                x += glyph.advance;
            }

            return x - startX;
        }

        private static int GlyphIndex(char c)
        {
            return (c < GlyphCount) ? c : '?';
        }

        private static SystemFontData.Face PickFace(uint size)
        {
            if (BitUtil.FlagSet(size, SystemFontSize.PixelFlag))
            {
                return NearestFace((int)(size & ~(uint)SystemFontSize.PixelFlag));
            }

            if (BitUtil.FlagSet(size, SystemFontSize.PointFlag))
            {
                int points = (int)(size & ~(uint)SystemFontSize.PointFlag);
                return NearestFace(points * HandsetPixelsPerInch / 72);
            }

            switch ((SystemFontSize)size)
            {
                case SystemFontSize.Small:
                    return SystemFontData.Small;

                case SystemFontSize.Large:
                    return SystemFontData.Large;

                default:
                    return SystemFontData.Normal;
            }
        }

        private static SystemFontData.Face NearestFace(int pixelHeight)
        {
            SystemFontData.Face best = SystemFontData.Normal;
            int bestDistance = int.MaxValue;

            foreach (SystemFontData.Face face in new[] { SystemFontData.Small, SystemFontData.Normal, SystemFontData.Large })
            {
                int distance = Math.Abs(face.cellHeight - pixelHeight);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = face;
                }
            }

            return best;
        }

        // Decode a face into a 16x16 glyph atlas (white pixels, alpha as coverage)
        // and measure each glyph's used columns for proportional advances.
        private static Variant BuildVariant(SystemFontData.Face face, bool bold, bool monospace)
        {
            byte[] rows = Convert.FromBase64String(face.rows);

            int cellWidth = face.cellWidth + (bold ? 1 : 0);
            int cellHeight = face.cellHeight;
            int atlasWidth = cellWidth * GlyphsPerAtlasRow;
            int atlasHeight = cellHeight * (GlyphCount / GlyphsPerAtlasRow);
            int spaceAdvance = (face.cellWidth + 1) / 2 + (bold ? 1 : 0);

            byte[] pixels = new byte[atlasWidth * atlasHeight * 4];
            Glyph[] glyphs = new Glyph[GlyphCount];

            for (int code = 0; code < GlyphCount; code++)
            {
                int cellX = (code % GlyphsPerAtlasRow) * cellWidth;
                int cellY = (code / GlyphsPerAtlasRow) * cellHeight;

                int minColumn = cellWidth;
                int maxColumn = -1;

                for (int y = 0; y < cellHeight; y++)
                {
                    int bits = rows[code * cellHeight + y];
                    if (bold)
                    {
                        bits |= bits >> 1;
                    }

                    for (int x = 0; x < cellWidth; x++)
                    {
                        if ((bits & (0x80 >> x)) == 0)
                        {
                            continue;
                        }

                        int offset = ((cellY + y) * atlasWidth + cellX + x) * 4;
                        pixels[offset] = 255;
                        pixels[offset + 1] = 255;
                        pixels[offset + 2] = 255;
                        pixels[offset + 3] = 255;

                        minColumn = Math.Min(minColumn, x);
                        maxColumn = Math.Max(maxColumn, x);
                    }
                }

                Glyph glyph;
                if (monospace)
                {
                    glyph.column = 0;
                    glyph.width = cellWidth;
                    glyph.advance = cellWidth;
                }
                else if (maxColumn < 0)
                {
                    glyph.column = 0;
                    glyph.width = 0;
                    glyph.advance = spaceAdvance;
                }
                else
                {
                    glyph.column = minColumn;
                    glyph.width = maxColumn - minColumn + 1;
                    glyph.advance = glyph.width + 1;
                }

                glyphs[code] = glyph;
            }

            return new Variant
            {
                face = face,
                cellWidth = cellWidth,
                atlasWidth = atlasWidth,
                atlasHeight = atlasHeight,
                glyphs = glyphs,
                pixels = pixels
            };
        }
    }
}
