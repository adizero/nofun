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

using System.Collections;
using System.Collections.Generic;
using Nofun.Driver.Unity.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using ScreenOrientation = Nofun.Settings.ScreenOrientation;

namespace Nofun
{
    public class ScreenManager: MonoBehaviour
    {
        [SerializeField]
        private GameObject controlMobilePotrait;

        [SerializeField]
        private GameObject controlMobileLandscape;

        [SerializeField]
        private RawImage displayPotrait;

        [SerializeField]
        private RawImage displayLandscape;

        [SerializeField]
        private PanelSettings landscapePanelSettings;

        [SerializeField]
        private PanelSettings potraitPanelSettings;

        private Settings.ScreenOrientation screenOrientation;

        public event System.Action<Settings.ScreenOrientation> ScreenOrientationChanged;
        private Coroutine confirmScreenSizeChangeCoroutine;
        private bool isConfirmingPotrait = false;

        public Settings.ScreenOrientation ScreenOrientation
        {
            get => screenOrientation;
            set
            {
                SetScreenOrientationDetail(value);
            }
        }

        public RawImage CurrentDisplay => (screenOrientation == Settings.ScreenOrientation.Potrait) ? displayPotrait : displayLandscape;

        public PanelSettings CurrentPanelSettings => (screenOrientation == Settings.ScreenOrientation.Potrait) ? potraitPanelSettings : landscapePanelSettings;

        private void UpdateCanvasOrientation()
        {
            ScreenOrientationChanged?.Invoke(screenOrientation);
        }

        private void SetScreenOrientationDetail(Settings.ScreenOrientation value)
        {
            if (screenOrientation == value)
            {
                return;
            }

            if (Application.isMobilePlatform)
            {
                Screen.orientation = (value == Settings.ScreenOrientation.Potrait) ? UnityEngine.ScreenOrientation.Portrait : UnityEngine.ScreenOrientation.LandscapeLeft;
                screenOrientation = value;

                UpdateCanvasOrientation();
            }
        }

        private void Awake()
        {
#if !UNITY_EDITOR
            if (Application.isMobilePlatform)
            {
                screenOrientation = (Screen.orientation == UnityEngine.ScreenOrientation.Portrait) ? Settings.ScreenOrientation.Potrait : Settings.ScreenOrientation.Landscape;
            }
            else
#endif
            {
                if (Screen.width > Screen.height)
                {
                    screenOrientation = Settings.ScreenOrientation.Landscape;
                }
                else
                {
                    screenOrientation = Settings.ScreenOrientation.Potrait;
                }
            }
        }

        private IEnumerator ConfirmScreenSizeChange(bool isConfirmingPotrait)
        {
            for (int i = 0; i < 2; i++)
            {
                if (isConfirmingPotrait && (Screen.width > Screen.height))
                {
                    confirmScreenSizeChangeCoroutine = null;
                    yield break;
                }
                else if (!isConfirmingPotrait && (Screen.width <= Screen.height))
                {
                    confirmScreenSizeChangeCoroutine = null;
                    yield break;
                }
                else
                {
                    yield return null;
                }
            }

            screenOrientation = isConfirmingPotrait ? ScreenOrientation.Potrait : ScreenOrientation.Landscape;
            confirmScreenSizeChangeCoroutine = null;

            UpdateCanvasOrientation();
        }

#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
        private void Update()
        {
            if ((Screen.width > Screen.height) && (screenOrientation != Settings.ScreenOrientation.Landscape))
            {
                if (confirmScreenSizeChangeCoroutine != null)
                {
                    if (!isConfirmingPotrait)
                    {
                        return;
                    }

                    StopCoroutine(confirmScreenSizeChangeCoroutine);
                }

                confirmScreenSizeChangeCoroutine = StartCoroutine(ConfirmScreenSizeChange(false));
                isConfirmingPotrait = false;
            }

            if ((Screen.width <= Screen.height) && (screenOrientation != Settings.ScreenOrientation.Potrait))
            {
                if (confirmScreenSizeChangeCoroutine != null)
                {
                    if (isConfirmingPotrait)
                    {
                        return;
                    }

                    StopCoroutine(confirmScreenSizeChangeCoroutine);
                }

                confirmScreenSizeChangeCoroutine = StartCoroutine(ConfirmScreenSizeChange(true));
                isConfirmingPotrait = true;
            }
        }
#endif

        private void Start()
        {
            if (!Application.isMobilePlatform)
            {
                controlMobileLandscape.SetActive(false);
                controlMobilePotrait.SetActive(false);
            }
            else
            {
                StartCoroutine(SetupTouchKeypads());
            }

            UpdateCanvasOrientation();
        }

        private IEnumerator SetupTouchKeypads()
        {
            // Wait a bit so RectTransforms have their final sizes.
            for (var i = 0; i < 2; i++)
            {
                yield return null;
            }

            var inputDriver = FindObjectOfType<InputDriver>();
            if (inputDriver == null)
            {
                yield break;
            }

            EnsureKeypad(controlMobilePotrait, inputDriver, isLandscape: false);
            EnsureKeypad(controlMobileLandscape, inputDriver, isLandscape: true);
        }

