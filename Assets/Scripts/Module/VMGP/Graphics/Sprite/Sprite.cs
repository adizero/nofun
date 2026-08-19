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

using Nofun.Driver.Graphics;
using Nofun.Util;
using Nofun.Util.Logging;
using Nofun.VM;
using System;

namespace Nofun.Module.VMGP
{
    [Module]
    public partial class VMGP
    {
        private class SpriteSlot
        {
            public VMPtr<NativeSprite> sprite;
            public short x;
            public short y;
        }

        private SpriteSlot[] spriteSlots;
        private SpriteCache spriteCache;

        [ModuleCall]
        private uint vSpriteInit(byte count)
        {
            if ((spriteSlots != null) && (spriteSlots.Length == count))
            {
                return 1;
            }

            if (spriteSlots != null)
            {
                Logger.Warning(LogClass.VMGPGraphic, "Sprite slots have already been initialized! Re-allocating");
            }

            spriteSlots = new SpriteSlot[count];
            return 1;
        }

        [ModuleCall]
        private void vSpriteDispose()
        {
            spriteSlots = null;
        }

        [ModuleCall]
        private void vSpriteClear()
        {
            for (int i = 0; i < spriteSlots.Length; i++)
            {
                if (spriteSlots[i] != null)
                {
                    spriteSlots[i].sprite = VMPtr<NativeSprite>.Null;
                }
            }
        }

        [ModuleCall]
        private void vSpriteSet(byte slot, VMPtr<NativeSprite> sprite, short x, short y)
        {
            if (slot >= spriteSlots.Length)
            {
                Logger.Warning(LogClass.VMGPGraphic, $"Trying to set sprite slot={slot} to sprite array length={spriteSlots.Length}!");
                return;
            }

            SpriteSlot slotData = spriteSlots[slot];

            if (slotData == null)
            {
                slotData = new SpriteSlot()
                {
                    sprite = sprite,
                    x = x,
                    y = y
                };

                spriteSlots[slot] = slotData;
            }
            else
            {
                slotData.sprite = sprite;
                slotData.x = x;
                slotData.y = y;
            }
        }

        [ModuleCall]
        private void vUpdateSprite()
        {
            foreach (SpriteSlot slot in spriteSlots)
            {
                if (slot != null)
                {
                    vDrawObject(slot.x, slot.y, slot.sprite);
                }
            }
        }

        [ModuleCall]
        private short vSpriteCollision(byte slotCheck, byte from, byte to)
        {
            if ((slotCheck >= spriteSlots.Length) || (spriteSlots[slotCheck].sprite.IsNull))
            {
                return -1;
            }

            NativeSprite checkSprite = spriteSlots[slotCheck].sprite.Read(system.Memory);
            NRectangle checkCollider = new NRectangle(spriteSlots[slotCheck].x, spriteSlots[slotCheck].y,
                    checkSprite.width, checkSprite.height);

            for (int i = from; i <= Math.Min((int)to, spriteSlots.Length - 1); i++)
            {
                if ((spriteSlots[i] == null) || (spriteSlots[i].sprite.IsNull))
                {
                    continue;
                }

                NativeSprite sprite = spriteSlots[i].sprite.Read(system.Memory);
                NRectangle colliderSprite = new NRectangle(spriteSlots[i].x, spriteSlots[i].y,
                    sprite.width, sprite.height);

                if (checkCollider.Collide(colliderSprite))
                {
                    return (short)i;
                }
            }

            return -1;
        }

        [ModuleCall]
        private short vSpriteBoxCollision(VMPtr<VMGPRect> boxPtr, byte from, byte to)
        {
            VMGPRect box = boxPtr.Read(system.Memory);
            NRectangle boxN = box.ToNofunRectangle();

            for (int i = from; i <= Math.Min((int)to, spriteSlots.Length - 1); i++)
            {
                if ((spriteSlots[i] == null) || (spriteSlots[i].sprite.IsNull))
                {
                    continue;
                }

                NativeSprite sprite = spriteSlots[i].sprite.Read(system.Memory);
                NRectangle colliderSprite = new NRectangle(spriteSlots[i].x, spriteSlots[i].y,
                    sprite.width, sprite.height);

                if (boxN.Collide(colliderSprite))
                {
                    return (short)i;
                }
            }

            return -1;
        }

        [ModuleCall]
        private void vDrawTile(VMPtr<byte> data, int format, short x, short y)
        {
            if (data.IsNull)
            {
                return;
            }

            // Bits 0-2 carry the pixel format, bit 3 the transparent flag and
            // bits 8-15 the palette offset. Tiles are always 8x8 pixels.
            NativeSprite tileInfo = new NativeSprite()
            {
                format = (byte)(format & 0x7),
                paletteOffset = (byte)((format >> 8) & 0xFF),
                width = 8,
                height = 8
            };

            bool transparent = (format & 0x8) != 0;

            long tileSizeInBits = TextureUtil.GetTextureSizeInBits(tileInfo.width, tileInfo.height,
                (TextureFormat)tileInfo.format);

            Span<byte> tileData = data.AsSpan(system.Memory, (int)((tileSizeInBits + 7) / 8));

            try
            {
                ITexture drawTexture = spriteCache.Retrieve(system.GraphicDriver, tileInfo, tileData,
                    ScreenPalette, transparent);

                system.GraphicDriver.DrawTexture(x, y, 0, 0, 0, drawTexture);
            }
            catch (Exception ex)
            {
                Logger.Error(LogClass.VMGPGraphic, $"Draw tile failed with error: {ex}");
            }
        }

        [ModuleCall]
        private void vDrawObject(short x, short y, VMPtr<NativeSprite> sprite)
        {
            if (sprite.IsNull)
            {
                return;
            }

            NativeSprite spriteInfo = sprite.Read(system.Memory);
            if ((spriteInfo.width == 0) || (spriteInfo.height == 0))
            {
                return;
            }

            long spriteSizeInBits = TextureUtil.GetTextureSizeInBits(spriteInfo.width, spriteInfo.height,
                (TextureFormat)spriteInfo.format);

            int spriteSizeInBytes = (int)((spriteSizeInBits + 7) / 8);

            Span<byte> spriteData = sprite[1].Cast<byte>().AsSpan(system.Memory, spriteSizeInBytes);

            try
            {
                bool transparent = BitUtil.FlagSet(currentTransferMode, TransferMode.Transparent);

                ITexture drawTexture = spriteCache.Retrieve(system.GraphicDriver, spriteInfo, spriteData,
                    ScreenPalette, transparent);

                // Palette and RGB332 sprites get color 0 knocked out during conversion.
                // Direct-color formats without alpha are uploaded as-is, so transparency
                // (color 0 = black) has to be handled at draw time instead.
                TextureFormat format = (TextureFormat)spriteInfo.format;
                bool colorKeyedAtDraw = transparent &&
                    ((format == TextureFormat.RGB565) || (format == TextureFormat.RGB888));

                // Draw it to the screen
                system.GraphicDriver.DrawTexture(x, y, spriteInfo.centerX, spriteInfo.centerY,
                    0, drawTexture, blackAsTransparent: colorKeyedAtDraw,
                    flipX: BitUtil.FlagSet(currentTransferMode, TransferMode.FlipX),
                    flipY: BitUtil.FlagSet(currentTransferMode, TransferMode.FlipY));
            }
            catch (Exception ex)
            {
                Logger.Error(LogClass.VMGPGraphic, $"Draw object failed with error: {ex}");
            }
        }
    }
}