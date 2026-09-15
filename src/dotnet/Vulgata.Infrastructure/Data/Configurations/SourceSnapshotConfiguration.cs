using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

internal sealed class SourceSnapshotConfiguration : IEntityTypeConfiguration<SourceSnapshot>
{
    public void Configure(EntityTypeBuilder<SourceSnapshot> builder)
    {
        builder.ToTable("SourceSnapshots", table =>
        {
            table.HasCheckConstraint(
                "CK_SourceSnapshots_ManifestSchemaIdentity",
                "\"ManifestSchemaIdentity\" = 'retained-source-manifest/1.0'");
            table.HasCheckConstraint(
                "CK_SourceSnapshots_ProfiledRevision",
                "((\"GitObjectFormat\" = 'sha1' AND length(\"ProfiledRevision\") = 40) OR " +
                "(\"GitObjectFormat\" = 'sha256' AND length(\"ProfiledRevision\") = 64)) AND " +
                "replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(" +
                "replace(replace(replace(replace(replace(replace(\"ProfiledRevision\", '0', ''), " +
                "'1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), " +
                "'8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '') = ''");
            table.HasCheckConstraint(
                "CK_SourceSnapshots_ManifestSha256",
                "length(\"ManifestSha256\") = 64 AND " +
                "replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(" +
                "replace(replace(replace(replace(replace(replace(\"ManifestSha256\", '0', ''), " +
                "'1', ''), '2', ''), '3', ''), '4', ''), '5', ''), '6', ''), '7', ''), " +
                "'8', ''), '9', ''), 'a', ''), 'b', ''), 'c', ''), 'd', ''), 'e', ''), 'f', '') = ''");
            table.HasCheckConstraint(
                "CK_SourceSnapshots_Counts",
                "\"EntryCount\" >= 0 AND \"TotalBlobBytes\" >= 0");
            table.HasCheckConstraint(
                "CK_SourceSnapshots_InternalStorageKey",
                "length(\"InternalStorageKey\") BETWEEN 1 AND 2048");
        });

        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();
        builder.Property(snapshot => snapshot.ProfiledRevision)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(snapshot => snapshot.GitObjectFormat)
            .HasMaxLength(6)
            .IsRequired();
        builder.Property(snapshot => snapshot.ManifestSchemaIdentity)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(snapshot => snapshot.ManifestSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(snapshot => snapshot.InternalStorageKey)
            .HasMaxLength(2_048)
            .IsRequired();
        builder.Property(snapshot => snapshot.CompletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(snapshot => new { snapshot.RepositoryId, snapshot.ProfiledRevision })
            .IsUnique();
        builder.HasIndex(snapshot => snapshot.InternalStorageKey).IsUnique();

        builder.HasOne(snapshot => snapshot.Repository)
            .WithMany()
            .HasForeignKey(snapshot => snapshot.RepositoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
