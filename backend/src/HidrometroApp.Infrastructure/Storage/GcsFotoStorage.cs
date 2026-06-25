// backend/src/HidrometroApp.Infrastructure/Storage/GcsFotoStorage.cs
using Google;
using Google.Cloud.Storage.V1;
using HidrometroApp.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HidrometroApp.Infrastructure.Storage;

/// <summary>
/// Armazenamento de fotos no Google Cloud Storage. Ativado quando GCS_BUCKET_NAME está setado.
/// Credenciais via Application Default Credentials: GOOGLE_APPLICATION_CREDENTIALS apontando para o
/// Service Account JSON, ou Workload Identity quando rodando em GCP.
/// </summary>
public sealed class GcsFotoStorage : IFotoStorage
{
    private readonly StorageClient _client;
    private readonly string _bucket;
    private readonly ILogger<GcsFotoStorage> _logger;

    public GcsFotoStorage(IConfiguration config, ILogger<GcsFotoStorage> logger)
    {
        _bucket = config["GCS_BUCKET_NAME"]
            ?? throw new InvalidOperationException("GCS_BUCKET_NAME não configurado");
        _client = StorageClient.Create();
        _logger = logger;
    }

    public async Task<string> SalvarAsync(byte[] bytes, string objectName, string contentType = "image/jpeg", CancellationToken ct = default)
    {
        using var ms = new MemoryStream(bytes);
        await _client.UploadObjectAsync(_bucket, objectName, contentType, ms, cancellationToken: ct);
        _logger.LogDebug("Foto enviada ao GCS: gs://{Bucket}/{Object} ({Bytes} bytes)", _bucket, objectName, bytes.Length);
        return $"gs://{_bucket}/{objectName}";
    }

    public async Task<byte[]?> ObterAsync(string referencia, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(referencia)) return null;

        var objectName = ExtrairObjectName(referencia);

        try
        {
            using var ms = new MemoryStream();
            await _client.DownloadObjectAsync(_bucket, objectName, ms, cancellationToken: ct);
            return ms.ToArray();
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Objeto não encontrado no GCS: gs://{Bucket}/{Object}", _bucket, objectName);
            return null;
        }
    }

    // Aceita tanto "gs://bucket/yyyyMM/condo_x/uuid.jpg" quanto o objectName puro.
    private static string ExtrairObjectName(string referencia)
    {
        if (!referencia.StartsWith("gs://", StringComparison.Ordinal)) return referencia;

        var semScheme = referencia["gs://".Length..];
        var barra = semScheme.IndexOf('/');
        return barra >= 0 ? semScheme[(barra + 1)..] : semScheme;
    }
}
