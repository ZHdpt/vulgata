using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data.Configurations;

public sealed class DatabaseConnectionConfiguration : IEntityTypeConfiguration<DatabaseConnection>
{
    public void Configure(EntityTypeBuilder<DatabaseConnection> builder)
    {
        builder.ToTable("DatabaseConnections");

        builder.HasKey(connection => connection.Id);

        builder.Property(connection => connection.Id)
            .ValueGeneratedNever();

        builder.Property(connection => connection.RepositoryId)
            .IsRequired();

        builder.Property(connection => connection.EncryptedConnectionString)
            .IsRequired()
            .HasMaxLength(8000);

        builder.Property(connection => connection.DatabaseType)
            .HasConversion<int>();

        builder.Property(connection => connection.EncryptedUsername)
            .HasMaxLength(4000);

        builder.Property(connection => connection.EncryptedPassword)
            .HasMaxLength(4000);

        builder.Property(connection => connection.CreatedAt)
            .IsRequired();

        builder.Property(connection => connection.UpdatedAt)
            .IsRequired();

        builder.HasIndex(connection => connection.RepositoryId)
            .IsUnique();

        builder.HasOne(connection => connection.Repository)
            .WithOne(repository => repository.DatabaseConnection)
            .HasForeignKey<DatabaseConnection>(connection => connection.RepositoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
