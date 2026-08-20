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
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nofun.Driver.Unity.Input
{
    /// <summary>
    /// Forwards pointer/touch events on the emulated screen display to the
    /// input driver, translated into Mophun screen coordinates (Y down).
    /// </summary>
    public class DisplayPointerCapture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private InputDriver receiver;
        private Func<Vector2> emulatedScreenSize;
        private RectTransform rectTransform;

        public void Setup(InputDriver receiver, Func<Vector2> emulatedScreenSize)
        {
            this.receiver = receiver;
            this.emulatedScreenSize = emulatedScreenSize;
            this.rectTransform = GetComponent<RectTransform>();
        }

        private void Forward(PointerEventData eventData, bool down)
        {
            if ((receiver == null) || (rectTransform == null))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position,
                eventData.pressEventCamera, out Vector2 localPoint))
            {
                return;
            }

            Rect displayRect = rectTransform.rect;
            Vector2 screenSize = emulatedScreenSize();

            if ((displayRect.width <= 0) || (displayRect.height <= 0) || (screenSize.x <= 0) || (screenSize.y <= 0))
            {
                return;
            }

            float u = (localPoint.x - displayRect.xMin) / displayRect.width;
            float v = (localPoint.y - displayRect.yMin) / displayRect.height;

            int x = Mathf.Clamp(Mathf.RoundToInt(u * (screenSize.x - 1)), 0, (int)screenSize.x - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((1.0f - v) * (screenSize.y - 1)), 0, (int)screenSize.y - 1);

            receiver.SetPointerState(x, y, down, down && (eventData.button == PointerEventData.InputButton.Right));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Forward(eventData, true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Forward(eventData, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Forward(eventData, false);
        }
    }
}
