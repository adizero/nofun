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
using Nofun.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nofun.Driver.Unity.Input
{
    public class InputDriver : MonoBehaviour, IInputDriver
    {
        private uint buttonData;
        private uint keypadButtonData;
        private uint pointerButtonData;

        private int pointerX;
        private int pointerY;

        private readonly System.Collections.Generic.HashSet<char> pressedKeypadKeys = new();

        private uint CombinedButtonData => buttonData | keypadButtonData | pointerButtonData;

        public uint PointerPos => ((uint)(ushort)pointerY << 16) | (ushort)pointerX;

        public void SetPointerState(int x, int y, bool down, bool altDown)
        {
            pointerX = x;
            pointerY = y;

            pointerButtonData = (down ? (uint)Driver.Input.KeyCode.PointerDown : 0) |
                (altDown ? (uint)Driver.Input.KeyCode.PointerAltDown : 0);
        }

        /// <summary>
        /// Attach (or refresh) the pointer capture component on the display
        /// showing the emulated screen.
        /// </summary>
        public void AttachPointerCapture(UnityEngine.UI.RawImage display, System.Func<UnityEngine.Vector2> emulatedScreenSize)
        {
            DisplayPointerCapture capture = display.gameObject.GetComponent<DisplayPointerCapture>();

            if (capture == null)
            {
                capture = display.gameObject.AddComponent<DisplayPointerCapture>();
            }

            capture.Setup(this, emulatedScreenSize);
        }

        public void OnFire(InputValue value)
        {
            if (value.isPressed)
            {
                buttonData |= (uint)Driver.Input.KeyCode.Fire;
            }
            else
            {
                buttonData &= ~(uint)Driver.Input.KeyCode.Fire;
            }
        }

        public void OnFire2(InputValue value)
        {
            if (value.isPressed)
            {
                buttonData |= (uint)Driver.Input.KeyCode.Fire2;
            }
            else
            {
                buttonData &= ~(uint)Driver.Input.KeyCode.Fire2;
            }
        }

        public void OnLeft(InputValue value)
        {
            if (value.isPressed)
            {
                buttonData |= (uint)Driver.Input.KeyCode.Left;
            }
            else
            {
                buttonData &= ~(uint)Driver.Input.KeyCode.Left;
            }
        }

        public void OnRight(InputValue value)
        {
            if (value.isPressed)
            {
                buttonData |= (uint)Driver.Input.KeyCode.Right;
            }
            else
            {
                buttonData &= ~(uint)Driver.Input.KeyCode.Right;
            }
        }

        public void OnUp(InputValue value)
        {
            if (value.isPressed)
            {
                buttonData |= (uint)Driver.Input.KeyCode.Up;
            }
            else
            {
                buttonData &= ~(uint)Driver.Input.KeyCode.Up;
            }
        }

        public void OnDown(InputValue value)
        {
            if (value.isPressed)
            {
                buttonData |= (uint)Driver.Input.KeyCode.Down;
            }
            else
            {
                buttonData &= ~(uint)Driver.Input.KeyCode.Down;
            }
        }

        public void OnSelect(InputValue value)
        {
            if (value.isPressed)
            {
                buttonData |= (uint)Driver.Input.KeyCode.Select;
            }
            else
            {
                buttonData &= ~(uint)Driver.Input.KeyCode.Select;
            }
        }

        public void OnSEJoystickPush(InputValue value)
        {
            if (value.isPressed)
            {
                buttonData |= (uint)Driver.Input.KeyCode.SEJoystickPush;
            }
            else
            {
                buttonData &= ~(uint)Driver.Input.KeyCode.SEJoystickPush;
            }
        }

        public uint GetButtonData()
        {
            return CombinedButtonData;
        }

        public void SetKeypadKey(char key, bool pressed)
        {
            if (pressed)
            {
                pressedKeypadKeys.Add(key);
            }
            else
            {
                pressedKeypadKeys.Remove(key);
            }

            keypadButtonData = 0;
            foreach (var pressedKey in pressedKeypadKeys)
            {
                keypadButtonData |= GetKeypadKeyMask(pressedKey);
            }
        }

        private static uint GetKeypadKeyMask(char key)
        {
            switch (key)
            {
                case '1':
                    return (uint)(Driver.Input.KeyCode.Up | Driver.Input.KeyCode.Left);
                case '2':
                    return (uint)Driver.Input.KeyCode.Up;
                case '3':
                    return (uint)(Driver.Input.KeyCode.Up | Driver.Input.KeyCode.Right);
                case '4':
                    return (uint)Driver.Input.KeyCode.Left;
                case '5':
                case '*':
                    return (uint)Driver.Input.KeyCode.Fire;
                case '6':
                    return (uint)Driver.Input.KeyCode.Right;
                case '7':
                    return (uint)(Driver.Input.KeyCode.Down | Driver.Input.KeyCode.Left);
                case '8':
                    return (uint)Driver.Input.KeyCode.Down;
                case '9':
                    return (uint)(Driver.Input.KeyCode.Down | Driver.Input.KeyCode.Right);
                case '#':
                    return (uint)Driver.Input.KeyCode.Fire2;
                case '0':
                    return (uint)Driver.Input.KeyCode.Select;
                default:
                    return 0;
            }
        }

        public bool KeyPressed(uint keyCodeAsciiOrImplDefined)
        {
            var currentButtonData = CombinedButtonData;

            switch (keyCodeAsciiOrImplDefined)
            {
                case '1':
                    {
                        return (currentButtonData & (uint)(Driver.Input.KeyCode.Up | Driver.Input.KeyCode.Left)) == (uint)(Driver.Input.KeyCode.Up | Driver.Input.KeyCode.Left);
                    }

                case '2':
                    {
                        return BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Up);
                    }

                case '3':
                    {
                        return (currentButtonData & (uint)(Driver.Input.KeyCode.Up | Driver.Input.KeyCode.Right)) == (uint)(Driver.Input.KeyCode.Up | Driver.Input.KeyCode.Right);
                    }

                case '4':
                    {
                        return BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Left);
                    }

                case '5':
                case '*':
                    {
                        return BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Fire);
                    }

                case '#':
                    {
                        return BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Fire2);
                    }

                case '0':
                case (uint)SystemAsciiCode.SEOption:
                    {
                        return BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Select);
                    }

                case '6':
                    {
                        return BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Right);
                    }

                case '7':
                    {
                        return (currentButtonData & (uint)(Driver.Input.KeyCode.Down | Driver.Input.KeyCode.Left)) == (uint)(Driver.Input.KeyCode.Down | Driver.Input.KeyCode.Left);
                    }

                case '8':
                    {
                        return BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Down);
                    }

                case '9':
                    {
                        return (currentButtonData & (uint)(Driver.Input.KeyCode.Down | Driver.Input.KeyCode.Right)) == (uint)(Driver.Input.KeyCode.Down | Driver.Input.KeyCode.Right);
                    }

                default:
                    return false;
            }
        }

        public uint KeyScan
        {
            get
            {
                var currentButtonData = CombinedButtonData;

                if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Up))
                {
                    if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Left))
                    {
                        return '1';
                    }

                    if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Right))
                    {
                        return '3';
                    }

                    return '2';
                }
                else if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Down))
                {
                    if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Left))
                    {
                        return '7';
                    }

                    if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Right))
                    {
                        return '9';
                    }

                    return '8';
                }
                else if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Left))
                {
                    return '4';
                }
                else if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Right))
                {
                    return '6';
                }
                else if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Fire))
                {
                    return '5';
                }
                else if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Fire2))
                {
                    return '#';
                }
                else if (BitUtil.FlagSet(currentButtonData, Driver.Input.KeyCode.Select))
                {
                    return (uint)SystemAsciiCode.SEOption;
                }
                else
                {
                    return 0;
                }
            }
        }

        public void EndFrame()
        {
        }
    }
}