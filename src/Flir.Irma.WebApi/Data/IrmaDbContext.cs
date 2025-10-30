using Flir.Irma.WebApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace Flir.Irma.WebApi.Data;

public class IrmaDbContext(DbContextOptions<IrmaDbContext> options) : DbContext(options)
{
    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConversationEntity>(builder =>
        {
            builder.ToTable("Conversations");

            builder.HasKey(c => c.ConversationId);

            builder.Property(c => c.CreatedDateTimeUtc)
                .IsRequired();

            builder.Property(c => c.DisplayName)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(c => c.Product)
                .IsRequired(false)
                .HasMaxLength(128);

            builder.Property(c => c.State)
                .IsRequired();

            builder.Property(c => c.TurnCount)
                .IsRequired();

            builder.Property(c => c.LastModifiedUtc)
                .IsRequired();

            builder.HasMany(c => c.Messages)
                .WithOne(m => m.Conversation!)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(c => c.CreatedDateTimeUtc);
        });

        modelBuilder.Entity<MessageEntity>(builder =>
        {
            builder.ToTable("Messages");

            builder.HasKey(m => m.MessageId);

            builder.Property(m => m.Role)
                .IsRequired();

            builder.Property(m => m.Text)
                .IsRequired()
                .HasMaxLength(4000);

            builder.Property(m => m.CreatedDateTimeUtc)
                .IsRequired();

            builder.HasIndex(m => new { m.ConversationId, m.CreatedDateTimeUtc });
        });
    }
}
