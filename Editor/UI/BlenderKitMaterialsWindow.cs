using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BlenderKitUnity.Editor.API;
using BlenderKitUnity.Editor.Pipeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlenderKitUnity.Editor.UI
{
    public sealed class BlenderKitMaterialsWindow : EditorWindow
    {
        private static readonly string[] Categories = new string[]
        {
            "All", "Wood", "Stone", "Metal", "Fabric", "Ground", "Concrete",
            "Marble", "Tiles", "Plaster", "Glass", "Plastic", "Leather", "Organic", "Roof", "Wall"
        };

        private static readonly string[] Resolutions = new string[]
        {
            "512", "1K", "2K", "4K"
        };

        private BlenderKitApiClient apiClient;
        private BlenderKitMaterialService materialService;
        private CancellationTokenSource searchCts;

        private VisualElement root;
        private TextField searchTextField;
        private PopupField<string> categoryPopup;
        private PopupField<string> resolutionPopup;
        private Toggle freeOnlyToggle;
        private Toggle purePbrToggle;
        private VisualElement settingsContainer;
        private TextField blenderPathField;
        private TextField apiKeyField;
        private TextField targetFolderField;

        private ScrollView materialsScrollView;
        private VisualElement materialsGrid;
        private Label statusLabel;
        private Button prevPageBtn;
        private Button nextPageBtn;
        private Label pageInfoLabel;

        private int currentPage = 1;
        private int totalCount = 0;
        private const int PageSize = 24;

        [MenuItem("Window/BlenderKit/Materials Browser")]
        public static void Open()
        {
            var window = GetWindow<BlenderKitMaterialsWindow>();
            window.titleContent = new GUIContent("BlenderKit Materials");
            window.minSize = new Vector2(580, 500);
            window.Show();
        }

        private void OnEnable()
        {
            apiClient = new BlenderKitApiClient();
            materialService = new BlenderKitMaterialService(apiClient);
        }

        private void OnDisable()
        {
            searchCts?.Cancel();
            apiClient?.Dispose();
        }

        public void CreateGUI()
        {
            root = rootVisualElement;
            root.AddToClassList("root-container");

            LoadStyleSheet();

            BuildToolbar();
            BuildSettingsPanel();
            BuildInfoBanner();
            BuildMaterialsView();
            BuildFooter();

            _ = TriggerSearchAsync();
        }

        private void LoadStyleSheet()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BlenderKitMaterialsWindow).Assembly);
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.assetPath))
            {
                string packageUssPath = $"{packageInfo.assetPath}/Editor/UI/BlenderKitStyles.uss";
                var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(packageUssPath);
                if (style != null)
                {
                    root.styleSheets.Add(style);
                    return;
                }
            }

            string[] guids = AssetDatabase.FindAssets("BlenderKitStyles");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase))
                {
                    var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (styleSheet != null)
                    {
                        root.styleSheets.Add(styleSheet);
                        return;
                    }
                }
            }

            string directPath = "Packages/com.blenderkit.unity/Editor/UI/BlenderKitStyles.uss";
            var directStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(directPath);
            if (directStyle != null)
            {
                root.styleSheets.Add(directStyle);
            }
        }

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar-panel");

            searchTextField = new TextField { placeholderText = "Search materials..." };
            searchTextField.AddToClassList("search-field");
            searchTextField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    currentPage = 1;
                    _ = TriggerSearchAsync();
                }
            });
            toolbar.Add(searchTextField);

            categoryPopup = new PopupField<string>(new List<string>(Categories), 0);
            categoryPopup.AddToClassList("dropdown-field");
            categoryPopup.RegisterValueChangedCallback(_ =>
            {
                currentPage = 1;
                _ = TriggerSearchAsync();
            });
            toolbar.Add(categoryPopup);

            int defaultResIndex = Math.Max(0, Array.IndexOf(Resolutions, BlenderKitSettings.DefaultResolution));
            resolutionPopup = new PopupField<string>(new List<string>(Resolutions), defaultResIndex);
            resolutionPopup.AddToClassList("dropdown-field");
            resolutionPopup.RegisterValueChangedCallback(evt =>
            {
                BlenderKitSettings.DefaultResolution = evt.newValue;
            });
            toolbar.Add(resolutionPopup);

            freeOnlyToggle = new Toggle("Free") { value = true };
            freeOnlyToggle.AddToClassList("toggle-field");
            freeOnlyToggle.RegisterValueChangedCallback(_ =>
            {
                currentPage = 1;
                _ = TriggerSearchAsync();
            });
            toolbar.Add(freeOnlyToggle);

            purePbrToggle = new Toggle("PBR Only") { value = BlenderKitSettings.PurePbrOnly };
            purePbrToggle.AddToClassList("toggle-field");
            purePbrToggle.RegisterValueChangedCallback(evt =>
            {
                BlenderKitSettings.PurePbrOnly = evt.newValue;
                currentPage = 1;
                _ = TriggerSearchAsync();
            });
            toolbar.Add(purePbrToggle);

            var searchBtn = new Button(() =>
            {
                currentPage = 1;
                _ = TriggerSearchAsync();
            })
            { text = "Search" };
            searchBtn.AddToClassList("action-btn");
            toolbar.Add(searchBtn);

            var settingsToggleBtn = new Button(ToggleSettingsPanel) { text = "Settings" };
            settingsToggleBtn.AddToClassList("action-btn");
            toolbar.Add(settingsToggleBtn);

            root.Add(toolbar);
        }

        private void BuildSettingsPanel()
        {
            settingsContainer = new VisualElement();
            settingsContainer.AddToClassList("settings-panel");
            settingsContainer.style.display = DisplayStyle.None;

            var title = new Label("BlenderKit & Blender Configuration");
            title.AddToClassList("settings-title");
            settingsContainer.Add(title);

            var pathRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            blenderPathField = new TextField("Blender Path") { value = BlenderKitSettings.BlenderExecutablePath };
            blenderPathField.style.flexGrow = 1;
            pathRow.Add(blenderPathField);

            var browseBtn = new Button(() =>
            {
                string selected = EditorUtility.OpenFilePanel("Select blender.exe", "", "exe");
                if (!string.IsNullOrEmpty(selected))
                {
                    blenderPathField.value = selected;
                    BlenderKitSettings.BlenderExecutablePath = selected;
                }
            })
            { text = "Browse..." };
            pathRow.Add(browseBtn);
            settingsContainer.Add(pathRow);

            apiKeyField = new TextField("API Key (Optional)") { value = BlenderKitSettings.ApiKey, isPasswordField = true };
            apiKeyField.style.marginBottom = 4;
            apiKeyField.RegisterValueChangedCallback(evt => BlenderKitSettings.ApiKey = evt.newValue);
            settingsContainer.Add(apiKeyField);

            targetFolderField = new TextField("Save Folder") { value = BlenderKitSettings.TargetFolder };
            targetFolderField.style.marginBottom = 6;
            targetFolderField.RegisterValueChangedCallback(evt => BlenderKitSettings.TargetFolder = evt.newValue);
            settingsContainer.Add(targetFolderField);

            root.Add(settingsContainer);
        }

        private void ToggleSettingsPanel()
        {
            bool isVisible = settingsContainer.style.display == DisplayStyle.Flex;
            settingsContainer.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void BuildInfoBanner()
        {
            var banner = new Label("Tip: Drag any downloaded material card directly onto a 3D object in the Scene View.");
            banner.style.paddingTop = 4;
            banner.style.paddingBottom = 4;
            banner.style.paddingLeft = 12;
            banner.style.paddingRight = 12;
            banner.style.backgroundColor = new Color(0.12f, 0.28f, 0.4f, 0.5f);
            banner.style.color = new Color(0.8f, 0.9f, 1f);
            banner.style.fontSize = 11;
            banner.style.borderBottomWidth = 1;
            banner.style.borderBottomColor = new Color(0.2f, 0.4f, 0.6f, 0.5f);
            root.Add(banner);
        }

        private void BuildMaterialsView()
        {
            materialsScrollView = new ScrollView(ScrollViewMode.Vertical);
            materialsScrollView.AddToClassList("materials-scroll");

            statusLabel = new Label("Initializing...");
            statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            statusLabel.style.marginTop = 20;
            statusLabel.style.fontSize = 13;
            materialsScrollView.Add(statusLabel);

            materialsGrid = new VisualElement();
            materialsGrid.AddToClassList("materials-grid");
            materialsScrollView.Add(materialsGrid);

            root.Add(materialsScrollView);
        }

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("footer-panel");

            pageInfoLabel = new Label("Showing 0 materials");
            pageInfoLabel.AddToClassList("page-label");
            footer.Add(pageInfoLabel);

            var pagination = new VisualElement();
            pagination.AddToClassList("pagination-controls");

            prevPageBtn = new Button(OnPrevPageClicked) { text = "◀ Previous" };
            prevPageBtn.AddToClassList("action-btn");
            prevPageBtn.SetEnabled(false);
            pagination.Add(prevPageBtn);

            nextPageBtn = new Button(OnNextPageClicked) { text = "Next ▶" };
            nextPageBtn.AddToClassList("action-btn");
            nextPageBtn.SetEnabled(false);
            pagination.Add(nextPageBtn);

            footer.Add(pagination);
            root.Add(footer);
        }

        private async Task TriggerSearchAsync()
        {
            searchCts?.Cancel();
            searchCts = new CancellationTokenSource();

            statusLabel.text = "Searching BlenderKit materials...";
            statusLabel.style.display = DisplayStyle.Flex;
            materialsGrid.Clear();

            string keywords = searchTextField.value;
            string category = categoryPopup.value;
            bool freeOnly = freeOnlyToggle.value;
            bool purePbr = purePbrToggle.value;

            try
            {
                SearchResponse response = await apiClient.SearchMaterialsAsync(
                    keywords,
                    category,
                    currentPage,
                    freeOnly,
                    purePbr,
                    searchCts.Token);

                PopulateResults(response);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                statusLabel.text = $"Error loading materials: {ex.Message}";
                statusLabel.style.display = DisplayStyle.Flex;
                throw new InvalidOperationException($"BlenderKit search encountered an error: {ex.Message}", ex);
            }
        }

        private void PopulateResults(SearchResponse response)
        {
            materialsGrid.Clear();
            totalCount = response.count;

            if (response.results == null || response.results.Count == 0)
            {
                statusLabel.text = "No materials found matching your criteria.";
                statusLabel.style.display = DisplayStyle.Flex;
                UpdatePaginationControls();
                return;
            }

            statusLabel.style.display = DisplayStyle.None;

            for (int i = 0; i < response.results.Count; i++)
            {
                var card = new MaterialCardElement(
                    response.results[i],
                    apiClient,
                    materialService,
                    () => resolutionPopup.value);

                materialsGrid.Add(card);
            }

            UpdatePaginationControls();
        }

        private void UpdatePaginationControls()
        {
            int totalPages = Mathf.Max(1, Mathf.CeilingToInt((float)totalCount / PageSize));
            pageInfoLabel.text = $"Page {currentPage} of {totalPages} ({totalCount} materials)";
            prevPageBtn.SetEnabled(currentPage > 1);
            nextPageBtn.SetEnabled(currentPage < totalPages);
        }

        private void OnPrevPageClicked()
        {
            if (currentPage > 1)
            {
                currentPage--;
                _ = TriggerSearchAsync();
            }
        }

        private void OnNextPageClicked()
        {
            currentPage++;
            _ = TriggerSearchAsync();
        }
    }
}
