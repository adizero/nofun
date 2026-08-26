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

using System.Collections.Generic;
using System;
using Nofun.Util.Logging;

namespace Nofun.Util.Allocator
{
    public class BlockAllocator : ISpaceAllocator
    {
        private class BlockInfo
        {
            public long offset;
            public long size;

            public bool active;
        };

        // Blocks are kept sorted by offset and tile the used region [0, top)
        // contiguously (each block is either active or a free hole). Keeping
        // them sorted lets Free() coalesce adjacent free blocks, mirroring how
        // a real heap merges neighbouring free space. Without coalescing the
        // heap fragments: free bytes get stranded in small holes, allocations
        // that a real device would satisfy start failing, and titles that
        // watch vMemFree wrongly report a memory leak.
        private List<BlockInfo> blocks;
        private long maxSize;
        private long allocated;

        public BlockAllocator(long maxSize)
        {
            this.maxSize = maxSize;
            this.blocks = new();
        }

        public long Allocate(uint bytes)
        {
            long roundedSize = MemoryUtil.AlignUp(bytes, 4);
            long farthestEndOffset = 0;

            for (int i = 0; i < blocks.Count; i++)
            {
                BlockInfo block = blocks[i];
                farthestEndOffset = Math.Max(farthestEndOffset, block.offset + block.size);

                if (!block.active && block.size >= roundedSize)
                {
                    // Gonna use it right away
                    if (block.size == roundedSize)
                    {
                        block.active = true;
                        allocated += roundedSize;
                        return block.offset;
                    }

                    long returnValue = block.offset;

                    // Divide it to two: an active head and a free remainder.
                    BlockInfo newBlock = new BlockInfo()
                    {
                        active = true,
                        offset = block.offset,
                        size = roundedSize
                    };

                    // Change our old block
                    block.size -= roundedSize;
                    block.offset += roundedSize;

                    allocated += roundedSize;

                    // Keep the list sorted by offset: the active head goes
                    // immediately before its former block.
                    blocks.Insert(i, newBlock);
                    return returnValue;
                }
            }

            if (farthestEndOffset + roundedSize > maxSize)
            {
                return -1;
            }

            // We try our best, but we can't
            // We should alloc new block
            BlockInfo newBlockFin = new BlockInfo()
            {
                active = true,
                offset = farthestEndOffset,
                size = roundedSize
            };

            // Appended at the tail, so the sorted-by-offset invariant holds.
            blocks.Add(newBlockFin);
            allocated += roundedSize;

            return farthestEndOffset;
        }

        public void Free(long offset)
        {
            int index = blocks.FindIndex(block => block.offset == offset && block.active);

            if (index < 0)
            {
                Logger.Error(LogClass.VMGP3D, $"Can't find offset {offset}");
                return;
            }

            BlockInfo block = blocks[index];
            block.active = false;
            allocated -= block.size;

            // Coalesce with the following block when it is also free.
            if ((index + 1 < blocks.Count) && !blocks[index + 1].active)
            {
                block.size += blocks[index + 1].size;
                blocks.RemoveAt(index + 1);
            }

            // Coalesce with the preceding block when it is also free.
            if ((index > 0) && !blocks[index - 1].active)
            {
                blocks[index - 1].size += block.size;
                blocks.RemoveAt(index);
            }
        }

        public long AmountFree => Math.Max(maxSize - allocated, 0);
    }
}