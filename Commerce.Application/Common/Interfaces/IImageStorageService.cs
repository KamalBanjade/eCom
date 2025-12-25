namespace Commerce.Application.Common.Interfaces;

/// <summary>
/// Interface for cloud image storage operations
/// </summary>
public interface IImageStorageService
{
    /// <summary>
    /// Uploads an image to cloud storage
    /// </summary>
    /// <param name="imageStream">Image file stream</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="folder">Folder path in cloud storage (e.g., "products/123")</param>
    /// <returns>Public URL of uploaded image</returns>
    Task<string> UploadImageAsync(Stream imageStream, string fileName, string folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an image from cloud storage
    /// </summary>
    /// <param name="imageUrl">Full URL or public ID of the image</param>
    Task<bool> DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the public ID from a Cloudinary URL
    /// </summary>
    string GetPublicIdFromUrl(string imageUrl);
}
