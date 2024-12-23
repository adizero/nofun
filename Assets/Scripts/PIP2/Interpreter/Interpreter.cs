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

using System;
using Nofun.PIP2.Encoding;

namespace Nofun.PIP2.Interpreter
{
    public partial class Interpreter : Processor
    {
        private Action<UInt32>[] OpcodeTables;

        private bool shouldStop = false;
        private bool isRunning = false;
        private int instructionRan = 0;

        public override int InstructionRan => instructionRan;

        public Interpreter(ProcessorConfig config) : base(config)
        {
            OpcodeTables = new Action<UInt32>[116]
            {
                null, null, I(ADD), I(AND), I(MUL), I(DIV), I(DIVU), I(OR),    // 0x00
                I(XOR), I(SUB), I(SLL), I(SRA), I(SRL), I(NOT), I(NEG), I(EXSB),    // 0x08
                I(EXSH), I(MOV), I(ADDB), I(SUBB), I(ANDB), I(ORB), I(MOVB), I(ADDH),    // 0x10
                I(SUBH), I(ANDH), I(ORH), I(MOVH), I(SLLi), I(SRAi), I(SRLi), I(ADDQ),    // 0x18
                I(MULQ), I(ADDBi), I(ANDBi), I(ORBi), I(SLLB), I(SRLB), I(SRAB), I(ADDHi),    // 0x20
                I(ANDHi), I(SLLH), I(SRLH), I(SRAH), I(BEQI), I(BNEI), I(BGEI), null,    // 0x28
                I(BGTI), I(BGTUI), I(BLEI), I(BLEUI), I(BLTI), I(BLTUI), I(BEQIB), I(BNEIB),    // 0x30
                I(BGEIB), null, I(BGTIB), I(BGTUIB), I(BLEIB), I(BLEUIB), I(BLTIB), I(BLTUIB),    // 0x38
                I(LDQ), I(JPr), I(CALLr), I(STORE), I(RESTORE), I(RET), I(KILLTASK), I(SLEEP),    // 0x40
                I(SYSCPY), I(SYSSET), I(ADDi), I(ANDi), I(MULi), I(DIVi), I(DIVUi), I(ORi),    // 0x48
                I(XORi), I(SUBi), I(STBd), I(STHd), I(STWd), I(LDBd), I(LDHd), I(LDWd),    // 0x50
                I(LDBUd), I(LDHUd), I(LDI), I(JPl), I(CALLl), I(BEQ), I(BNE), I(BGE),    // 0x58
                I(BGEU), I(BGT), I(BGTU), I(BLE), I(BLEU), I(BLT), I(BLTU), null,    // 0x60
                null, null, null, null, null, null, null, null,    // 0x68
                null, null, null, null                             // 0x70
            };
        }

        #region Instruction Handler Wrapper
        private Action<UInt32> I(Action<TwoSourcesEncoding> handler)
        {
            return value => handler(new TwoSourcesEncoding()
            {
                Instruction = value
            });
        }

        private Action<UInt32> I(Action<UndefinedEncoding> handler)
        {
            return value => handler(new UndefinedEncoding()
            {
                Instruction = value
            });
        }

        private Action<UInt32> I(Action<DestOnlyEncoding> handler)
        {
            return value => handler(new DestOnlyEncoding()
            {
                Instruction = value
            });
        }

        private Action<UInt32> I(Action<WordEncoding> handler)
        {
            return value => handler(new WordEncoding()
            {
                Instruction = value
            });
        }

        private Action<UInt32> I(Action<RangeRegEncoding> handler)
        {
            return value => handler(new RangeRegEncoding()
            {
                Instruction = value
            });
        }
        #endregion

        private bool IsDwordRawImmediate(ref uint immediate)
        {
            if ((immediate & 0x80000000U) != 0)
            {
                immediate = (uint)((immediate & 0x7FFFFFFF) | ((immediate << 1) & 0x80000000));
                return true;
            }

            return false;
        }

        private long FetchImmediate()
        {
            UInt32 infoWord = config.ReadDword(Reg[Register.PC]);
            Reg[Register.PC] += 4;

            // Bit 31 set marking that it's a constant
            if (IsDwordRawImmediate(ref infoWord))
            {
                return (int)infoWord;
            }
            else
            {
                PoolData data = GetPoolData(infoWord);

                uint? loadNumber = data?.ImmediateInteger;
                if (loadNumber == null)
                {
                    throw new InvalidOperationException("The data to load from pool is not integer (poolItemIndex=" + infoWord + ")");
                }

                return (uint)loadNumber;
            }
        }

        private void SkipImmediate()
        {
            Reg[Register.PC] += 4;
        }

        public override void Run(int instructionPerRun)
        {
            if (isRunning)
            {
                throw new InvalidOperationException("Can't call Run() when is already running.");
            }

            shouldStop = false;
            isRunning = true;

            instructionRan = 0;

            while (!shouldStop && (instructionRan < instructionPerRun))
            {
                uint value = config.ReadCode(registers[Register.PCIndex]);
                Action<UInt32> handler = OpcodeTables[value & 0xFF];

                if (handler == null)
                {
                    throw new InvalidOperationException($"Unimplemented opcode {(Opcode)(value & 0xFF)} at PC={registers[Register.PCIndex]}");
                }

                registers[Register.PCIndex] += InstructionSize;
                handler(value);

                instructionRan++;
            }

            isRunning = false;
        }

        public override void Stop()
        {
            shouldStop = true;
        }
    }
}
