using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Domain.Conversations.Identity;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Messaging.Infrastructure.Persistence.Configurations;

public sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("participants");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(new StronglyTypedIdConverter<ParticipantId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.ConversationId)
            .HasConversion(new StronglyTypedIdConverter<ConversationId>())
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(p => p.JoinedAt)
            .HasColumnName("joined_at")
            .IsRequired();

        builder.Property(p => p.LastReadAt)
            .HasColumnName("last_read_at");

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasIndex(p => p.ConversationId);
        builder.HasIndex(p => new { p.ConversationId, p.UserId }).IsUnique();
    }
}
