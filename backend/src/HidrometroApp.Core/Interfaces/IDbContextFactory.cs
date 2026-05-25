using HidrometroApp.Infrastructure.Data;

namespace HidrometroApp.Core.Interfaces;

public interface IDbContextFactory
{
    HidrometroDbContext CreateDbContext();
}
