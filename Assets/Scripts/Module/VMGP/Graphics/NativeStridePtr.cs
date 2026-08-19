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

using Nofun.VM;

namespace Nofun.Module.VMGP
{
    /// <summary>
    /// Graphics offset description (VMGPSTRIDEPTR in the Mophun SDK), used by
    /// vCopyRect to describe a rectangle either in memory or on the screen.
    /// </summary>
    public struct NativeStridePtr
    {
        /// <summary>Start of the graphics data. Null means the screen at (0, 0).</summary>
        public VMPtr<byte> ptr;

        /// <summary>Bytes to add to the pointer for the start location.</summary>
        public ushort xpan;

        /// <summary>Rows (times stride bytes) to add to the pointer for the start location.</summary>
        public ushort ypan;

        /// <summary>Bytes to add to get to the next line.</summary>
        public short stride;
    }
}