        private void EnsureKeypad(GameObject controlRoot, InputDriver inputDriver, bool isLandscape)
        {
            if (controlRoot == null)
            {
                return;
            }

            var mobileRoot = FindDescendant(controlRoot.transform, "Mobile") as RectTransform;
            if (mobileRoot == null)
            {
                return;
            }

            if (mobileRoot.Find("Keypad") != null)
            {
                return;
            }

            var twoSides = FindDescendant(mobileRoot, "TwoSides") as RectTransform;
            if (twoSides == null)
            {
                return;
            }

            var fire1 = FindDescendant(mobileRoot, "Fire1") as RectTransform;
            var fire2 = FindDescendant(mobileRoot, "Fire2") as RectTransform;

            // Base keypad size on the existing control block width.
            var keypadWidth = Mathf.Clamp(twoSides.rect.width * 0.45f, 300f, 700f);
            var keypadHeight = keypadWidth * 4f / 3f;
            const float spacing = 10f;
            const float margin = 25f;

            var cellWidth = (keypadWidth - (spacing * 2f)) / 3f;
            var cellHeight = (keypadHeight - (spacing * 3f)) / 4f;

            // Default placement (fallback): centered above the existing controls.
            var keypadParent = mobileRoot;
            var keypadAnchorMin = new Vector2(0.5f, 0.5f);
            var keypadAnchorMax = new Vector2(0.5f, 0.5f);
            var keypadPivot = new Vector2(0.5f, 0.5f);
            var keypadAnchoredPosition = new Vector2(0f, GetTopInParentSpace(mobileRoot, twoSides) + (keypadHeight * 0.5f) + margin);
            var usedFireButtonsPlacement = false;

            // Preferred placement: replace the existing Fire1/Fire2 (A/B) buttons.
            if (fire1 != null && fire2 != null && fire1.parent is RectTransform fireParent)
            {
                // Hide the old A/B buttons.
                fire1.gameObject.SetActive(false);
                fire2.gameObject.SetActive(false);

                keypadParent = fireParent;
                keypadAnchorMin = fire1.anchorMin;
                keypadAnchorMax = fire1.anchorMax;
                keypadPivot = new Vector2(1f, 0.5f);

                var bounds1 = GetBoundsInParentSpace(fireParent, fire1);
                var bounds2 = GetBoundsInParentSpace(fireParent, fire2);
                var combined = Encapsulate(bounds1, bounds2);

                // Anchor/pivot on the right edge (like the old buttons), grow left.
                keypadAnchoredPosition = new Vector2(combined.max.x, combined.center.y);
                usedFireButtonsPlacement = true;

                // Landscape tweak: shift left/up so the keypad stays on-screen.
                if (isLandscape)
                {
                    keypadAnchoredPosition += new Vector2(-1.75f * cellWidth, cellHeight);
                }
                else
                {
                    keypadAnchoredPosition += new Vector2(-0.18f * cellWidth, 0);
                }
            }
            else
            {
                // If we can only find one of them, still hide it.
                if (fire1 != null) fire1.gameObject.SetActive(false);
                if (fire2 != null) fire2.gameObject.SetActive(false);
            }

            var keypadGo = new GameObject("Keypad",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UnityEngine.UI.Image),
                typeof(GridLayoutGroup));
            keypadGo.transform.SetParent(keypadParent, false);
            keypadGo.transform.SetAsLastSibling();

            var keypadRect = keypadGo.GetComponent<RectTransform>();
            keypadRect.anchorMin = keypadAnchorMin;
            keypadRect.anchorMax = keypadAnchorMax;
            keypadRect.pivot = keypadPivot;
            keypadRect.sizeDelta = new Vector2(keypadWidth, keypadHeight);
            keypadRect.anchoredPosition = keypadAnchoredPosition;

            var keypadImage = keypadGo.GetComponent<UnityEngine.UI.Image>();
            keypadImage.color = new Color(0f, 0f, 0f, 0.15f);
            keypadImage.raycastTarget = false;

            var keypadGroup = keypadGo.GetComponent<CanvasGroup>();
            keypadGroup.alpha = 1f;

            var grid = keypadGo.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.spacing = new Vector2(spacing, spacing);
            grid.childAlignment = TextAnchor.MiddleCenter;

            grid.cellSize = new Vector2(cellWidth, cellHeight);

            foreach (var keypadKey in GetKeypadKeys())
            {
                CreateKeyButton(keypadGo.transform, inputDriver, keypadKey);
            }
        }

        private static IEnumerable<char> GetKeypadKeys()
        {
            yield return '1';
            yield return '2';
            yield return '3';
            yield return '4';
            yield return '5';
            yield return '6';
            yield return '7';
            yield return '8';
            yield return '9';
            yield return '*';
            yield return '0';
            yield return '#';
        }

        private static void CreateKeyButton(Transform parent, InputDriver inputDriver, char keypadKey)
        {
            var buttonGo = new GameObject($"Key_{keypadKey}",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button),
                typeof(global::Nofun.TouchKeypadButton));

            buttonGo.transform.SetParent(parent, false);

            var image = buttonGo.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 1f, 1f, 0.75f);

            var touchHandler = buttonGo.GetComponent<global::Nofun.TouchKeypadButton>();
            touchHandler.Init(inputDriver, keypadKey);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(buttonGo.transform, false);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = keypadKey.ToString();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 40;
            text.color = Color.black;
            text.raycastTarget = false;
        }

        private static float GetTopInParentSpace(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            return parent.InverseTransformPoint(corners[1]).y;
        }

        private static Bounds GetBoundsInParentSpace(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);

            for (var i = 0; i < 4; i++)
            {
                var p = parent.InverseTransformPoint(corners[i]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b.min);
            a.Encapsulate(b.max);
            return a;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
