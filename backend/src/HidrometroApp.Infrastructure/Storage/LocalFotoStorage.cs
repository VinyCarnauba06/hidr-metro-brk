// backend/src/HidrometroApp.Infrastructure/Storage/LocalFotoStorage.cs
using HidrometroApp.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HidrometroApp.Infrastructure.Storage;

/// <summary>
/// Armazenamento de fotos no filesystem local. Default quando GCS_BUCKET_NAME não está setado.
/// Base configurável via STORAGE_PATH; senão usa ./storage/fotos relativo ao executável.
/// </summary>
public sealed class LocalFotoStorage : IFotoStorage
{
    private readonly IConfiguration _config;
    private readonly ILogger<LocalFotoStorage> _logger;

    public LocalFotoStorage(IConfiguration config, ILogger<LocalFotoStorage> logger)
    {
        _config = config;
        _logger = logger;
    }

    private string BasePath => _config["STORAGE_PATH"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "storage", "fotos");

    public async Task<string> SalvarAsync(byte[] bytes, string objectName, string contentType = "image/jpeg", CancellationToken ct = default)
    {
        var caminho = Path.Combine(BasePath, objectName.Replace('/', Path.DirectorySeparatorChar));
        var pasta = Path.GetDirectoryName(caminho)!;
        Directory.CreateDirectory(pasta);
        await File.WriteAllBytesAsync(caminho, bytes, ct);
        _logger.LogDebug("Foto salva localmente: {Caminho} ({Bytes} bytes)", caminho, bytes.Length);
        return caminho;
    }

    public async Task<byte[]?> ObterAsync(string referencia, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(referencia)) return null;

        // referencia pode ser path absoluto (gravações novas e antigas) ou objectName relativo ao BasePath.
        var caminho = File.Exists(referencia)
            ? referencia
            : Path.Combine(BasePath, referencia.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(caminho) ? await File.ReadAllBytesAsync(caminho, ct) : null;
    }
}
