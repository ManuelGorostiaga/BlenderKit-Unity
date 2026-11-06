using System;
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
    public sealed class MaterialCardElement : VisualElement
    {
        private readonly AssetData assetData;
        private readonly BlenderKitApiClient apiClient;
        private readonly BlenderKitMaterialService materialService;
        private readonly Func<string> getSelectedResolution;

        private readonly Image thumbnailImage;
        private readonly Label titleLabel;
        private readonly Label categoryLabel;
        private readonly ProgressBar progressBar;
        private readonly Button downloadButton;
        private readonly Button applyButton;
        private readonly Label dragHandleLabel;

        private Vector2 pointerDownPosition;
        private bool isPointerDown;
        private Material cachedMaterial;
        private CancellationTokenSource downloadCts;

        public MaterialCardElement(
            AssetData asset,
            BlenderKitApiClient client,
            BlenderKitMaterialService service,
            Func<string> resolutionGetter)
        {
            assetData = asset ?? throw new ArgumentNullException(nameof(asset));
            apiClient = client ?? throw new ArgumentNullException(nameof(client));
            materialService = service ?? throw new ArgumentNullException(nameof(service));
            getSelectedResolution = resolutionGetter ?? throw new ArgumentNullException(nameof(resolutionGetter));

            AddToClassList("material-card");

            thumbnailImage = new Image();
            thumbnailImage.AddToClassList("card-thumbnail");
            Add(thumbnailImage);

            titleLabel = new Label(assetData.GetEffectiveName());
            titleLabel.AddToClassList("card-title");
            titleLabel.tooltip = assetData.GetEffectiveName();
            Add(titleLabel);

            categoryLabel = new Label(string.IsNullOrWhiteSpace(assetData.category) ? "Material" : assetData.category.ToUpperInvariant());
            categoryLabel.AddToClassList("card-category");
            Add(categoryLabel);

            progressBar = new ProgressBar();
            progressBar.AddToClassList("card-progress");
            progressBar.style.display = DisplayStyle.None;
            Add(progressBar);

            downloadButton = new Button(OnDownloadClicked) { text = "Download" };
            downloadButton.AddToClassList("card-btn");
            downloadButton.AddToClassList("download-btn");
            Add(downloadButton);

            applyButton = new Button(OnApplyClicked) { text = "Apply to Selected" };
            applyButton.AddToClassList("card-btn");
            applyButton.AddToClassList("apply-btn");
            applyButton.style.display = DisplayStyle.None;
            Add(applyButton);

            dragHandleLabel = new Label(":: DRAG TO SCENE ::");
            dragHandleLabel.AddToClassList("drag-handle");
            dragHandleLabel.style.display = DisplayStyle.None;
            Add(dragHandleLabel);

            RegisterDragEvents();
            RefreshState();
            _ = LoadThumbnailAsync();
        }

        private void RegisterDragEvents()
        {
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && cachedMaterial != null)
                {
                    isPointerDown = true;
                    pointerDownPosition = evt.position;
                    evt.StopPropagation();
                }
            });

            RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!isPointerDown || cachedMaterial == null) return;

                if (Vector2.Distance(pointerDownPosition, evt.position) > 6f)
                {
                    isPointerDown = false;
                    StartUnityDrag();
                    evt.StopPropagation();
                }
            });

            RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    isPointerDown = false;
                }
            });

            RegisterCallback<PointerCaptureOutEvent>(_ => isPointerDown = false);
        }

        private void StartUnityDrag()
        {
            if (cachedMaterial == null)
            {
                throw new InvalidOperationException("Cannot initiate drag for a material that has not been downloaded.");
            }

            string assetPath = AssetDatabase.GetAssetPath(cachedMaterial);
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new InvalidOperationException("Material asset path is invalid.");
            }

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new UnityEngine.Object[] { cachedMaterial };
            DragAndDrop.paths = new string[] { assetPath };
            DragAndDrop.StartDrag(cachedMaterial.name);
        }

        public void RefreshState()
        {
            if (materialService.TryGetExistingMaterial(assetData, out cachedMaterial))
            {
                AddToClassList("downloaded");
                downloadButton.style.display = DisplayStyle.None;
                applyButton.style.display = DisplayStyle.Flex;
                dragHandleLabel.style.display = DisplayStyle.Flex;
                progressBar.style.display = DisplayStyle.None;
            }
            else
            {
                RemoveFromClassList("downloaded");
                downloadButton.style.display = DisplayStyle.Flex;
                applyButton.style.display = DisplayStyle.None;
                dragHandleLabel.style.display = DisplayStyle.None;
                progressBar.style.display = DisplayStyle.None;
            }
        }

        private async void OnDownloadClicked()
        {
            downloadButton.SetEnabled(false);
            progressBar.style.display = DisplayStyle.Flex;
            progressBar.value = 0f;
            progressBar.title = "Downloading...";

            downloadCts?.Cancel();
            downloadCts = new CancellationTokenSource();

            var progress = new Progress<float>(percent =>
            {
                progressBar.value = percent * 100f;
                progressBar.title = $"Downloading {Mathf.RoundToInt(percent * 100f)}%";
            });

            try
            {
                string resolution = getSelectedResolution();
                Material result = await materialService.DownloadAndCreateMaterialAsync(
                    assetData,
                    resolution,
                    progress,
                    downloadCts.Token);

                cachedMaterial = result;
                progressBar.title = "Ready!";
                RefreshState();
            }
            catch (Exception ex)
            {
                progressBar.title = "Failed";
                downloadButton.SetEnabled(true);
                throw new InvalidOperationException($"Failed to download or extract material '{assetData.GetEffectiveName()}': {ex.Message}", ex);
            }
        }

        private void OnApplyClicked()
        {
            if (cachedMaterial == null)
            {
                throw new InvalidOperationException($"Material '{assetData.GetEffectiveName()}' is not ready.");
            }

            materialService.ApplyMaterialToSelection(cachedMaterial);
        }

        private async Task LoadThumbnailAsync()
        {
            string url = !string.IsNullOrEmpty(assetData.thumbnailMiddleUrl)
                ? assetData.thumbnailMiddleUrl
                : assetData.thumbnailSmallUrl;

            if (string.IsNullOrEmpty(url)) return;

            try
            {
                Texture2D texture = await apiClient.DownloadThumbnailAsync(url);
                thumbnailImage.image = texture;
            }
            catch
            {
                // Thumbnail loading error should not crash the entire browser window
            }
        }

        public void CancelOperations()
        {
            downloadCts?.Cancel();
        }
    }
}
