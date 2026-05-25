using HidrometroApp.Core.Models;

namespace HidrometroApp.Core.Interfaces;

public interface ITokenGenerator
{
    (string token, DateTime expira) Gerar(Usuario usuario);
}
