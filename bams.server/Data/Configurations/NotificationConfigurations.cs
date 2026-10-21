using bams.server.Constants;
using bams.server.Models.Notifications;
using bams.server.Models.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Type).IsRequired().HasMaxLength(60);
        builder.Property(notification => notification.Category).IsRequired().HasMaxLength(60);
        builder.Property(notification => notification.Title).IsRequired().HasMaxLength(NotificationConstants.MaximumTitleLength);
        builder.Property(notification => notification.Message).IsRequired().HasMaxLength(NotificationConstants.MaximumMessageLength);
        builder.Property(notification => notification.TargetType).IsRequired().HasMaxLength(60);
        builder.Property(notification => notification.GroupKey).HasMaxLength(100);

        // The bell reads unread rows per recipient; group dismissal looks up unread rows by key.
        builder.HasIndex(notification => new { notification.RecipientUserId, notification.ReadAtUtc, notification.CreatedAtUtc })
            .HasDatabaseName("IX_Notification_RecipientUnread");
        builder.HasIndex(notification => new { notification.GroupKey, notification.ReadAtUtc })
            .HasDatabaseName("IX_Notification_GroupUnread");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(notification => notification.RecipientUserId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Notification_Recipient");
        builder.HasOne<User>().WithMany()
            .HasForeignKey(notification => notification.ActorUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Notification_Actor");
    }
}
