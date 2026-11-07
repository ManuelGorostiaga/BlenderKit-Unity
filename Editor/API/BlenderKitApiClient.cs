using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BlenderKitUnity.Editor.API
{
    public sealed class BlenderKitApiClient : IDisposable
    {
        private const string SearchEndpoint = "https://www.blenderkit.com/api/v1/search/";
        private const string AddonVersion = "3.18.1";

        private readonly HttpClient httpClient;

        public BlenderKitApiClient()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60);
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BlenderKitUnity", "1.0.0"));
        }

        public async Task<SearchResponse> SearchMaterialsAsync(
            string keywords,
            string category,
            int page,
            bool freeOnly,
            CancellationToken cancellationToken = default)
        {
            if (page < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than or equal to 1.");
            }

            string query = "asset_type:material";

            if (freeOnly)
            {
                query += " is_free:true";
            }

            if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
            {
                query += $" category_subtree:{category.ToLowerInvariant()}";
            }

            if (!string.IsNullOrWhiteSpace(keywords))
            {
                query += $" {keywords.Trim()}";
            }

            string requestUri = $"{SearchEndpoint}?query={Uri.EscapeDataString(query)}&addon_version={AddonVersion}&page={page}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            string apiKey = BlenderKitSettings.ApiKey;
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            }

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"BlenderKit search failed with status {response.StatusCode}: {errorBody}");
            }

            string json = await response.Content.ReadAsStringAsync();
            SearchResponse searchResponse = JsonUtility.FromJson<SearchResponse>(json);
            if (searchResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize BlenderKit search response.");
            }

            return searchResponse;
        }

        public async Task<string> ResolveDownloadUrlAsync(
            string downloadEndpointUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(downloadEndpointUrl))
            {
                throw new ArgumentException("Download endpoint URL cannot be empty.", nameof(downloadEndpointUrl));
            }

            string sceneUuid = Guid.NewGuid().ToString();
            string separator = downloadEndpointUrl.Contains("?") ? "&" : "?";
            string fullUrl = $"{downloadEndpointUrl}{separator}scene_uuid={sceneUuid}";

            using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
            string apiKey = BlenderKitSettings.ApiKey;
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            }

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to resolve download URL with status {response.StatusCode}: {errorContent}");
            }

            string json = await response.Content.ReadAsStringAsync();
            DownloadResponse downloadResponse = JsonUtility.FromJson<DownloadResponse>(json);
            if (downloadResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize BlenderKit download response payload.");
            }

            return downloadResponse.GetResolvedUrl();
        }

        public async Task DownloadFileAsync(
            string directDownloadUrl,
            string destinationFilePath,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(directDownloadUrl))
            {
                throw new ArgumentException("Direct download URL cannot be null or empty.", nameof(directDownloadUrl));
            }

            if (string.IsNullOrWhiteSpace(destinationFilePath))
            {
                throw new ArgumentException("Destination file path cannot be null or empty.", nameof(destinationFilePath));
            }

            string directory = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using HttpResponseMessage response = await httpClient.GetAsync(
                directDownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Download stream failed with status {response.StatusCode} from {directDownloadUrl}");
            }

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();
            using Stream streamToWriteTo = File.Open(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[81920];
            long totalBytesRead = 0L;
            int bytesRead;

            while ((bytesRead = await streamToReadFrom.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await streamToWriteTo.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalBytesRead += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    progress.Report((float)totalBytesRead / totalBytes);
                }
            }

            if (totalBytes > 0 && totalBytesRead < totalBytes)
            {
                throw new IOException($"Download was interrupted: received {totalBytesRead} of {totalBytes} bytes.");
            }
        }

        public async Task<Texture2D> DownloadThumbnailAsync(
            string thumbnailUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(thumbnailUrl))
            {
                throw new ArgumentException("Thumbnail URL cannot be null or empty.", nameof(thumbnailUrl));
            }

            byte[] imageBytes = await httpClient.GetByteArrayAsync(thumbnailUrl);
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new InvalidOperationException($"Empty thumbnail response received from {thumbnailUrl}");
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(imageBytes))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException($"Failed to decode thumbnail image data from {thumbnailUrl}");
            }

            return texture;
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}
