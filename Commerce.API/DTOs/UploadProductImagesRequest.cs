using System.ComponentModel.DataAnnotations;
namespace Commerce.API.DTOs;

public class UploadProductImagesRequest
{
    [Required]
    public IFormFileCollection Files { get; set; } = null!;
}