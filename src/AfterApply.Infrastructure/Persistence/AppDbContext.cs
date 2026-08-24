using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
