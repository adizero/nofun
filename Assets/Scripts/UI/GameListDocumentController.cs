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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Nofun.Data;
using Nofun.Data.Model;
using Nofun.DynamicIcons;
using Nofun.Parser;
using Nofun.Services;
using Nofun.Plugins;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Nofun.UI
{
    public class GameListDocumentController : FlexibleUIDocumentController, IGameProvider
    {
        private static readonly string GameDatabaseFileName = "games.db";
        private string GameDatabasePath => $"{Application.persistentDataPath}/{GameDatabaseFileName}";

        private Button installButton;
        private VisualElement gameList;
        private GameDatabase gameDatabase;
        private TextField searchBar;

        [Header("UI")]
        [SerializeField] private VisualTreeAsset gameEntryTemplate;
        [SerializeField] private GameIconManifest gameIconManifest;
        [SerializeField] private Transform dynamicIconRendererContainer;
        [SerializeField] private GameDetailsDocumentController gameDetailsDocumentController;
        [SerializeField] private Vector2 defaultIconSize = new Vector2(180.0f, 210.0f);
        [SerializeField] private float iconPaddingReservePercentage = 6;

        [Header("Runner")]
        [SerializeField] private NofunRunner runner;

        [Inject] private ITranslationService translationService;
        [Inject] private IDialogService dialogService;
        [Inject] private ILayoutService layoutService;
        private DynamicIconsProvider dynamicIconsProvider;

        private string GamePathRoot => $"{Application.persistentDataPath}/__Games";

        public string GetGamePath(string gameFileName)
        {
            return $"{GamePathRoot}/{gameFileName}";
        }

        private string GetGamePath(GameInfo gameInfo) => GetGamePath(gameInfo.GameFileName);

        public override void Awake()
        {
            base.Awake();

            if (!File.Exists(GameDatabasePath))
            {
                TextAsset asset = Resources.Load(GameDatabaseFileName) as TextAsset;

                if (asset != null)
                {
                    File.WriteAllBytes(GameDatabasePath, asset.bytes);
                }
            }

            gameDatabase = new GameDatabase(GameDatabasePath);
            dynamicIconsProvider = new DynamicIconsProvider(dynamicIconRendererContainer);

            Directory.CreateDirectory(GamePathRoot);
        }

        private void OnEnable()
        {
            layoutService.SetVisibility(false);

            installButton = document.rootVisualElement.Q<Button>("InstallButton");
            gameList = document.rootVisualElement.Q<VisualElement>("GameList");
            searchBar = document.rootVisualElement.Q<TextField>("SearchBar");
            installButton.clicked += OnInstallButtonClicked;
            gameDetailsDocumentController.OnGameInfoChoosen += OnGameIconClicked;
            gameDetailsDocumentController.OnGameRemovalRequested += RemoveGame;

            searchBar.RegisterValueChangedCallback(OnSearchBarContentChanged);

            gameList.RegisterCallback<GeometryChangedEvent>((_) =>
            {
                LoadGameList();
            });
        }

        private void OnDisable()
        {
            layoutService.SetVisibility(true);
            dynamicIconsProvider.Cleanup();

            installButton.clicked -= OnInstallButtonClicked;
            gameDetailsDocumentController.OnGameInfoChoosen -= OnGameIconClicked;
            gameDetailsDocumentController.OnGameRemovalRequested -= RemoveGame;
        }

        private void OnSearchBarContentChanged(ChangeEvent<string> newValue)
        {
            LoadGameList(newValue.newValue);
        }

        private void OnGameIconClicked(string gameFileName)
        {
            if (runner != null)
            {
                string gamePath = GetGamePath(gameFileName);
                if (!File.Exists(gamePath))
                {
                    dialogService.Show(Severity.Error,
                        ButtonType.OK,
                        translationService.Translate("Error"),
                        translationService.Translate("Error_Description_NoGameFileFound"),
                        null);

                    return;
                }

                runner.gameObject.SetActive(true);
                runner.Launch(gamePath);

                ImmediateHide();
            }
        }

        private void LoadGameList(string filter = "")
        {
            var gameInfos = string.IsNullOrEmpty(filter) ? gameDatabase.AllGames : gameDatabase.GamesByKeyword(filter);
            RebuildGameList(gameInfos);
        }

        private void RebuildGameList(GameInfo[] gameInfos)
        {
            foreach (var child in gameList.Children())
            {
                if (child.userData is GameInfoEntryController controller)
                {
                    controller.OnGameInfoChoosen -= OnGameIconClicked;
                }
            }

            gameList.Clear();

            Vector2? sizeIcon = null;

            foreach (var gameInfo in gameInfos)
            {
                var gameInfoEntry = gameEntryTemplate.Instantiate();

                if (sizeIcon == null)
                {
                    float actualResolvedWidth = gameList.resolvedStyle.width * (100.0f - iconPaddingReservePercentage) / 100.0f;
                    int totalIconEachRow = Mathf.RoundToInt(actualResolvedWidth / defaultIconSize.x);
                    float actualWidth = actualResolvedWidth / totalIconEachRow;
                    float scaleFactor = actualWidth / defaultIconSize.x;

                    sizeIcon = new Vector2(actualWidth, defaultIconSize.y * scaleFactor);
                }

                var gameInfoEntryBinder = new GameInfoEntryController(gameIconManifest, dynamicIconsProvider, gameDetailsDocumentController);

                gameInfoEntryBinder.SetVisualElement(gameInfoEntry);
                gameInfoEntryBinder.BindData(gameInfo);
                gameInfoEntryBinder.OnGameInfoChoosen += OnGameIconClicked;

                gameInfoEntry.style.width = sizeIcon.Value.x;
                gameInfoEntry.style.height = sizeIcon.Value.y;

                gameList.Add(gameInfoEntry);
            }
        }

        private void RemoveGame(GameInfo gameInfo)
        {
            string gamePath = GetGamePath(gameInfo);
            if (File.Exists(gamePath))
            {
                File.Delete(gamePath);
            }

            gameDatabase.RemoveGame(gameInfo);
            LoadGameList();
        }

        private void InstallGame(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            byte[] rawData;
            try
            {
                rawData = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read '{path}': {ex}");

                dialogService.Show(Severity.Error,
                    ButtonType.OK,
                    translationService.Translate("Error"),
                    translationService.Translate("Error_Description_NotMophun"),
                    null);

                return;
            }

            // A .zip may bundle one or more games; unpack and route accordingly.
            if (LooksLikeZip(rawData))
            {
                InstallFromZip(rawData);
                return;
            }

            InstallGameFromData(rawData, Path.GetFileName(path), SafeLastWriteTime(path));
        }

        private static bool LooksLikeZip(byte[] data)
        {
            // Local file header ("PK\x03\x04") or an empty archive ("PK\x05\x06").
            return (data.Length >= 4) && (data[0] == 'P') && (data[1] == 'K')
                && (((data[2] == 3) && (data[3] == 4)) || ((data[2] == 5) && (data[3] == 6)));
        }

        private static DateTime SafeLastWriteTime(string path)
        {
            try
            {
                return File.GetLastWriteTime(path);
            }
            catch
            {
                // Some content providers hide the timestamp; fall back to now.
                return DateTime.Now;
            }
        }

        private sealed class ZipGameCandidate
        {
            public string name;
            public byte[] data;
            public DateTime date;
        }

        private void InstallFromZip(byte[] zipData)
        {
            var candidates = new List<ZipGameCandidate>();

            try
            {
                using (var zipStream = new MemoryStream(zipData))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        // Skip directory markers and anything that is not a .mpn.
                        if (string.IsNullOrEmpty(entry.Name) ||
                            !entry.Name.EndsWith(".mpn", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        using (var entryStream = entry.Open())
                        using (var buffer = new MemoryStream())
                        {
                            entryStream.CopyTo(buffer);

                            candidates.Add(new ZipGameCandidate()
                            {
                                name = entry.Name,
                                data = buffer.ToArray(),
                                date = entry.LastWriteTime.DateTime
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read zip archive: {ex}");

                dialogService.Show(Severity.Error,
                    ButtonType.OK,
                    translationService.Translate("Error"),
                    translationService.Translate("Error_Description_NotMophun"),
                    null);

                return;
            }

            if (candidates.Count == 0)
            {
                dialogService.Show(Severity.Error,
                    ButtonType.OK,
                    translationService.Translate("Error"),
                    translationService.Translate("Error_Description_NoGameFileFound"),
                    null);

                return;
            }

            if (candidates.Count == 1)
            {
                var only = candidates[0];
                InstallGameFromData(only.data, only.name, only.date);
                return;
            }

            ShowZipChooser(candidates);
        }

        // Presents the bundled games as a scrollable list overlay built on this
        // controller's own panel, so the user picks one directly.
        private void ShowZipChooser(List<ZipGameCandidate> candidates)
        {
            var root = document.rootVisualElement;

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.7f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;

            var panel = new VisualElement();
            panel.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f, 1.0f);
            panel.style.paddingLeft = 16;
            panel.style.paddingRight = 16;
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 16;
            panel.style.minWidth = 320;
            panel.style.maxWidth = 560;
            panel.style.width = Length.Percent(80);
            panel.style.maxHeight = Length.Percent(80);
            panel.style.borderTopLeftRadius = 10;
            panel.style.borderTopRightRadius = 10;
            panel.style.borderBottomLeftRadius = 10;
            panel.style.borderBottomRightRadius = 10;
            overlay.Add(panel);

            var title = new Label("Select a game to install");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 20;
            title.style.color = Color.white;
            title.style.marginBottom = 12;
            panel.Add(title);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            panel.Add(scroll);

            foreach (var candidate in candidates)
            {
                var entry = candidate;
                var button = new Button(() =>
                {
                    root.Remove(overlay);
                    InstallGameFromData(entry.data, entry.name, entry.date);
                });

                button.text = $"{entry.name}  ({FormatSize(entry.data.LongLength)})";
                button.style.whiteSpace = WhiteSpace.Normal;
                button.style.marginBottom = 6;
                button.style.paddingTop = 10;
                button.style.paddingBottom = 10;
                scroll.Add(button);
            }

            var cancel = new Button(() => root.Remove(overlay));
            cancel.text = translationService.Translate("Cancel");
            cancel.style.marginTop = 12;
            panel.Add(cancel);

            root.Add(overlay);
        }

        private void InstallGameFromData(byte[] rawData, string sourceLabel, DateTime sourceDate)
        {
            try
            {
                // Compressed titles are inflated and encrypted ones decrypted so
                // the copy kept in the library is always plain and runnable.
                VMExecutableProcessor.ProcessResult processed = VMExecutableProcessor.Process(rawData);

                VMMetaInfoReader metaInfoReader;
                using (var executableStream = new MemoryStream(processed.data))
                {
                    VMGPExecutable executable = new VMGPExecutable(executableStream);
                    metaInfoReader = executable.GetMetaInfo();
                }

                if (metaInfoReader == null)
                {
                    dialogService.Show(Severity.Error,
                        ButtonType.OK,
                        translationService.Translate("Error"),
                        translationService.Translate("Error_Description_NoGameInfo"),
                        null);

                    return;
                }

                string titleName = metaInfoReader.Get("Title");
                string vendor = metaInfoReader.Get("Vendor");
                string version = metaInfoReader.Get("Program version");

                Debug.Log($"Title: {titleName}, Vendor: {vendor}, Version: {version}");

                if (titleName == null)
                {
                    dialogService.Show(Severity.Error,
                        ButtonType.OK,
                        translationService.Translate("Error"),
                        translationService.Translate("Error_Description_NoGameTitle"),
                        null);

                    return;
                }

                int[] versionNumbers;

                try
                {
                    versionNumbers = (version == null)
                        ? null
                        : version.Split(".", StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                }
                catch
                {
                    versionNumbers = new[] { 0, 0, 0 };
                }

                GameInfo gameInfo = new GameInfo(titleName, vendor ?? null,
                    versionNumbers != null && versionNumbers.Length >= 1 ? versionNumbers[0] : 0,
                    versionNumbers != null && versionNumbers.Length >= 2 ? versionNumbers[1] : 0,
                    versionNumbers != null && versionNumbers.Length >= 3 ? versionNumbers[2] : 0);

                if (!gameDatabase.AddGame(gameInfo))
                {
                    dialogService.Show(Severity.Error,
                        ButtonType.OK,
                        translationService.Translate("Error"),
                        translationService.Translate("Error_Description_GameAlreadyInstalled"),
                        null);

                    return;
                }
                else
                {
                    // Save the processed (plain) game into the persistent folder
                    string gamePath = GetGamePath(gameInfo);
                    File.WriteAllBytes(gamePath, processed.data);

                    dialogService.Show(Severity.Info,
                        ButtonType.OK,
                        translationService.Translate("Success"),
                        BuildInstallStatus(gameInfo, version, processed, sourceDate),
                        null);

                    LoadGameList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Install failed for '{sourceLabel}': {ex}");

                dialogService.Show(Severity.Error,
                    ButtonType.OK,
                    translationService.Translate("Error"),
                    translationService.Translate("Error_Description_NotMophun"),
                    null);
            }
        }

        private string BuildInstallStatus(GameInfo gameInfo, string version,
            VMExecutableProcessor.ProcessResult processed, DateTime sourceDate)
        {
            // Stat labels are plain literals: the localization string tables are
            // Unity assets keyed by generated ids and cannot be extended safely
            // outside the editor. Only the pre-existing success line is localized.
            string format;
            if (processed.wasCompressed && processed.wasEncrypted)
            {
                format = "Compressed + encrypted";
            }
            else if (processed.wasCompressed)
            {
                format = "Compressed";
            }
            else if (processed.wasEncrypted)
            {
                format = "Encrypted";
            }
            else
            {
                format = "Plain";
            }

            DateTime importDate = DateTime.Now;

            var builder = new StringBuilder();
            builder.AppendLine(translationService.Translate("Success_Description_Install"));
            builder.AppendLine();
            builder.AppendLine($"{gameInfo.Name}");

            if (!string.IsNullOrEmpty(gameInfo.Vendor))
            {
                builder.AppendLine($"Vendor: {gameInfo.Vendor}");
            }

            if (!string.IsNullOrEmpty(version))
            {
                builder.AppendLine($"Version: {version}");
            }

            builder.AppendLine($"Format: {format}");
            builder.AppendLine($"Size: {FormatSize(processed.originalSize)}");

            if (processed.processedSize != processed.originalSize)
            {
                builder.AppendLine($"Stored size: {FormatSize(processed.processedSize)}");
            }

            builder.AppendLine($"File date: {sourceDate:yyyy-MM-dd HH:mm}");
            builder.Append($"Imported: {importDate:yyyy-MM-dd HH:mm}");

            return builder.ToString();
        }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unit = 0;

            while ((size >= 1024.0) && (unit < units.Length - 1))
            {
                size /= 1024.0;
                unit++;
            }

            return (unit == 0) ? $"{bytes} {units[unit]}" : $"{size:0.##} {units[unit]}";
        }

        private void OnInstallButtonClicked()
        {
            bool permissionGranted = FilePicker.OpenPickFileDialog(new FilterItem[]
            {
                #if UNITY_EDITOR || !UNITY_ANDROID
                new FilterItem
                {
                    name = "Mophun game",
                    spec = "mpn,zip"
                }
                #else
                // The Android document picker can only filter by MIME type and
                // .mpn has no registered one - what providers report for it
                // varies per device, and files not matching the filter cannot
                // be selected. Allow everything; InstallGame validates that the
                // picked file really is a Mophun executable.
                new FilterItem
                {
                    name = "Mophun game",
                    spec = "*/*"
                }
                #endif
            }, (string path) =>
            {
                if (!string.IsNullOrEmpty(path))
                {
                    InstallGame(path);
                }
            });

            if (!permissionGranted)
            {
                Debug.Log("Todo: Show error message not granted");
            }
        }

        public void ImmediateShow()
        {
            gameObject.SetActive(true);
        }

        public void ImmediateHide()
        {
            gameObject.SetActive(false);
        }
    }
}
