namespace WareHouseFileArchiver.Models.Domains
{
    public class FileDownloadLog
    {
        public Guid Id { get; set; }
        public Guid ArchiveFileId { get; set; }
        public string DownloadedBy { get; set; } = string.Empty;
        public DateTime DownloadedAt { get; set; }

        public ArchiveFile? ArchiveFile { get; set; }
    }
}