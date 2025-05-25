using downloader_service.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace downloader_service.Helpers
{
    public interface IHttpClassHelper
    {
        Task DownloadFileAsync(string url, string fullPath);
        string GetTableFromDomain(string domain);
        Task<HttpResponseMessage> SendPostRequestAsync(MangaUpdateRequest payload);
    }
    public class HttpClassHelper : IHttpClassHelper, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IFileHandler _fileHandler;
        private bool _disposed;

        public HttpClassHelper(ILogger logger, HttpClientOptions? options)
        {
            _httpClient = CreateHttpClient(options);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileHandler = new FileHandler(logger);
        }

        public HttpClient CreateHttpClient(HttpClientOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var client = new HttpClient();
            client.Timeout = options.Timeout;

            if (options.BaseAddress != null)
            {
                client.BaseAddress = options.BaseAddress;
            }

            foreach (var header in options.DefaultRequestHeaders)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            return client;
        }

        public async Task DownloadFileAsync(string url, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL must be provided", nameof(url));
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentException("Invalid path", nameof(fullPath));
            var fileName = Path.GetFileName(fullPath);
            var safeFileName = _fileHandler.SanitizeFileName(fileName);
            _logger.LogInformation($"Sanitized file name from '{fullPath}' to '{safeFileName}'");

            try
            {
                _logger.LogInformation($"Starting download from {url}");
                var folderDestination = _fileHandler.GetParentFolder(fullPath);
                if (!_fileHandler.FolderExists(folderDestination))
                    Directory.CreateDirectory(folderDestination);
                _logger.LogInformation($"Ensured destination folder exists: {folderDestination}");

                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                _logger.LogInformation($"Received HTTP {response.StatusCode} for {url}");


                using var responseStream = await response.Content.ReadAsStreamAsync();
                var safeFullPath = Path.Combine(folderDestination, safeFileName);
                using var fileStream = new FileStream(safeFullPath, FileMode.Create, FileAccess.Write, FileShare.None);

                _logger.LogInformation($"Writing content to {safeFullPath}");
                await responseStream.CopyToAsync(fileStream);

                _logger.LogInformation($"Download completed and saved to {safeFullPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading file from {url} to {fullPath}");
                throw;
            }
        }

        public string GetTableFromDomain(string domain)
        {
            if (domain.Contains("asura", StringComparison.OrdinalIgnoreCase))
                return "AsuraScans";
            if (domain.Contains("flamecomics", StringComparison.OrdinalIgnoreCase))
                return "FlameScans";
            return "WeebCentral";
        }

        public async Task<HttpResponseMessage> SendPostRequestAsync(MangaUpdateRequest payload)
        {
            if (payload is null)
                throw new InvalidOperationException("Payload has not been created. Call CreatePayload first.");

            _logger.LogInformation($"Sending request to {_httpClient.BaseAddress}");
            try
            {
                using StringContent jsonContent = new(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json");

                var response = await _httpClient.PostAsync(_httpClient.BaseAddress, jsonContent);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Request succeeded with status {response.StatusCode}");
                }
                else
                {
                    _logger.LogWarning($"Request failed with status {response.StatusCode}");
                }
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending request to {_httpClient.BaseAddress}");
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
