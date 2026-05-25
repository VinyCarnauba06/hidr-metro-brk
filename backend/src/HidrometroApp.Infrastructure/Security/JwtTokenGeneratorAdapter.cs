using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using Microsoft.Extensions.Configuration;

namespace HidrometroApp.Infrastructure.Security;

public class JwtTokenGeneratorAdapter : ITokenGenerator
{
    private readonly JwtTokenGenerator _inner;

    public JwtTokenGeneratorAdapter(IConfiguration config)
    {
        _inner = new JwtTokenGenerator(config);
    }

    public (string token, DateTime expira) Gerar(Usuario usuario) => _inner.Gerar(usuario);
}
