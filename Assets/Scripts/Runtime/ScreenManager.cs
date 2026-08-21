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

        private class ControlSide
        {
            public RectTransform half;
            public GameObject original;
            public bool isLeft;
            public Dictionary<Settings.ControlPadType, GameObject> built = new();
        }

        private class PadAssets
        {
            public Sprite up;
            public Sprite down;
            public Sprite left;
            public Sprite right;
            public Sprite fire1;
            public Sprite fire2;
            public Color directionColor = Color.white;
            public Color fireColor = Color.white;
        }

        private struct OverlayPlacement
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 anchoredPosition;
        }

        private class ControlRootInfo
        {
            public bool isLandscape;
            public float padBaseWidth;
            public Vector2 canvasSize;
            public RectTransform padParent;
            public ControlSide left;
            public ControlSide right;
            public RectTransform twoSides;
            public RectTransform menuButton;
            public RectTransform settingButton;
            public OverlayPlacement menuOriginal;
            public OverlayPlacement settingOriginal;
            public PadAssets assets;
            public InputDriver inputDriver;
        }

        private const float PadBottomMargin = 40f;
        private const float PadEdgeMargin = 40f;

        private readonly List<ControlRootInfo> controlRoots = new();
        private Settings.ControlPadType leftControlPad = Settings.ControlPadType.DPad;
        private Settings.ControlPadType rightControlPad = Settings.ControlPadType.Keypad;

        /// <summary>
        /// Select which control pad to show on each side of the screen.
        /// </summary>
        public void SetControlLayouts(Settings.ControlPadType left, Settings.ControlPadType right)
        {
            leftControlPad = left;
            rightControlPad = right;

            ApplyControlLayouts();
        }

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
                StartCoroutine(SetupTouchControls());
            }

            UpdateCanvasOrientation();
        }

        private IEnumerator SetupTouchControls()
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

            SetupControlRoot(controlMobilePotrait, inputDriver, isLandscape: false);
            SetupControlRoot(controlMobileLandscape, inputDriver, isLandscape: true);

            ApplyControlLayouts();
        }

        private void SetupControlRoot(GameObject controlRoot, InputDriver inputDriver, bool isLandscape)
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

            var twoSides = FindDescendant(mobileRoot, "TwoSides") as RectTransform;
            var halfLeft = FindDescendant(mobileRoot, "HalfLeft") as RectTransform;
            var halfRight = FindDescendant(mobileRoot, "HalfRight") as RectTransform;

            if ((twoSides == null) || (halfLeft == null) || (halfRight == null))
            {
                return;
            }

            var dpad = FindDescendant(halfLeft, "Dpad") as RectTransform;
            var fires = FindDescendant(halfRight, "Normal") as RectTransform;

            Canvas canvas = controlRoot.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                return;
            }

            // Size and position the pads relative to the whole screen (canvas),
            // not the scene-designed halves - those are not symmetric, so pads
            // would end up sized and aligned differently per side
            var canvasSize = ((RectTransform)canvas.transform).rect.size;
            if (Mathf.Min(canvasSize.x, canvasSize.y) < 100f)
            {
                // The inactive orientation canvas may not be laid out yet
                canvasSize = isLandscape ? new Vector2(2400f, 1080f) : new Vector2(1080f, 2400f);
            }

            var info = new ControlRootInfo()
            {
                isLandscape = isLandscape,
                canvasSize = canvasSize,
                padBaseWidth = Mathf.Clamp(Mathf.Min(canvasSize.x, canvasSize.y) * 0.42f, 300f, 700f),
                twoSides = twoSides,
                inputDriver = inputDriver,
                assets = CollectPadAssets(dpad, fires),
                left = new ControlSide()
                {
                    half = halfLeft,
                    original = (dpad != null) ? dpad.gameObject : null,
                    isLeft = true
                },
                right = new ControlSide()
                {
                    half = halfRight,
                    original = (fires != null) ? fires.gameObject : null,
                    isLeft = false
                }
            };

            // The burger (menu) and gear (setting) buttons live under the
            // canvas-level "Absolute" overlay of the same canvas; the runtime
            // pads are parented there too so they can be screen-anchored
            var absolute = canvas.transform.Find("Absolute") as RectTransform;

            info.padParent = (absolute != null) ? absolute : (RectTransform)canvas.transform;

            if (absolute != null)
            {
                info.menuButton = absolute.Find("Menu") as RectTransform;
                info.settingButton = absolute.Find("Setting") as RectTransform;

                info.menuOriginal = CapturePlacement(info.menuButton);
                info.settingOriginal = CapturePlacement(info.settingButton);
            }

            controlRoots.Add(info);
        }

        private static PadAssets CollectPadAssets(RectTransform dpad, RectTransform fires)
        {
            var assets = new PadAssets();

            assets.up = SpriteOf(dpad, "Up", out Color directionColor);
            assets.down = SpriteOf(dpad, "Down", out _);
            assets.left = SpriteOf(dpad, "Left", out _);
            assets.right = SpriteOf(dpad, "Right", out _);
            assets.fire1 = SpriteOf(fires, "Fire1", out Color fireColor);
            assets.fire2 = SpriteOf(fires, "Fire2", out _);

            if (assets.up != null)
            {
                assets.directionColor = directionColor;
            }

            if (assets.fire1 != null)
            {
                assets.fireColor = fireColor;
            }

            return assets;
        }

        private static Sprite SpriteOf(RectTransform root, string name, out Color color)
        {
            color = Color.white;

            var target = (root != null) ? FindDescendant(root, name) : null;
            var image = (target != null) ? target.GetComponent<UnityEngine.UI.Image>() : null;

            if (image == null)
            {
                return null;
            }

            color = image.color;
            return image.sprite;
        }

        private static OverlayPlacement CapturePlacement(RectTransform target)
        {
            if (target == null)
            {
                return default;
            }

            return new OverlayPlacement()
            {
                anchorMin = target.anchorMin,
                anchorMax = target.anchorMax,
                pivot = target.pivot,
                anchoredPosition = target.anchoredPosition
            };
        }

        private static void RestorePlacement(RectTransform target, OverlayPlacement placement)
        {
            target.anchorMin = placement.anchorMin;
            target.anchorMax = placement.anchorMax;
            target.pivot = placement.pivot;
            target.anchoredPosition = placement.anchoredPosition;
        }

        private void ApplyControlLayouts()
        {
            foreach (var info in controlRoots)
            {
                ApplySide(info, info.left, leftControlPad);
                ApplySide(info, info.right, rightControlPad);

                PlaceOverlayButtons(info);
            }
        }

        private void ApplySide(ControlRootInfo info, ControlSide side, Settings.ControlPadType wanted)
        {
            // The scene-designed controls are aligned and sized differently per
            // side; runtime pads are used everywhere so every side and pad type
            // follows the same placement rules.
            if (side.original != null)
            {
                side.original.SetActive(false);
            }

            if (!side.built.ContainsKey(wanted))
            {
                side.built[wanted] = BuildPad(info, side, wanted);
            }

            foreach (var pad in side.built)
            {
                if (pad.Value != null)
                {
                    pad.Value.SetActive(pad.Key == wanted);
                }
            }
        }

        private GameObject BuildPad(ControlRootInfo info, ControlSide side, Settings.ControlPadType type)
        {
            float width;
            float height;

            switch (type)
            {
                case Settings.ControlPadType.Keypad:
                    width = info.padBaseWidth;
                    height = width * 4f / 3f;
                    break;

                case Settings.ControlPadType.ABButtons:
                    width = info.padBaseWidth * 0.9f;
                    height = width * 0.45f;
                    break;

                default:
                    width = info.padBaseWidth * 0.95f;
                    height = width;
                    break;
            }

            // Cap the pad height to the lower part of the screen, so pads can
            // never reach up into the displayed game content
            float maxWidth = (info.canvasSize.x * 0.5f) - (PadEdgeMargin * 2f);
            float maxHeight = (info.canvasSize.y * (info.isLandscape ? 0.5f : 0.35f)) - PadBottomMargin;

            float scale = Mathf.Min(1f, Mathf.Min(maxWidth / width, maxHeight / height));
            width *= scale;
            height *= scale;

            var padGo = new GameObject($"Pad{type}", typeof(RectTransform));
            padGo.transform.SetParent(info.padParent, false);

            // Keep the menu/setting buttons (siblings in the overlay) on top
            padGo.transform.SetAsFirstSibling();

            var padRect = padGo.GetComponent<RectTransform>();
            padRect.pivot = new Vector2(0.5f, 0f);
            padRect.sizeDelta = new Vector2(width, height);

            // Uniform, screen-anchored placement for every pad type on both
            // sides: rest on a common baseline above the screen bottom. In
            // portrait each pad is centered in its half of the screen width,
            // in landscape pads hug the outer screen edges so the middle stays
            // free for the game display.
            if (info.isLandscape)
            {
                float anchorX = side.isLeft ? 0f : 1f;
                float offsetX = (side.isLeft ? 1f : -1f) * (PadEdgeMargin + (width * 0.5f));

                padRect.anchorMin = new Vector2(anchorX, 0f);
                padRect.anchorMax = new Vector2(anchorX, 0f);
                padRect.anchoredPosition = new Vector2(offsetX, PadBottomMargin);
            }
            else
            {
                float anchorX = side.isLeft ? 0.25f : 0.75f;

                padRect.anchorMin = new Vector2(anchorX, 0f);
                padRect.anchorMax = new Vector2(anchorX, 0f);
                padRect.anchoredPosition = new Vector2(0f, PadBottomMargin);
            }

            switch (type)
            {
                case Settings.ControlPadType.Keypad:
                    BuildKeypadContent(padGo, info);
                    break;

                case Settings.ControlPadType.ABButtons:
                    BuildABContent(padGo, info);
                    break;

                default:
                    BuildDPadContent(padGo, info, includeDiagonals: (type == Settings.ControlPadType.DiagonalDPad));
                    break;
            }

            return padGo;
        }

        private void BuildKeypadContent(GameObject padGo, ControlRootInfo info)
        {
            var padRect = padGo.GetComponent<RectTransform>();

            const float spacing = 10f;
            var cellWidth = (padRect.sizeDelta.x - (spacing * 2f)) / 3f;
            var cellHeight = (padRect.sizeDelta.y - (spacing * 3f)) / 4f;

            var keypadImage = padGo.AddComponent<UnityEngine.UI.Image>();
            keypadImage.color = new Color(0f, 0f, 0f, 0.15f);
            keypadImage.raycastTarget = false;

            var keypadGroup = padGo.AddComponent<CanvasGroup>();
            keypadGroup.alpha = 1f;

            var grid = padGo.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.spacing = new Vector2(spacing, spacing);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.cellSize = new Vector2(cellWidth, cellHeight);

            foreach (var keypadKey in GetKeypadKeys())
            {
                CreateKeyButton(padGo.transform, info.inputDriver, keypadKey);
            }
        }

        private void BuildABContent(GameObject padGo, ControlRootInfo info)
        {
            var padRect = padGo.GetComponent<RectTransform>();

            float buttonSize = Mathf.Min(padRect.sizeDelta.y, padRect.sizeDelta.x * 0.45f);
            float offset = padRect.sizeDelta.x * 0.25f;

            CreatePadButton(padGo.transform, info.inputDriver, '5', new Vector2(-offset, 0f),
                new Vector2(buttonSize, buttonSize), info.assets.fire1, 0f, info.assets.fireColor, "A");
            CreatePadButton(padGo.transform, info.inputDriver, '#', new Vector2(offset, 0f),
                new Vector2(buttonSize, buttonSize), info.assets.fire2, 0f, info.assets.fireColor, "B");
        }

        private void BuildDPadContent(GameObject padGo, ControlRootInfo info, bool includeDiagonals)
        {
            var padRect = padGo.GetComponent<RectTransform>();

            float cell = padRect.sizeDelta.x / 3f;
            var buttonSize = new Vector2(cell * 0.95f, cell * 0.95f);
            var assets = info.assets;

            Vector2 CellPosition(int column, int row)
            {
                return new Vector2((column - 1) * cell, (1 - row) * cell);
            }

            CreatePadButton(padGo.transform, info.inputDriver, '2', CellPosition(1, 0), buttonSize, assets.up, 0f, assets.directionColor, "^");
            CreatePadButton(padGo.transform, info.inputDriver, '4', CellPosition(0, 1), buttonSize, assets.left, 0f, assets.directionColor, "<");
            CreatePadButton(padGo.transform, info.inputDriver, '6', CellPosition(2, 1), buttonSize, assets.right, 0f, assets.directionColor, ">");
            CreatePadButton(padGo.transform, info.inputDriver, '8', CellPosition(1, 2), buttonSize, assets.down, 0f, assets.directionColor, "v");

            if (includeDiagonals)
            {
                CreatePadButton(padGo.transform, info.inputDriver, '1', CellPosition(0, 0), buttonSize, assets.up, 45f, assets.directionColor, "\\");
                CreatePadButton(padGo.transform, info.inputDriver, '3', CellPosition(2, 0), buttonSize, assets.up, -45f, assets.directionColor, "/");
                CreatePadButton(padGo.transform, info.inputDriver, '7', CellPosition(0, 2), buttonSize, assets.down, -45f, assets.directionColor, "/");
                CreatePadButton(padGo.transform, info.inputDriver, '9', CellPosition(2, 2), buttonSize, assets.down, 45f, assets.directionColor, "\\");
            }
        }

        private static GameObject CreatePadButton(Transform parent, InputDriver inputDriver, char key,
            Vector2 anchoredPosition, Vector2 size, Sprite sprite, float rotation, Color color, string fallbackLabel)
        {
            var buttonGo = new GameObject($"Key_{key}",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button),
                typeof(global::Nofun.TouchKeypadButton));

            buttonGo.transform.SetParent(parent, false);

            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            rect.localEulerAngles = new Vector3(0f, 0f, rotation);

            var image = buttonGo.GetComponent<UnityEngine.UI.Image>();

            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = color;
                image.preserveAspect = true;
            }
            else
            {
                image.color = new Color(1f, 1f, 1f, 0.75f);
                AddButtonLabel(buttonGo, fallbackLabel);
            }

            var touchHandler = buttonGo.GetComponent<global::Nofun.TouchKeypadButton>();
            touchHandler.Init(inputDriver, key);

            return buttonGo;
        }

        private void PlaceOverlayButtons(ControlRootInfo info)
        {
            if ((info.menuButton == null) && (info.settingButton == null))
            {
                return;
            }

            List<Rect> obstacles = CollectActiveControlRects(info);

            if (info.menuButton != null)
            {
                PlaceOverlayButton(info, info.menuButton, info.menuOriginal, obstacles);
                obstacles.Add(WorldRectOf(info.menuButton));
            }

            if (info.settingButton != null)
            {
                PlaceOverlayButton(info, info.settingButton, info.settingOriginal, obstacles);
            }
        }

        private List<Rect> CollectActiveControlRects(ControlRootInfo info)
        {
            var rects = new List<Rect>();

            void AddSide(ControlSide side)
            {
                if ((side.original != null) && side.original.activeSelf)
                {
                    rects.Add(WorldRectOfContent((RectTransform)side.original.transform));
                }

                foreach (var pad in side.built)
                {
                    if ((pad.Value != null) && pad.Value.activeSelf)
                    {
                        rects.Add(WorldRectOf((RectTransform)pad.Value.transform));
                    }
                }
            }

            AddSide(info.left);
            AddSide(info.right);

            return rects;
        }

        private void PlaceOverlayButton(ControlRootInfo info, RectTransform button, OverlayPlacement original, List<Rect> obstacles)
        {
            const float margin = 20f;

            RestorePlacement(button, original);

            if (!OverlapsAny(WorldRectOf(button), obstacles, margin))
            {
                return;
            }

            // Candidate spots inside the control area, corners and edge centers.
            // In landscape the middle of the area is covered by the game display,
            // so only corners are considered there.
            Vector2[] candidates = info.isLandscape
                ? new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) }
                : new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f) };

            foreach (var candidate in candidates)
            {
                MoveToAreaPoint(button, info.twoSides, candidate, margin);

                if (!OverlapsAny(WorldRectOf(button), obstacles, margin * 0.5f))
                {
                    return;
                }
            }
        }

        private static void MoveToAreaPoint(RectTransform button, RectTransform area, Vector2 normalizedPoint, float margin)
        {
            Rect areaRect = area.rect;

            var localPoint = new Vector2(
                Mathf.Lerp(areaRect.xMin + margin, areaRect.xMax - margin, normalizedPoint.x),
                Mathf.Lerp(areaRect.yMin + margin, areaRect.yMax - margin, normalizedPoint.y));

            Vector3 worldPoint = area.TransformPoint(localPoint);

            button.pivot = normalizedPoint;
            button.position = new Vector3(worldPoint.x, worldPoint.y, button.position.z);
        }

        private static bool OverlapsAny(Rect rect, List<Rect> obstacles, float margin)
        {
            var grown = new Rect(rect.xMin - margin, rect.yMin - margin,
                rect.width + (margin * 2f), rect.height + (margin * 2f));

            foreach (var obstacle in obstacles)
            {
                if (grown.Overlaps(obstacle))
                {
                    return true;
                }
            }

            return false;
        }

        private static Rect WorldRectOf(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            var min = Vector3.Min(Vector3.Min(corners[0], corners[1]), Vector3.Min(corners[2], corners[3]));
            var max = Vector3.Max(Vector3.Max(corners[0], corners[1]), Vector3.Max(corners[2], corners[3]));

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect WorldRectOfContent(RectTransform root)
        {
            Rect result = WorldRectOf(root);

            foreach (Transform child in root)
            {
                if (child is RectTransform childRect)
                {
                    var childWorld = WorldRectOf(childRect);
                    result = Rect.MinMaxRect(
                        Mathf.Min(result.xMin, childWorld.xMin),
                        Mathf.Min(result.yMin, childWorld.yMin),
                        Mathf.Max(result.xMax, childWorld.xMax),
                        Mathf.Max(result.yMax, childWorld.yMax));
                }
            }

            return result;
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

            AddButtonLabel(buttonGo, keypadKey.ToString());
        }

        private static void AddButtonLabel(GameObject buttonGo, string label)
        {
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(buttonGo.transform, false);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 40;
            text.color = Color.black;
            text.raycastTarget = false;
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
