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
using System.Collections.Generic;
using System.IO;
using Nofun.Util;
using Nofun.Util.Logging;
using UnityEngine;
using Logger = Nofun.Util.Logging.Logger;

namespace Nofun.Parser
{
    /// <summary>
    /// Turns a raw imported Mophun executable into the plain, uncompressed form
    /// the runtime expects: encrypted titles are decrypted and compressed ones
    /// are inflated, transparently, while adding to the library.
    /// </summary>
    public static class VMExecutableProcessor
    {
        private const string KeyResourceRoot = "Keys";
        private const int PoolItemSize = 8;

        public struct ProcessResult
        {
            public byte[] data;
            public bool wasCompressed;
            public bool wasEncrypted;
            public long originalSize;
            public long processedSize;
        }

        private static VMExecutableCrypto.Keys[] cachedKeys;

        private static byte[] LoadKeyBytes(string name)
        {
            var asset = Resources.Load<TextAsset>($"{KeyResourceRoot}/{name}");
            return (asset != null) ? asset.bytes : null;
        }

        private static VMExecutableCrypto.Keys[] LoadKeys()
        {
            if (cachedKeys != null)
            {
                return cachedKeys;
            }

            var selector = VMExecutableCrypto.ToXteaKey(LoadKeyBytes("selectorKey"));
            var moduliKey = VMExecutableCrypto.ToXteaKey(LoadKeyBytes("keyDecKey"));

            var sets = new List<VMExecutableCrypto.Keys>();

            if (selector != null)
            {
                // SE titles: moduli are used directly (reference tool's -n path).
                byte[] se = LoadKeyBytes("se");
                if ((se != null) && (se.Length == 512))
                {
                    sets.Add(new VMExecutableCrypto.Keys()
                    {
                        bigKeys = se,
                        selectorKey = selector,
                        moduliKey = null
                    });
                }

                // PPC/archos titles: moduli need decrypting first, so they are
                // only usable once keyDecKey is present.
                byte[] ppc = LoadKeyBytes("ppc");
                if ((ppc != null) && (ppc.Length == 512) && (moduliKey != null))
                {
                    sets.Add(new VMExecutableCrypto.Keys()
                    {
                        bigKeys = ppc,
                        selectorKey = selector,
                        moduliKey = moduliKey
                    });
                }
            }

            cachedKeys = sets.ToArray();
            return cachedKeys;
        }

        private static bool IsCompressed(byte[] data)
        {
            // The compressed flag rides in the high byte of the stack-size word
            // (file offset 11); bit 0x80 marks a compressed executable.
            return (data.Length > 11) && ((data[11] & 0x80) != 0);
        }

        private static bool HasValidMagic(byte[] data)
        {
            return (data.Length >= 4) && (data[0] == 'V') && (data[1] == 'M') && (data[2] == 'G') && (data[3] == 'P');
        }

        public static ProcessResult Process(byte[] raw)
        {
            var result = new ProcessResult()
            {
                data = raw,
                originalSize = raw.LongLength,
                processedSize = raw.LongLength
            };

            if (!HasValidMagic(raw))
            {
                throw new VMGPInvalidHeaderException("The magic is wrong!");
            }

            byte[] working = raw;
            bool compressed = IsCompressed(working);
            result.wasCompressed = compressed;

            var detect = VMExecutableCrypto.DetectResult.Empty;
            foreach (var keys in LoadKeys())
            {
                detect = VMExecutableCrypto.Detect(working, compressed, keys);
                if (detect.encrypted)
                {
                    break;
                }
            }

            result.wasEncrypted = detect.encrypted;

            if (detect.encrypted)
            {
                working = VMExecutableCrypto.Decrypt(working, compressed, detect.symmetricKey);
                Logger.Trace(LogClass.Loader, "Decrypted an encrypted Mophun executable during import.");
            }

            if (compressed)
            {
                working = Decompress(working);
                Logger.Trace(LogClass.Loader, "Decompressed a compressed Mophun executable during import.");
            }

            result.data = working;
            result.processedSize = working.LongLength;

            return result;
        }

        /// <summary>
        /// Rebuilds a compressed executable into its plain layout, inflating the
        /// code, data, pool and string sections (resources are stored raw).
        /// </summary>
        private static byte[] Decompress(byte[] data)
        {
            uint codeSize = BitConverter.ToUInt32(data, 12);
            uint dataSize = BitConverter.ToUInt32(data, 16);
            uint resSize = BitConverter.ToUInt32(data, 24);
            uint poolSize = BitConverter.ToUInt32(data, 32);
            uint stringSize = BitConverter.ToUInt32(data, 36);

            using (var output = new MemoryStream())
            {
                // Header: keep offsets 0..10, clear the compressed bit at 11,
                // then copy the remaining header while zeroing the (now unused)
                // resources file-offset field at offset 28.
                output.Write(data, 0, 11);
                output.WriteByte((byte)(data[11] & ~0x80));
                output.Write(data, 12, 16);
                output.Write(new byte[4], 0, 4);
                output.Write(data, 32, VMGPHeader.TotalSize - 32);

                int cursor = VMGPHeader.TotalSize;

                cursor = WriteSection(data, cursor, (int)codeSize, output);
                cursor = WriteSection(data, cursor, (int)dataSize, output);

                output.Write(data, cursor, (int)resSize);
                cursor += (int)resSize;

                cursor = WriteSection(data, cursor, (int)(poolSize * PoolItemSize), output);
                cursor = WriteSection(data, cursor, (int)stringSize, output);

                return output.ToArray();
            }
        }

        private static int WriteSection(byte[] data, int cursor, int desiredSize, MemoryStream output)
        {
            int size = BitConverter.ToInt32(data, cursor);
            cursor += 4;

            if (size == 0)
            {
                output.Write(data, cursor, desiredSize);
                return cursor + desiredSize;
            }

            if ((data[cursor] != 'L') || (data[cursor + 1] != 'Z'))
            {
                throw new InvalidDataException("Compressed section is missing its LZ magic.");
            }

            byte maxOffsetBits = data[cursor + 2];
            byte extendedOffsetBits = data[cursor + 3];
            uint innerLength = BitConverter.ToUInt32(data, cursor + 4);

            var inflated = new byte[desiredSize];
            int innerTarget = Math.Min((int)innerLength, desiredSize);

            var compressed = new Memory<byte>(data, cursor + 0x16, size - 0x16);
            CompressionUtil.TryLZDecompressContent(new MemoryBitStream(compressed),
                new Span<byte>(inflated, 0, innerTarget), extendedOffsetBits, maxOffsetBits);

            output.Write(inflated, 0, desiredSize);
            return cursor + size;
        }
    }
}
