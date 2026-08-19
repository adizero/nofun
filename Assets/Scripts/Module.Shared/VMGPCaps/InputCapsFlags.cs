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

namespace Nofun.Module.VMGPCaps
{
    /// <summary>
    /// Input capability flags, matching ICAPS_* in vmgpcaps.h of the Mophun SDK.
    /// </summary>
    public enum InputCapsFlags
    {
        Pointer = 0x0001,
        Joystick = 0x0002,
        Ascii = 0x0004,
        NumericKeypad = 0x0008,
        OnscreenControls = 0x0010
    }
}
