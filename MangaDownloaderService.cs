using dotenv.net;
using downloader_service.Helpers;
using downloader_service.Models;
using downloader_service.Repo;
using downloader_service.Services;
using localscrape.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading;

namespace downloader_service
{
    public partial class MangaDownloaderService: ServiceBase
    {
        public RabbitRepo _downloadRabbit;

        public MangaDownloaderService() : base()
        {
            InitializeComponent();
            InitialiseServices();
            ExecuteService();
        }

        private void InitialiseServices()
        {
            var factory = LoggerFactory.Create(
                e => e.AddEventLog(
                    s => { s.SourceName = "MangaDownloadService";
                        s.LogName = "Application";
                    }));
            var logger = factory.CreateLogger("MangaDownloadService");
            var env = new DotEnvOptions (envFilePaths: new List<string> { @"C:\\Users\\noti_\\source\\repos\\downloader_service\\bin\\Debug\\.env" } );
            var envHelper = new DotEnvHelper(env, logger);
            var sqlRepo = new MangasDownloadRepo(envHelper, logger);
            var httpConfig = new HttpClientOptions { BaseAddress = new Uri("http://127.0.0.1") };
            var httpHelper = new HttpClassHelper(logger, httpConfig);
            var fileHandler = new FileHandler(logger);
            var downloadService = new DownloaderService(sqlRepo, httpHelper, fileHandler, envHelper, logger);
            _downloadRabbit = new RabbitRepo(envHelper, dlObj => _ = downloadService.DownloadManga(dlObj), logger);
        }

        protected override void OnStart(string[] args)
        {
        }

        private async void ExecuteService()
        {
            Thread.Sleep(1000);
            await _downloadRabbit.StartConsuming();
        }

        protected override void OnStop()
        {
        }
    }
}
