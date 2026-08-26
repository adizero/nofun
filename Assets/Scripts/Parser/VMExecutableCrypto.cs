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
using System.Numerics;

namespace Nofun.Parser
{
    /// <summary>
    /// Decrypts Mophun SE executables. The meta resource carries an RSA-signed
    /// blob (exponent 3, per-title 1024-bit modulus picked from a key table by
    /// a XTEA-encrypted selector); recovering it yields the symmetric key used
    /// to scramble the code section.
    /// </summary>
    public static class VMExecutableCrypto
    {
        private const int MetaBlobSize = 0x98;
        private const uint MetaMagic = 0x4154454D; // "META"
        private const uint CompressedCodeXorSeed = 0xABADCEED;

        public class Keys
        {
            // 512-byte table of four 128-byte moduli.
            public byte[] bigKeys;

            // 16-byte XTEA key protecting the key selector.
            public ushort[] selectorKey;

            // 16-byte XTEA key protecting the moduli (only used by non-SE
            // titles; SE runs with the -n / skip-key-decryption path).
            public ushort[] moduliKey;

            public bool Valid => (bigKeys != null) && (bigKeys.Length == 512) && (selectorKey != null);
        }

        public struct DetectResult
        {
            public bool encrypted;
            public uint[] symmetricKey;

            public static DetectResult Empty => new DetectResult();
        }

        public static ushort[] ToXteaKey(byte[] raw)
        {
            if ((raw == null) || (raw.Length != 16))
            {
                return null;
            }

            var key = new ushort[8];
            for (int i = 0; i < 8; i++)
            {
                key[i] = (ushort)(raw[i * 2] | (raw[i * 2 + 1] << 8));
            }

            return key;
        }

        private static uint XteaDecrypt(uint inp, ushort[] key)
        {
            uint sum = 0xC6EF3720;
            uint v0 = (ushort)inp;
            uint v1 = (inp >> 16) & 0xFFFF;

            for (int i = 32; i > 0; --i)
            {
                v1 -= (((uint)(ushort)(16 * v0) ^ ((v0 << 16) >> 21)) + v0)
                    ^ (uint)(ushort)(key[2 * ((sum << 19) >> 30)] + sum);
                sum -= 0x9E3779B9;
                v0 -= (((uint)(ushort)(16 * v1) ^ ((v1 << 16) >> 21)) + v1)
                    ^ (uint)(ushort)(key[2 * (sum & 3)] + sum);
            }

            return (uint)(((ushort)v1 << 16) | (ushort)v0);
        }

