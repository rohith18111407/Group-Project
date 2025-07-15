using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Models.DTOs
{
    public class ArchiveFileResponseDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public CategoryType Category { get; set; }
    public string FileExtension { get; set; }
    public long FileSizeInBytes { get; set; }
    public string FilePath { get; set; }
    public int VersionNumber { get; set; }
    public string? Description { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

}