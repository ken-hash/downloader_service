using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using MySql.Data.MySqlClient;
using localscrape.Helpers;
using Microsoft.Extensions.Logging;

namespace downloader_service.Repo
{
    public interface IMangasDownloadRepo
    {
        void AddChapterExcluded(string title, string chapter);
        void AppendExtraInformation(string title, string extraInformation, string table);
        List<string> GetChaptersExcluded(string title);
    }

    public class MangasDownloadRepo : IMangasDownloadRepo
    {
        private readonly IEnvHelper _envHelper;
        private readonly string _connectionString;
        private readonly ILogger _logger;

        public MangasDownloadRepo(IEnvHelper envHelper, ILogger logger)
        {
            _envHelper = envHelper ?? throw new ArgumentNullException(nameof(envHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = _envHelper.GetEnvValue("connectionString");
        }

        public void AddChapterExcluded(string title, string chapter)
        {
            _logger.LogInformation($"Checking exclusion for title='{title}', chapter='{chapter}'");

            const string countSql = @"
                SELECT COUNT(1)
                  FROM ExcludeManga
                 WHERE MangaTitle = @Title
                   AND Chapter    = @Chapter;";

            const string insertSql = @"
                INSERT INTO ExcludeManga (MangaTitle, Chapter)
                     VALUES (@Title, @Chapter);";

            using (IDbConnection db = new MySqlConnection(_connectionString))
            {
                db.Open();
                _logger.LogInformation($"Database connection opened for title='{title}', chapter='{chapter}'");

                var parameters = new { Title = title, Chapter = chapter };
                var existing = db.ExecuteScalar<int>(countSql, parameters);

                if (existing > 0)
                {
                    _logger.LogInformation($"Entry already exists for title='{title}', chapter='{chapter}', skipping insert.");
                    return;
                }

                db.Execute(insertSql, parameters);
                _logger.LogInformation($"Inserted exclusion record for title='{title}', chapter='{chapter}'");
            }
        }

        public void AppendExtraInformation(string title, string extraInformation, string table)
        {
            _logger.LogInformation($"Appending extra information '{extraInformation}' to title='{title}' in table='{table}'");

            var cleanSql = $@"
                UPDATE {table}
                   SET ExtraInformation = REPLACE(ExtraInformation, ',,', ',')
                 WHERE Title = @Title;";

            var updateSql = $@"
                UPDATE {table}
                   SET LastUpdated      = @Now,
                       ExtraInformation = CONCAT(ExtraInformation, @ExtraWithComma)
                 WHERE Title = @Title;";

            using (IDbConnection db = new MySqlConnection(_connectionString))
            {
                db.Open();
                _logger.LogInformation($"Database connection opened for appending to title='{title}'");

                db.Execute(cleanSql, new { Title = title });
                _logger.LogInformation($"Cleaned extra information placeholders for title='{title}'");

                db.Execute(updateSql, new
                {
                    Title = title,
                    Now = DateTime.Now,
                    ExtraWithComma = extraInformation + ","
                });
                _logger.LogInformation($"Appended extra information '{extraInformation}' to title='{title}'");
            }
        }

        public List<string> GetChaptersExcluded(string title)
        {
            _logger.LogInformation($"Retrieving excluded chapters for title='{title}'");

            const string sql = @"
                SELECT Chapter
                  FROM ExcludeManga
                 WHERE MangaTitle = @Title;";

            using (IDbConnection db = new MySqlConnection(_connectionString))
            {
                db.Open();
                _logger.LogInformation($"Database connection opened for retrieval of title='{title}'");

                var result = db.Query<string>(sql, new { Title = title }).ToList();
                _logger.LogInformation($"Retrieved {result.Count} excluded chapters for title='{title}'");
                return result;
            }
        }
    }
}