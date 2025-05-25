using downloader_service.Repo;
using System.Collections.Generic;

namespace downloader_service.Models
{
    public class DownloadObject
    {
        public string Title { get; set; }
        public string ChapterNum { get; set; }
        public List<MangaImages> MangaImages { get; set; }
    }
}
