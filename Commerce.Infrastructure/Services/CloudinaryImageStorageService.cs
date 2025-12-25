using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Commerce.Application.Common.Interfaces;
using Commerce.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Services;

/// <summary>
/// Cloudinary implementation of image storage service
/// </summary>
public class CloudinaryImageStorageService : IImageStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryImageStorageService(IOptions<CloudinarySettings> settings)
    {
        var account = new Account(
            settings.Value.CloudName,
            settings.Value.ApiKey,
            settings.Value.ApiSecret
        );
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(Stream imageStream, string fileName, string folder, CancellationToken cancellationToken = default)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, imageStream),
            Folder = folder,
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);

        if (uploadResult.Error != null)
        {
            throw new InvalidOperationException($"Image upload failed: {uploadResult.Error.Message}");
        }

        return uploadResult.SecureUrl.ToString();
    }

    public async Task<bool> DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var publicId = GetPublicIdFromUrl(imageUrl);
            
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            return result.Result == "ok";
        }
        catch
        {
            return false;
        }
    }

    public string GetPublicIdFromUrl(string imageUrl)
    {
        // Extract public ID from Cloudinary URL
        // Example URL: https://res.cloudinary.com/cloud-name/image/upload/v1234567890/folder/filename.jpg
        // Public ID: folder/filename
        
        var uri = new Uri(imageUrl);
        var segments = uri.AbsolutePath.Split('/');
        
        // Find the "upload" segment and get everything after version number
        var uploadIndex = Array.IndexOf(segments, "upload");
        if (uploadIndex >= 0 && uploadIndex + 2 < segments.Length)
        {
            // Skip version (v1234567890)
            var pathSegments = segments.Skip(uploadIndex + 2);
            var publicId = string.Join("/", pathSegments);
            
            // Remove file extension
            var lastDot = publicId.LastIndexOf('.');
            if (lastDot > 0)
            {
                publicId = publicId.Substring(0, lastDot);
            }
            
            return publicId;
        }

        throw new ArgumentException("Invalid Cloudinary URL format", nameof(imageUrl));
    }
}
