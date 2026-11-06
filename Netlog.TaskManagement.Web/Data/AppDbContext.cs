
using Microsoft.EntityFrameworkCore;
using Netlog.TaskManagement.Web.Models;

namespace Netlog.TaskManagement.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Developer> Developers => Set<Developer>();
        
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
}
