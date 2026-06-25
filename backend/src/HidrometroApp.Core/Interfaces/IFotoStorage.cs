// backend/src/HidrometroApp.Core/Interfaces/IFotoStorage.cs
namespace HidrometroApp.Core.Interfaces;

/// <summary>
/// Abstração de armazenamento de fotos de hidrômetro.
/// Implementações: LocalFotoStorage (filesystem, default) e GcsFotoStorage (Google Cloud Storage).
/// </summary>
public interface IFotoStorage
{
    /// <summary>
    /// Persiste os bytes sob <paramref name="objectName"/> (formato "yyyyMM/condo_{id}/{guid}.jpg")
    /// e retorna a referência usada para recuperar depois: path absoluto no local, "gs://bucket/object" no GCS.
    /// </summary>
    Task<string> SalvarAsync(byte[] bytes, string objectName, string contentType = "image/jpeg", CancellationToken ct = default);

    /// <summary>Recupera os bytes a partir da referência retornada por <see cref="SalvarAsync"/>. Null se não existir.</summary>
    Task<byte[]?> ObterAsync(string referencia, CancellationToken ct = default);
}
