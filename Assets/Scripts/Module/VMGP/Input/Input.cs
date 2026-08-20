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

using Nofun.Driver.Input;
using Nofun.Module.VMGPCaps;

namespace Nofun.Module.VMGP
{
    [Module]
    public partial class VMGP
    {
        private bool PointerInputAvailable => system.GameSetting.deviceModel.HasTouchscreen();

        [ModuleCall]
        private uint vGetButtonData()
        {
            uint data = system.InputDriver.GetButtonData();

            // Only expose the pointer buttons on emulated touchscreen devices,
            // matching the input capabilities the game was told about
            if (!PointerInputAvailable)
            {
                data &= ~(uint)(KeyCode.PointerDown | KeyCode.PointerAltDown);
            }

            return data;
        }

        [ModuleCall]
        private int vTestKey(uint key)
        {
            return system.InputDriver.KeyPressed(key) ? 1 : 0;
        }

        [ModuleCall]
        private uint vScanKeys()
        {
            return system.InputDriver.KeyScan;
        }

        [ModuleCall]
        private uint vGetPointerPos()
        {
            // Vertical position in the high 16 bits, horizontal in the low 16 bits
            return PointerInputAvailable ? system.InputDriver.PointerPos : 0;
        }
    }
}