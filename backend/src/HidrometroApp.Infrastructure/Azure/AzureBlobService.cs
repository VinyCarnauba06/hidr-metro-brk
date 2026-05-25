using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HidrometroApp.Infrastructure.Azure;

public class AzureBlobService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AzureBlobService> _logger;

    public AzureBlobService(IConfiguration config, ILogger<AzureBlobService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> UploadAsync(string containerName, string blobName, byte[] dados)
    {
        try
        {
            var connStr = _config["AzureWebJobsStorage"]
                ?? $"DefaultEndpointsProtocol=https;AccountName={_config["AZURE_STORAGE_ACCOUNT"]};AccountKey={_config["AZURE_STORAGE_KEY"]};EndpointSuffix=core.windows.net";

            var client = new BlobContainerClient(connStr, containerName);
            await client.CreateIfNotExistsAsync();

            var blobClient = client.GetBlobClient(blobName);
            using var stream = new MemoryStream(dados);
            await blobClient.UploadAsync(stream, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer upload para Azure Blob: {BlobName}", blobName);
            return false;
        }
    }

    public async Task<byte[]?> DownloadAsync(string containerName, string blobName)
    {
        try
        {
            var connStr = $"DefaultEndpointsProtocol=https;AccountName={_config["AZURE_STORAGE_ACCOUNT"]};AccountKey={_config["AZURE_STORAGE_KEY"]};EndpointSuffix=core.windows.net";
            var blobClient = new BlobClient(connStr, containerName, blobName);

            if (!await blobClient.ExistsAsync()) return null;

            var response = await blobClient.DownloadContentAsync();
            return response.Value.Content.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao baixar do Azure Blob: {BlobName}", blobName);
            return null;
        }
    }
}
