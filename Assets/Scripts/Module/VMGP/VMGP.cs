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

using Nofun.Util.Logging;
using Nofun.Util.Allocator;
using Nofun.Settings;
using Nofun.VM;
using System;

namespace Nofun.Module.VMGP
{
    [Module]
    public partial class VMGP
    {
        private const ushort MajorAPIVersion = 2;
        private const ushort MinorAPIVersion50 = 50;
        private const ushort MinorAPIVersion30 = 30;

        private VM.VMSystem system;

        public VMGP(VM.VMSystem system)
        {
            this.system = system;
            this.fontCache = new();
            this.spriteCache = new();
            this.tilemapCache = new();
            this.heapAllocator = new BlockAllocator(system.HeapSize);
            Util.Logging.Logger.Warning(Util.Logging.LogClass.VMGPSystem, $"Guest heap size = {system.HeapSize} bytes");

            InitializeTasks();
        }

        public void OnSystemLoaded()
        {
        }

        [ModuleCall]
        private void DbgPrintf(VMString format)
        {
            // The single named argument takes up 4 bytes, varargs follow
            int argOffset = 4;
            Logger.Debug(LogClass.GameTTY, FormatString(format.Get(system.Memory), () => ReadVarArg(ref argOffset)));
        }

        [ModuleCall]
        private void vYieldToSystem()
        {
        }

        [ModuleCall]
        private ushort vSwap16(ushort value)
        {
            return (ushort)((value >> 8) | (value << 8));
        }

        [ModuleCall]
        private uint vSwap32(uint value)
        {
            return (value >> 24) | ((value >> 8) & 0xFF00) | ((value << 8) & 0xFF0000) | (value << 24);
        }

        [ModuleCall]
        private void vSwap(VMPtr<byte> ptr, uint count, uint size)
        {
            if ((size != 2) && (size != 4))
            {
                return;
            }

            Span<byte> data = ptr.AsSpan(system.Memory, (int)(count * size));

            for (int i = 0; i < count; i++)
            {
                data.Slice(i * (int)size, (int)size).Reverse();
            }
        }

        [ModuleCall]
        private uint vGetVMGPInfo()
        {
            return ((uint)MajorAPIVersion << 16) | ((system.Version == SystemVersion.Version150) ? MinorAPIVersion50 : MinorAPIVersion30);
        }

        [ModuleCall]
        private uint vUID()
        {
            return 0xDEADBEEF;
        }

        [ModuleCall]
        private void vTerminateVMGP()
        {
            system.Stop();
        }
    }
}