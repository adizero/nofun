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
using Nofun.Util.Logging;
using Nofun.VM;

namespace Nofun.Module.VMGP3D
{
    [Module]
    public partial class VMGP3D
    {
        private VMSystem system;

        /// <summary>
        /// Texture cache, used to manage indefinite texture uploaded using vSetTexture.
        /// </summary>
        private TextureCache textureCache;

        /// <summary>
        /// Current active texture. A single texture is used to draw over primitives.
        /// </summary>
        private ITexture activeTexture;

        /// <summary>
        /// Handle of the active texture in the permanent texture manager.
        /// If this equals to zero, it means current texture, if not null, comes from texture cache.
        /// </summary>
        private uint activeTextureHandle;

        private float FixedToFloat(int fixedValue)
        {
            return fixedValue / 16384.0f;
        }

        private int FloatToFixed(float value)
        {
            return (int)(value * 16384);
        }

        private float Fixed9PointToFloat(int fixedValue)
        {
            return fixedValue / 1024.0f;
        }

        public VMGP3D(VMSystem system)
        {
            this.system = system;
            this.textureCache = new();
        }

        [ModuleCall]
        private void vInit3D()
        {
        }

        [ModuleCall]
        private void vSetViewport(int left, int top, int width, int height)
        {
            system.GraphicDriver.SetViewport(left, top, width, height);
        }

        [ModuleCall]
        private void vSetRenderState(uint key, uint value)
        {

        }

        [ModuleCall]
        private void vSetZBuffer(ushort value)
        {
            system.GraphicDriver.ClearDepth(value / ushort.MaxValue);
        }

        [ModuleCall]
        private int vSetTexture(VMPtr<byte> textureData, uint format, uint lods, uint mipCount)
        {
            try
            {
                activeTexture = textureCache.Get(system.GraphicDriver, textureData, system.Memory, (TextureFormat)format,
                    lods, mipCount, system.VMGPModule.ScreenPalette);

                activeTextureHandle = 0;

                return 1;
            }
            catch (System.Exception ex)
            {
                Logger.Error(LogClass.VMGP3D, $"Set texture failed with exception: {ex}");
                return 0;
            }
        }

        [ModuleCall]
        private void vDrawBillboard(VMPtr<NativeBillboard> billboardPtr)
        {
            NativeBillboard billboard = billboardPtr.Read(system.Memory);

            system.GraphicDriver.Set3DViewMatrix(currentMatrix);
            system.GraphicDriver.DrawBillboard(billboard, activeTexture);
        }
    }
}