        /// <summary>
        /// The bespoke block cipher applied word-by-word to the code section.
        /// Ported verbatim from the reference decryptor's register trace.
        /// </summary>
        private static uint DecryptBlock(uint block, uint[] key)
        {
            uint r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10;
            r4 = block;
            r10 = key[0];
            r0 = r4 << 16;
            r8 = key[1];
            r5 = r0 >> 16;
            r7 = key[2];
            r9 = key[3];
            r0 = r5 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r5 << 16;
            r0 = r3 ^ (r1 >> 21);
            r3 = r0 + r5;
            r1 = r7 + 0x5400;
            r0 = r1 + 0xF;
            r2 = r0 << 16;
            r1 = r3 ^ (r2 >> 16);
            r4 = (r4 >> 16) - r1;
            r0 = r4 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r4 << 16;
            r0 = r3 ^ (r1 >> 21);
            r3 = r0 + r4;
            r1 = r7 + 0xDA00;
            r0 = r1 + 0x56;
            r2 = r0 << 16;
            r1 = r3 ^ (r2 >> 16);
            r5 = r5 - r1;
            r0 = r5 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r5 << 16;
            r0 = r3 ^ (r1 >> 21);
            r3 = r0 + r5;
            r1 = r9 + 0xDA00;
            r0 = r1 + 0x56;
            r2 = r0 << 16;
            r1 = r3 ^ (r2 >> 16);
            r4 = r4 - r1;
            r0 = r4 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r4 << 16;
            r0 = r3 ^ (r1 >> 21);
            r3 = r0 + r4;
            r1 = r8 + 0x6000;
            r0 = r1 + 0x9D;
            r2 = r0 << 16;
            r1 = r3 ^ (r2 >> 16);
            r6 = r5 - r1;
            r0 = r6 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r6 << 16;
            r0 = r3 ^ (r1 >> 21);
            r1 = r10 + 0x6000;
            r3 = r0 + r6;
            r0 = r1 + 0x9D;
            r2 = r0 << 16;
            r1 = r3 ^ (r2 >> 16);
            r0 = r10 + 0xE600;
            r5 = r4 - r1;
            r1 = r0 + 0xE4;
            r0 = r5 << 16;
            r4 = r1 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r5 << 16;
            r0 = r3 ^ (r1 >> 21);
            r2 = r0 + r5;
            r1 = r2 ^ (r4 >> 16);
            r6 = r6 - r1;
            r0 = r6 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r6 << 16;
            r0 = r3 ^ (r1 >> 21);
            r2 = r0 + r6;
            r1 = r2 ^ (r4 >> 16);
            r4 = r5 - r1;
            r0 = r4 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r4 << 16;
            r0 = r3 ^ (r1 >> 21);
            r3 = r0 + r4;
            r1 = r9 + 0x6D00;
            r0 = r1 + 0x2B;
            r2 = r0 << 16;
            r1 = r3 ^ (r2 >> 16);
            r6 = r6 - r1;
            r0 = r6 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r6 << 16;
            r0 = r3 ^ (r1 >> 21);
            r3 = r0 + r6;
            r1 = r8 + 0x6D00;
            r0 = r1 + 0x2B;
            r2 = r0 << 16;
            r1 = r3 ^ (r2 >> 16);
            r5 = r4 - r1;
            r0 = r7 + 0xF300;
            r1 = r0 + 0x72;
            r4 = r1 << 16;
            r0 = r5 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r5 << 16;
            r0 = r3 ^ (r1 >> 21);
            r2 = r0 + r5;
            r1 = r2 ^ (r4 >> 16);
            r6 = r6 - r1;
            r0 = r6 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r6 << 16;
            r0 = r3 ^ (r1 >> 21);
            r2 = r0 + r6;
            r1 = r2 ^ (r4 >> 16);
            r4 = r5 - r1;
            r0 = r4 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r4 << 16;
            r0 = r3 ^ (r1 >> 21);
            r1 = r8 + 0x7900;
            r3 = r0 + r4;
            r0 = r1 + 0xB9;
            r2 = r0 << 16;
            r1 = r3 ^ (r2 >> 16);
            r5 = r6 - r1;
            r0 = r5 << 16;
            r1 = r0 >> 16;
            r2 = r1 << 4;
            r0 = r2 << 16;
            r3 = r0 >> 16;
            r1 = r5 << 16;
            r0 = r3 ^ (r1 >> 21);
            r1 = r9 + 0x7900;
            r3 = r0 + r5;
            r0 = r1 + 0xB9;
            r2 = r0 << 16;
            r1 = r3 ^ (r2 >> 16);
            r3 = r4 - r1;
            r0 = r3 << 16;
            r4 = r0 >> 16;
            r1 = r4 << 4;
            r0 = r1 << 16;
            r2 = r0 >> 16;
            r1 = r3 << 16;
            r0 = r2 ^ (r1 >> 21);
            r3 = r0 + r3;
            r1 = r10 << 16;
            r0 = r3 ^ (r1 >> 16);
            r2 = r5 - r0;
            r1 = r2 << 16;
            r0 = r1 >> 16;
            r0 = r0 | (r4 << 16);
            return r0;
        }

        private static void TeaBlockDecrypt(byte[] data, int size, ushort[] key)
        {
            for (int i = 0; i < size; i += 4)
            {
                uint block = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
                uint dec = XteaDecrypt(block, key);

                data[i] = (byte)(dec & 0xFF);
                data[i + 1] = (byte)((dec >> 8) & 0xFF);
                data[i + 2] = (byte)((dec >> 16) & 0xFF);
                data[i + 3] = (byte)((dec >> 24) & 0xFF);
            }
        }

        private static BigInteger FromLittleEndian(byte[] source, int offset, int length)
        {
            // Append a zero byte so the value is always treated as positive.
            var tmp = new byte[length + 1];
            Array.Copy(source, offset, tmp, 0, length);
            return new BigInteger(tmp);
        }

        private static void ToLittleEndian(BigInteger value, byte[] dest, int length)
        {
            byte[] raw = value.ToByteArray();
            for (int i = 0; i < length; i++)
            {
                dest[i] = (i < raw.Length) ? raw[i] : (byte)0;
            }
        }

