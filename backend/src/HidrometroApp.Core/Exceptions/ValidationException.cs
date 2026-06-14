namespace HidrometroApp.Core.Exceptions;

public class HidrometroValidationException : Exception
{
    public HidrometroValidationException(string message) : base(message) { }
}

public class LeituraInvalidaException : Exception
{
    public LeituraInvalidaException(string message) : base(message) { }
}

public class FotoRejeitadaException : Exception
{
    public FotoRejeitadaException(string message) : base(message) { }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

// Lançada quando o OCR não encontra nenhum padrão de visor numérico válido na imagem,
// evitando que números de série alfanuméricos sejam persistidos como leitura.
public class OcrSemLeituraValidaException : Exception
{
    public OcrSemLeituraValidaException(string message) : base(message) { }
}
