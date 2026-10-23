using AgentFlow.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentFlow.Api.Data;

public class AgentFlowDbContext(DbContextOptions<AgentFlowDbContext> options) : DbContext(options)
{
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<AgentStep> Steps => Set<AgentStep>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Run>(e =>
        {
            e.Property(r => r.Question).HasMaxLength(500);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Error).HasMaxLength(1000);
            e.HasIndex(r => r.CreatedAt);
            e.HasMany(r => r.Steps).WithOne().HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AgentStep>(e =>
        {
            e.Property(s => s.Agent).HasMaxLength(40);
            e.Property(s => s.ToAgent).HasMaxLength(40);
            e.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Summary).HasMaxLength(500);
            e.HasIndex(s => new { s.RunId, s.Sequence }).IsUnique();
        });
    }
}
