using System.ComponentModel.DataAnnotations;
namespace Commerce.API.DTOs;

public class UploadVariantImageRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
}