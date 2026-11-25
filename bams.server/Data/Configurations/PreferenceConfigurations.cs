using bams.server.Constants;
using bams.server.Models.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(PreferenceConstants.SettingNameMaximumLength);
        builder.Property(s => s.UniqueCode).IsRequired().HasMaxLength(PreferenceConstants.SettingCodeMaximumLength);
        builder.Property(s => s.Description).IsRequired().HasMaxLength(PreferenceConstants.SettingDescriptionMaximumLength);
        builder.Property(s => s.ValueType).IsRequired().HasMaxLength(PreferenceConstants.SettingValueTypeMaximumLength);

        builder.HasIndex(s => s.UniqueCode).IsUnique();
    }
}

public sealed class PersonalPreferenceConfiguration : IEntityTypeConfiguration<PersonalPreference>
{
    public void Configure(EntityTypeBuilder<PersonalPreference> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ValueSet).HasMaxLength(PreferenceConstants.PreferenceValueMaximumLength);
        builder.Property(p => p.UpdateKey).IsRequired().HasMaxLength(PreferenceConstants.UpdateKeyMaximumLength).IsConcurrencyToken();

        // One preference row per user and setting; also makes the backfill safe to repeat.
        builder.HasIndex(p => new { p.UserId, p.SettingId }).IsUnique();

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A setting that users have preferences for cannot be deleted by accident.
        builder.HasOne(p => p.Setting)
            .WithMany()
            .HasForeignKey(p => p.SettingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
