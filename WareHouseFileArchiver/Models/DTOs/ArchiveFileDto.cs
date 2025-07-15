namespace WareHouseFileArchiver.Models.DTOs
{
    public class ArchiveFileDto
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string FileExtension { get; set; }
        public long FileSizeInBytes { get; set; }
        public int Version { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedBy { get; set; }
    }
}