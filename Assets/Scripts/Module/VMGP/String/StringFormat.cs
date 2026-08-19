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

using Nofun.PIP2;
using Nofun.VM;
using System;
using System.Text;

namespace Nofun.Module.VMGP
{
    [Module]
    public partial class VMGP
    {
        /// <summary>
        /// Read the next 32-bit variadic argument of the current module call.
        ///
        /// The first 16 bytes of arguments live in P0-P3, the rest on the stack
        /// (same convention as the module call binding generator). The offset
        /// starts after the named arguments of the call.
        /// </summary>
        private uint ReadVarArg(ref int argOffset)
        {
            uint value;

            if (argOffset < 16)
            {
                value = system.Processor.Reg[(uint)(Register.P0 + argOffset)];
            }
            else
            {
                value = system.Memory.ReadMemory32(system.Processor.Reg[Register.SP] + (uint)(argOffset - 16));
            }

            argOffset += 4;
            return value;
        }

        /// <summary>
        /// Format a Mophun format string. The supported syntax, as documented
        /// for vSprintf, is %[0][n]{c,d,i,u,x,X,o,b,s,%}.
        /// </summary>
        private string FormatString(string format, Func<uint> nextArg)
        {
            StringBuilder result = new();

            for (int i = 0; i < format.Length; i++)
            {
                if (format[i] != '%')
                {
                    result.Append(format[i]);
                    continue;
                }

                if (++i >= format.Length)
                {
                    break;
                }

                if (format[i] == '%')
                {
                    result.Append('%');
                    continue;
                }

                bool zeroPad = false;
                if (format[i] == '0')
                {
                    zeroPad = true;
                    i++;
                }

                int width = 0;
                while ((i < format.Length) && char.IsDigit(format[i]))
                {
                    width = width * 10 + (format[i] - '0');
                    i++;
                }

                if (i >= format.Length)
                {
                    break;
                }

                string formatted;

                switch (format[i])
                {
                    case 'c':
                        formatted = ((char)nextArg()).ToString();
                        break;

                    case 'd':
                    case 'i':
                        formatted = ((int)nextArg()).ToString();
                        break;

                    case 'u':
                        formatted = nextArg().ToString();
                        break;

                    case 'x':
                        formatted = nextArg().ToString("x");
                        break;

                    case 'X':
                        formatted = nextArg().ToString("X");
                        break;

                    case 'o':
                        formatted = Convert.ToString((long)nextArg(), 8);
                        break;

                    case 'b':
                        formatted = Convert.ToString((long)nextArg(), 2);
                        break;

                    case 's':
                        uint address = nextArg();
                        formatted = (address == 0) ? "(null)" : new VMString(address).Get(system.Memory);
                        break;

                    default:
                        // Unknown format code, emit it as-is
                        result.Append('%');
                        result.Append(format[i]);
                        continue;
                }

                if (width > formatted.Length)
                {
                    formatted = formatted.PadLeft(width, zeroPad ? '0' : ' ');
                }

                result.Append(formatted);
            }

            return result.ToString();
        }

        private VMPtr<byte> WriteStringToVM(VMPtr<byte> buf, string value)
        {
            Span<byte> dest = buf.AsSpan(system.Memory, value.Length + 1);

            for (int i = 0; i < value.Length; i++)
            {
                dest[i] = (byte)value[i];
            }

            dest[value.Length] = 0;

            // Like vStrCpy, return a pointer to the terminating null
            return buf + value.Length;
        }

        [ModuleCall]
        private VMPtr<byte> vSprintf(VMPtr<byte> buf, VMString format)
        {
            // The two named arguments take up 8 bytes, varargs follow
            int argOffset = 8;
            string result = FormatString(format.Get(system.Memory), () => ReadVarArg(ref argOffset));

            return WriteStringToVM(buf, result);
        }

        [ModuleCall]
        private VMPtr<byte> vSprintfVa(VMPtr<byte> buf, VMString format, VMPtr<uint> args)
        {
            VMPtr<uint> currentArg = args;
            string result = FormatString(format.Get(system.Memory), () =>
            {
                uint value = currentArg.Read(system.Memory);
                currentArg += 1;

                return value;
            });

            return WriteStringToVM(buf, result);
        }
    }
}
