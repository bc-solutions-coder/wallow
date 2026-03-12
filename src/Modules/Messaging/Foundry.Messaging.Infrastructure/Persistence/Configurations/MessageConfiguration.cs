using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Domain.Conversations.Identity;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Messaging.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new StronglyTypedIdConverter<MessageId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.ConversationId)
            .HasConversion(new StronglyTypedIdConverter<ConversationId>())
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(e => e.SenderId)
            .HasColumnName("sender_id")
            .IsRequired();

        builder.Property(e => e.Body)
            .HasColumnName("body")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.SentAt)
            .HasColumnName("sent_at")
            .IsRequired();

        builder.HasIndex(e => e.ConversationId);
        builder.HasIndex(e => e.SenderId);
    }
}
