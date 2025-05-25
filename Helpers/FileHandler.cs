using System.IO;
using System;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;

namespace downloader_service.Helpers
{
    public interface IFileHandler
    {
        void Move(string sourcePath, string destinationPath);
        void Rename(string filePath, string newName);
        void Save(string filePath, string content);
        void Copy(string sourcePath, string destinationPath, bool overwrite = false);
        void Delete(string filePath);
        void DeleteFolder(string folderPath);
        void SaveFileFromBase64(string filePath, string base64Content);
        bool FolderExists(string folderPath);
        bool HasFilesAboveSize(string folderPath, long minFileSizeBytes = 10 * 1024);
        string GetParentFolder(string filePath);
        string SanitizeFileName(string fileName);
        void CreateFolder(string folderPath);
    }

    public class FileHandler : IFileHandler
    {
        private readonly ILogger _logger;

        public FileHandler(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Move(string sourcePath, string destinationPath)
        {
            _logger.LogInformation($"Moving file from {sourcePath} to {destinationPath}");
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path must be provided", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("Destination path must be provided", nameof(destinationPath));

            File.Move(sourcePath, destinationPath);
            _logger.LogInformation($"File moved successfully from {sourcePath} to {destinationPath}");
        }

        public void Rename(string filePath, string newName)
        {
            _logger.LogInformation($"Renaming file {filePath} to {newName}");
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path must be provided", nameof(filePath));
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New name must be provided", nameof(newName));

            var directory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("Unable to determine directory from file path.");
            var newPath = Path.Combine(directory, newName);

            File.Move(filePath, newPath);
            _logger.LogInformation($"File renamed successfully from {filePath} to {newPath}");
        }

        public void Save(string filePath, string base64Content)
        {
            _logger.LogInformation($"Saving Base64 content to file {filePath}");
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path must be provided", nameof(filePath));
            if (string.IsNullOrWhiteSpace(base64Content))
                throw new ArgumentException("Content must be provided", nameof(base64Content));

            try
            {
                var bytes = Convert.FromBase64String(base64Content);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogInformation($"Ensured directory exists: {directory}");
                }

                File.WriteAllBytes(filePath, bytes);
                _logger.LogInformation($"File saved successfully to {filePath}");
            }
            catch (FormatException fex)
            {
                _logger.LogError(fex, $"Invalid Base64 content for file {filePath}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save file from Base64: {filePath}");
                throw;
            }
        }

        public void Copy(string sourcePath, string destinationPath, bool overwrite = false)
        {
            _logger.LogInformation($"Copying file from {sourcePath} to {destinationPath} (overwrite={overwrite})");
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path must be provided", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("Destination path must be provided", nameof(destinationPath));

            File.Copy(sourcePath, destinationPath, overwrite);
            _logger.LogInformation($"File copied successfully from {sourcePath} to {destinationPath}");
        }

        public void Delete(string filePath)
        {
            _logger.LogInformation($"Deleting file {filePath}");
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path must be provided", nameof(filePath));

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation($"File deleted successfully: {filePath}");
            }
            else
            {
                _logger.LogWarning($"Attempted to delete file that does not exist: {filePath}");
            }
        }

        public void DeleteFolder(string folderPath)
        {
            _logger.LogInformation($"Attempting to delete folder {folderPath}");
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path must be provided", nameof(folderPath));
            if (!Directory.Exists(folderPath))
            {
                _logger.LogWarning($"Folder does not exist: {folderPath}");
                return;
            }

            var subdirs = Directory.GetDirectories(folderPath);
            if (subdirs.Length > 0)
            {
                _logger.LogWarning($"Folder {folderPath} not deleted because it contains subfolders");
                return;
            }

            var files = Directory.GetFiles(folderPath);
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogInformation($"Deleted file: {file}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to delete file: {file}");
                }
            }

            try
            {
                Directory.Delete(folderPath);
                _logger.LogInformation($"Deleted folder: {folderPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete folder: {folderPath}");
            }
        }
        public void SaveFileFromBase64(string filePath, string base64Content)
        {
            _logger.LogInformation($"Saving Base64 content to {filePath}");
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path must be provided", nameof(filePath));
            if (string.IsNullOrWhiteSpace(base64Content))
                throw new ArgumentException("Base64 content must be provided", nameof(base64Content));

            try
            {
                var bytes = Convert.FromBase64String(base64Content);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogInformation($"Ensured directory exists: {directory}");
                }

                File.WriteAllBytes(filePath, bytes);
                _logger.LogInformation($"File saved successfully from Base64 to {filePath}");
            }
            catch (FormatException fex)
            {
                _logger.LogError(fex, $"Invalid Base64 content for file {filePath}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save Base64 content to {filePath}");
                throw;
            }
        }
        public bool FolderExists(string folderPath)
        {
            _logger.LogInformation($"Checking if folder exists: {folderPath}");
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path must be provided", nameof(folderPath));

            var exists = Directory.Exists(folderPath);
            _logger.LogInformation($"Folder '{folderPath}' exists: {exists}");
            return exists;
        }

        public bool HasFilesAboveSize(string folderPath, long minFileSizeBytes = 15 * 1024)
        {
            _logger.LogInformation($"Checking for files in '{folderPath}' with size >= {minFileSizeBytes} bytes");
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path must be provided", nameof(folderPath));
            if (!FolderExists(folderPath))
            {
                _logger.LogWarning($"Folder does not exist: {folderPath}");
                return false;
            }

            var files = Directory.GetFiles(folderPath);
            if (files.Length == 0)
            {
                _logger.LogWarning($"No files found in folder: {folderPath}");
                return false;
            }

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                if (info.Length > minFileSizeBytes)
                {
                    _logger.LogInformation($"File '{file}' size {info.Length} is above threshold");
                    return true;
                }
            }

            _logger.LogInformation($"All files in '{folderPath}' are < {minFileSizeBytes} bytes");
            return false;
        }

        public string GetParentFolder(string filePath)
        {
            _logger.LogInformation($"Retrieving parent folder for file: {filePath}");
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path must be provided", nameof(filePath));

            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                _logger.LogWarning($"Unable to determine parent directory for: {filePath}");
                return string.Empty;
            }

            _logger.LogInformation($"Parent folder for file '{filePath}' is '{directory}'");
            return directory;
        }
        public string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(ch => !invalidChars.Contains(ch)).ToArray());
            var normalized = sanitized.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark && ch != '%')
                {
                    sb.Append(ch);
                }
            }

            return HttpUtility.HtmlDecode(sb.ToString().Normalize(NormalizationForm.FormC));
        }

        public void CreateFolder(string folderPath)
        {
            _logger.LogInformation($"Creating folder: {folderPath}");
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path must be provided", nameof(folderPath));

            try
            {
                Directory.CreateDirectory(folderPath);
                _logger.LogInformation($"Folder created or already exists: {folderPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create folder: {folderPath}");
                throw;
            }
        }
    }
}
