
using downloader_service.Helpers;
using downloader_service.Models;
using downloader_service.Repo;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;
using localscrape.Helpers;
using System.Linq;

namespace downloader_service.Services
{
    public class DownloaderService
    {
        private readonly IMangasDownloadRepo _sql;
        private readonly IHttpClassHelper _httpHelper;
        private readonly IFileHandler _fileHandler;
        private readonly IEnvHelper _envHelper;
        private readonly ILogger _logger;
        private readonly string _updateApiEndpoint;

        public DownloaderService(
            IMangasDownloadRepo sql,
            IHttpClassHelper httpHelper,
            IFileHandler fileHandler,
            IEnvHelper envHelper,
            ILogger logger)
        {
            _sql = sql ?? throw new ArgumentNullException(nameof(sql));
            _httpHelper = httpHelper ?? throw new ArgumentNullException(nameof(httpHelper));
            _fileHandler = fileHandler ?? throw new ArgumentNullException(nameof(fileHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _envHelper = envHelper;

            _updateApiEndpoint = _envHelper.GetEnvValue("updateEndpoint");
        }

        public async Task DownloadManga(DownloadObject downloadObj)
        {
            if (downloadObj.MangaImages.Count < 1)
            {
                _logger.LogError($"Nothing to download");
                return;
            }
            var mangaTitleChapter = $"{downloadObj.Title} : {downloadObj.ChapterNum}";
            var destinationFolder = _fileHandler.GetParentFolder(downloadObj.MangaImages.First().FullPath);
            if (_fileHandler.FolderExists(destinationFolder))
            {
                _logger.LogInformation($"Folder is not empty. Redownloading files");
            }
            else
            {
                _fileHandler.CreateFolder(destinationFolder);
            }
                var chaptersExcluded = _sql.GetChaptersExcluded(downloadObj.Title);
            if (chaptersExcluded.Count > 0 && chaptersExcluded.Contains(downloadObj.ChapterNum))
            {
                _logger.LogInformation($"Skipping excluded chapter {mangaTitleChapter}");
                return;
            }
            if (downloadObj.MangaImages.Any(e => !string.IsNullOrEmpty(e.Base64String)))
            {
                _logger.LogInformation($"Starting saving files for {mangaTitleChapter}");
                SaveFiles(downloadObj.MangaImages);
            }
            else
            {
                foreach(var image in downloadObj.MangaImages)
                {
                    await _httpHelper.DownloadFileAsync(image.Uri, image.FullPath);
                }
            }
            var tableName = _httpHelper.GetTableFromDomain(downloadObj.MangaImages.First().Uri);
            if (_fileHandler.HasFilesAboveSize(destinationFolder))
            {
                _logger.LogInformation($"Successfully downloaded {mangaTitleChapter}");
                _sql.AppendExtraInformation(downloadObj.Title, downloadObj.ChapterNum, tableName);
                var payload = new MangaUpdateRequest
                {
                    MangaChapter = downloadObj.ChapterNum,
                    Name = downloadObj.Title
                };
                payload.Path = GetFileNames(downloadObj.MangaImages);
                var httpOpt = new HttpClientOptions();
                httpOpt.BaseAddress = new Uri(_updateApiEndpoint);
                using (var httpHelper = new HttpClassHelper(_logger, httpOpt))
                {
                    await httpHelper.SendPostRequestAsync(payload);
                }
            }
            else
            {
                _logger.LogWarning($"Files downloaded is invalid {mangaTitleChapter}");
            }
        }

        private string GetFileNames(List<MangaImages> mangaImages)
        {
            return string.Join(",", mangaImages.Select(e => _fileHandler.SanitizeFileName(e.ImageFileName)).ToArray());
        }

        private void SaveFiles(List<MangaImages> mangaImages)
        {
            foreach(var image in mangaImages)
            {
                _fileHandler.Save(image.FullPath, image.Base64String);
                _logger.LogInformation($"Image saved {image.FullPath}");
            }
        }
    }
}
