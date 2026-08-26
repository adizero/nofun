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

using Nofun.VM;
using System;

namespace Nofun.Module.VMGP
{
    [Module]
    public partial class VMGP
    {
        [ModuleCall]
        private VMPtr<byte> vStrCpy(VMPtr<byte> dest, VMPtr<byte> source)
        {
            // Sometimes it's also used like memcpy, so we must do byte-by-byte
            while (true)
            {
                byte sourceByte = source.Read(system.Memory);
                dest.Write(system.Memory, sourceByte);

                if (sourceByte == 0)
                {
                    break;
                }

                source += 1;
                dest += 1;
            }

            return dest;
        }

        [ModuleCall]
        private VMPtr<byte> vStrCat(VMPtr<byte> dest, VMPtr<byte> source)
        {
            while (dest.Read(system.Memory) != 0)
            {
                dest += 1;
            }

            return vStrCpy(dest, source);
        }

        [ModuleCall]
        private int vStrCmp(VMPtr<byte> lhs, VMPtr<byte> rhs)
        {
            while (true)
            {
                byte lhsByte = lhs.Read(system.Memory);
                byte rhsByte = rhs.Read(system.Memory);

                if (lhsByte != rhsByte)
                {
                    return lhsByte - rhsByte;
                }

                if (lhsByte == 0)
                {
                    return 0;
                }

                lhs += 1;
                rhs += 1;
            }
        }

        [ModuleCall]
        private int vStrLen(VMString str)
        {
            var strstr = str.Get(system.Memory);
            return strstr.Length;
        }

        [ModuleCall]
        private VMPtr<ushort> vStrCpyU(VMPtr<ushort> dest, VMPtr<ushort> source)
        {
            while (true)
            {
                ushort sourceChar = source.Read(system.Memory);
                dest.Write(system.Memory, sourceChar);

                if (sourceChar == 0)
                {
                    break;
                }

                source += 1;
                dest += 1;
            }

            return dest;
        }

        [ModuleCall]
        private VMPtr<ushort> vStrCatU(VMPtr<ushort> dest, VMPtr<ushort> source)
        {
            while (dest.Read(system.Memory) != 0)
            {
                dest += 1;
            }

            return vStrCpyU(dest, source);
        }

        [ModuleCall]
        private int vStrCmpU(VMPtr<ushort> lhs, VMPtr<ushort> rhs)
        {
            while (true)
            {
                ushort lhsChar = lhs.Read(system.Memory);
                ushort rhsChar = rhs.Read(system.Memory);

                if (lhsChar != rhsChar)
                {
                    return lhsChar - rhsChar;
                }

                if (lhsChar == 0)
                {
                    return 0;
                }

                lhs += 1;
                rhs += 1;
            }
        }

        [ModuleCall]
        private int vStrLenU(VMPtr<ushort> str)
        {
            int length = 0;

            while (str.Read(system.Memory) != 0)
            {
                length++;
                str += 1;
            }

            return length;
        }

        [ModuleCall]
        private VMPtr<ushort> vStrToU(VMPtr<ushort> dest, VMPtr<byte> source)
        {
            while (true)
            {
                byte sourceChar = source.Read(system.Memory);
                dest.Write(system.Memory, sourceChar);

                if (sourceChar == 0)
                {
                    break;
                }

                source += 1;
                dest += 1;
            }

            return dest;
        }

        private VMPtr<byte> NumberToString(long val, VMPtr<byte> buf, byte len, byte pad)
        {
            string valConverted = val.ToString();
            int fieldWidth = Math.Max(valConverted.Length, len);
            Span<byte> destBuf = buf.AsSpan(system.Memory, fieldWidth + 1);

            // The number is right-justified in a field of width len, with the fill
            // character padded on the left (e.g. vitoa(1, buf, 2, '0') -> "01").
            int padCount = fieldWidth - valConverted.Length;
            for (int i = 0; i < padCount; i++)
            {
                destBuf[i] = pad;
            }

            for (int i = 0; i < valConverted.Length; i++)
            {
                destBuf[padCount + i] = (byte)valConverted[i];
            }

            destBuf[fieldWidth] = 0;
            return buf + fieldWidth;
        }

        [ModuleCall]
        private VMPtr<byte> vitoa(int val, VMPtr<byte> buf, byte len, byte pad)
        {
            return NumberToString(val, buf, len, pad);
        }

        [ModuleCall]
        private VMPtr<byte> vutoa(uint val, VMPtr<byte> buf, byte len, byte pad)
        {
            return NumberToString(val, buf, len, pad);
        }

        [ModuleCall]
        private int vatoi(VMPtr<byte> str, VMPtr<uint> end)
        {
            string strConvert = "";

            while (true)
            {
                byte charVal = str.Read(system.Memory);

                if ((charVal >= '0') && (charVal <= '9'))
                {
                    strConvert += (char)charVal;
                }
                else
                {
                    if (!end.IsNull)
                    {
                        end.Write(system.Memory, str.Value);
                    }

                    break;
                }

                str += 1;
            }

            return (strConvert.Length == 0) ? 0 : int.Parse(strConvert);
        }
    }
}