        private static bool TryFindMeta(byte[] data, bool compressed, out int metaOffset)
        {
            metaOffset = -1;

            uint codeSize = BitConverter.ToUInt32(data, 12);
            uint dataSize = BitConverter.ToUInt32(data, 16);
            uint resSize = BitConverter.ToUInt32(data, 24);
            uint resourcesFileOffset = BitConverter.ToUInt32(data, 28);

            long resStart = compressed ? resourcesFileOffset : (VMGPHeader.TotalSize + codeSize + dataSize);
            if ((resStart <= 0) || (resStart + 4 > data.Length))
            {
                return false;
            }

            int resHeaderSize = BitConverter.ToInt32(data, (int)resStart);
            if ((resHeaderSize <= 0) || (resStart + resHeaderSize > data.Length))
            {
                return false;
            }

            int resCount = (resHeaderSize / sizeof(int)) - 1;
            if (resCount <= 0)
            {
                return false;
            }

            var resOffsets = new uint[resCount];
            for (int i = 0; i < resCount; i++)
            {
                resOffsets[i] = BitConverter.ToUInt32(data, (int)resStart + 4 + (i * 4));
            }
            resOffsets[resCount - 1] = resSize;

            uint offset = (uint)resHeaderSize;
            for (int i = 0; i < resCount; i++)
            {
                long entryPos = resStart + offset;
                if ((entryPos + 4 <= data.Length) && (BitConverter.ToUInt32(data, (int)entryPos) == MetaMagic))
                {
                    metaOffset = (int)(entryPos + 9);
                    return (metaOffset + MetaBlobSize) <= data.Length;
                }

                offset = resOffsets[i];
            }

            return false;
        }

        /// <summary>
        /// Recovers the symmetric key from the meta blob, which doubles as the
        /// test for whether the file is encrypted at all: an unencrypted file's
        /// meta will not survive the selector and padding checks.
        /// </summary>
        private static bool TryRecoverSymmetricKey(byte[] data, bool compressed, Keys keys, out uint[] symmetricKey)
        {
            symmetricKey = null;

            if (!keys.Valid || !TryFindMeta(data, compressed, out int metaOffset))
            {
                return false;
            }

            uint selectorEnc = BitConverter.ToUInt32(data, metaOffset + 0x8C);
            uint selector = XteaDecrypt(selectorEnc, keys.selectorKey);
            if ((selector & 0xFFFFFFFC) != 0)
            {
                return false;
            }

            var modulusBytes = new byte[128];
            Array.Copy(keys.bigKeys, (int)(128 * selector), modulusBytes, 0, 128);

            if (keys.moduliKey != null)
            {
                TeaBlockDecrypt(modulusBytes, 128, keys.moduliKey);
            }

            BigInteger modulus = FromLittleEndian(modulusBytes, 0, 128);
            BigInteger meta = FromLittleEndian(data, metaOffset, 128);
            BigInteger recovered = BigInteger.ModPow(meta, 3, modulus);

            var decryptedMeta = new byte[0x80];
            ToLittleEndian(recovered, decryptedMeta, 0x80);

            for (int i = 0x80 - 1; i >= 0x26; i--)
            {
                byte cur = decryptedMeta[i];
                if ((cur != 0) && (cur != 0xFF) && (cur != 0x01))
                {
                    return false;
                }
            }

            symmetricKey = new uint[4];
            for (int i = 0; i < 4; i++)
            {
                symmetricKey[i] = BitConverter.ToUInt32(decryptedMeta, 0x14 + (i * 4));
            }

            return true;
        }

        public static DetectResult Detect(byte[] data, bool compressed, Keys keys)
        {
            var result = new DetectResult();
            result.encrypted = TryRecoverSymmetricKey(data, compressed, keys, out result.symmetricKey);
            return result;
        }

        /// <summary>
        /// Decrypts the code section in place. For compressed titles the result
        /// is still compressed and must be run through the decompressor next.
        /// </summary>
        public static byte[] Decrypt(byte[] data, bool compressed, uint[] symmetricKey)
        {
            var output = (byte[])data.Clone();

            int cursor = VMGPHeader.TotalSize;
            int codeSize;

            if (compressed)
            {
                // The code section is prefixed with its compressed length.
                codeSize = BitConverter.ToInt32(data, cursor);
                cursor += 4;
            }
            else
            {
                codeSize = BitConverter.ToInt32(data, 12);
            }

            uint runningXor = CompressedCodeXorSeed;
            int wordCount = codeSize / 4;

            for (int i = 0; i < wordCount; i++)
            {
                int pos = cursor + (i * 4);
                uint block = BitConverter.ToUInt32(data, pos);
                uint dec = DecryptBlock(block, symmetricKey);

                if (compressed)
                {
                    runningXor = DecryptBlock(runningXor, symmetricKey);
                    dec ^= runningXor;
                }

                output[pos] = (byte)(dec & 0xFF);
                output[pos + 1] = (byte)((dec >> 8) & 0xFF);
                output[pos + 2] = (byte)((dec >> 16) & 0xFF);
                output[pos + 3] = (byte)((dec >> 24) & 0xFF);
            }

            // Trailing bytes that do not fill a word are left untouched (already
            // copied verbatim by the clone), matching the reference tool.
            return output;
        }
    }
}
