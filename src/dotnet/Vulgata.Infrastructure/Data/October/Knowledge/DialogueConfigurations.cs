using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberDialogue;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public sealed class DialogueConversationConfiguration : IEntityTypeConfiguration<DialogueConversationRecord>
{
    public void Configure(EntityTypeBuilder<DialogueConversationRecord> b)
    {
        b.ToTable("OctoberDialogueConversations", t => t.HasCheckConstraint("CK_DialogueConversation_Counters", "\"NextTurnSequence\" > 0 AND \"NextFence\" > 0"));
        b.HasKey(x => x.ConversationId);
        b.HasAlternateKey(x => new { x.ConversationId, x.BatchId, x.PublicationId }).HasName("AK_DialogueConversation_Publication");
        b.HasIndex(x => new { x.BatchId, x.PublicationId }).HasDatabaseName("IX_DialogueConversation_Publication");
        b.Property(x => x.OwnerSubjectId).IsRequired();
        b.Property(x => x.IdentityJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<KnowledgePublicationRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.PublicationId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DialogueConversation_Publication");
    }
}

public sealed class DialogueTurnConfiguration : IEntityTypeConfiguration<DialogueTurnRecord>
{
    public void Configure(EntityTypeBuilder<DialogueTurnRecord> b)
    {
        b.ToTable("OctoberDialogueTurns", t => t.HasCheckConstraint("CK_DialogueTurn_State", "\"State\" BETWEEN 0 AND 6 AND \"Sequence\" > 0 AND \"Fence\" > 0"));
        b.HasKey(x => new { x.ConversationId, x.TurnId });
        b.HasIndex(x => new { x.ConversationId, x.ClientRequestKey }).IsUnique();
        b.HasIndex(x => new { x.ConversationId, x.Sequence }).IsUnique();
        b.HasIndex(x => x.ConversationId).IsUnique().HasDatabaseName("UX_DialogueTurn_Active").HasFilter("\"State\" IN (0, 1, 2, 3)");
        b.Property(x => x.RequestDigest).IsRequired().HasMaxLength(64);
        b.Property(x => x.BeginEnvelopeJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<DialogueConversationRecord>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DialogueTurn_Conversation");
    }
}

public sealed class DialogueTerminalConfiguration : IEntityTypeConfiguration<DialogueTerminalRecord>
{
    public void Configure(EntityTypeBuilder<DialogueTerminalRecord> b)
    {
        b.ToTable("OctoberDialogueTerminals", t => t.HasCheckConstraint("CK_DialogueTerminal_State", "\"TerminalState\" BETWEEN 0 AND 2 AND \"Fence\" > 0"));
        b.HasKey(x => new { x.ConversationId, x.TurnId });
        b.HasAlternateKey(x => new { x.ConversationId, x.TurnId, x.BatchId, x.PublicationId }).HasName("AK_DialogueTerminal_Publication");
        b.HasIndex(x => new { x.ConversationId, x.BatchId, x.PublicationId }).HasDatabaseName("IX_DialogueTerminal_ConversationPublication");
        b.HasIndex(x => new { x.ConversationId, x.SaveOperationId }).IsUnique();
        b.Property(x => x.OutcomeDigest).IsRequired().HasMaxLength(64);
        b.Property(x => x.TerminalEnvelopeJson).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.VerifiedConsumedJson).IsRequired().HasColumnType("jsonb");
        b.HasOne<DialogueTurnRecord>().WithOne().HasForeignKey<DialogueTerminalRecord>(x => new { x.ConversationId, x.TurnId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DialogueTerminal_Turn");
        b.HasOne<DialogueConversationRecord>().WithMany().HasForeignKey(x => new { x.ConversationId, x.BatchId, x.PublicationId })
            .HasPrincipalKey(x => new { x.ConversationId, x.BatchId, x.PublicationId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DialogueTerminal_ConversationPublication");
    }
}

public sealed class DialogueReferenceConfiguration : IEntityTypeConfiguration<DialogueReferenceRecord>
{
    public void Configure(EntityTypeBuilder<DialogueReferenceRecord> b)
    {
        b.ToTable("OctoberDialogueReferences");
        b.HasKey(x => new { x.ConversationId, x.TurnId, x.Ordinal });
        b.HasIndex(x => new { x.ConversationId, x.TurnId, x.BatchId, x.PublicationId }).HasDatabaseName("IX_DialogueReference_Terminal");
        b.HasIndex(x => new { x.BatchId, x.PublicationId, x.LogicalDocumentId, x.VersionId }).HasDatabaseName("IX_DialogueReference_PublicationVersion");
        b.HasIndex(x => new { x.BatchId, x.VersionId, x.SectionId }).HasDatabaseName("IX_DialogueReference_Section");
        b.HasOne<DialogueTerminalRecord>().WithMany().HasForeignKey(x => new { x.ConversationId, x.TurnId, x.BatchId, x.PublicationId })
            .HasPrincipalKey(x => new { x.ConversationId, x.TurnId, x.BatchId, x.PublicationId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DialogueReference_Terminal");
        b.HasOne<PublicationMemberRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.PublicationId, x.LogicalDocumentId, x.VersionId })
            .HasPrincipalKey(x => new { x.BatchId, x.PublicationId, x.LogicalDocumentId, x.VersionId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DialogueReference_PublicationVersion");
        b.HasOne<DocumentSectionRecord>().WithMany().HasForeignKey(x => new { x.BatchId, x.VersionId, x.SectionId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DialogueReference_Section");
    }
}
