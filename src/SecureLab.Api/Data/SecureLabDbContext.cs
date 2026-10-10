using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Data;

/// <summary>
/// EF Core DbContext навчального стенда. <see cref="Incidents"/> — це
/// <c>DbSet&lt;Incident&gt;</c>, а <see cref="OnModelCreating"/> пов'язує entity
/// з таблицею <c>incidents</c> і задає збереження enum Severity та Status як тексту.
/// </summary>
public class SecureLabDbContext(DbContextOptions<SecureLabDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Частковий унікальний індекс: не більше одного не закритого інциденту з тим
    /// самим title. Остання лінія цілісності за паралельних запитів; звичайний
    /// конфлікт API пояснює раніше через перевірку AnyAsync.
    /// </summary>
    public const string ActiveIncidentTitleIndex = "ux_incidents_active_title";

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<User> Users => Set<User>();

    public DbSet<IncidentComment> IncidentComments => Set<IncidentComment>();

    public DbSet<IncidentStatusChange> IncidentStatusChanges => Set<IncidentStatusChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(320).IsRequired();
            entity.Property(u => u.Role).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.ToTable("incidents");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Title).HasMaxLength(300).IsRequired();
            entity.Property(i => i.Description).IsRequired();

            entity.HasIndex(i => i.Title)
                .HasDatabaseName(ActiveIncidentTitleIndex)
                .IsUnique()
                .HasFilter("status <> 'Closed'");

            // Enum -> text у PostgreSQL. Наслідок: сортування в SQL за цим
            // стовпцем лексикографічне, а не за порядком критичності.
            entity.Property(i => i.Severity)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(i => i.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.HasOne(i => i.Owner)
                .WithMany(u => u.OwnedIncidents)
                .HasForeignKey(i => i.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(i => i.Comments)
                .WithOne(c => c.Incident)
                .HasForeignKey(c => c.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(i => i.StatusChanges)
                .WithOne(s => s.Incident)
                .HasForeignKey(s => s.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IncidentComment>(entity =>
        {
            entity.ToTable("incident_comments");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.AuthorDisplayName).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Body).IsRequired();
        });

        modelBuilder.Entity<IncidentStatusChange>(entity =>
        {
            entity.ToTable("incident_status_changes");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.FromStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(s => s.ToStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(s => s.ChangedByDisplayName).HasMaxLength(200).IsRequired();
        });

        // Колонки в PostgreSQL мають snake_case-імена (created_at_utc, owner_user_id),
        // як у канонічному starter: на них розраховані scaffold-и наступних ЛР.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name) =>
        string.Concat(name.Select((ch, index) =>
            index > 0 && char.IsUpper(ch) ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString()));
}
