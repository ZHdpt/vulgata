using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExactCrossRepositoryFlowPass2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessLogicDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "LogicalDocumentStableKey",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlanFingerprint",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CrossRepositoryResolutionDecisions",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pass1PairExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoordinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerEntryIdentity = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ConsumerEntryStableKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ConsumerDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Protocol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NormalizedEndpointIdentifier = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentEdgeId = table.Column<Guid>(type: "uuid", nullable: true),
                    UncertaintyId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRepositoryResolutionDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrossRepositoryResolutionDecisions_DocumentEdges_DocumentEd~",
                        column: x => x.DocumentEdgeId,
                        principalSchema: "vulgata",
                        principalTable: "DocumentEdges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CrossRepositoryResolutionDecisions_Uncertainties_Uncertaint~",
                        column: x => x.UncertaintyId,
                        principalSchema: "vulgata",
                        principalTable: "Uncertainties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass2Executions",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pass1PairExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoordinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StateVersion = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass2Executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pass2Executions_Pass1PairExecutions_Pass1PairExecutionId",
                        column: x => x.Pass1PairExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass1PairExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass2Executions_Systems_SystemId",
                        column: x => x.SystemId,
                        principalSchema: "vulgata",
                        principalTable: "Systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass2Executions_TwoRepositoryScanCoordinations_Coordination~",
                        column: x => x.CoordinationId,
                        principalSchema: "vulgata",
                        principalTable: "TwoRepositoryScanCoordinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExactResolutionProviderEvidenceContributions",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolutionDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ProviderOrdinal = table.Column<int>(type: "integer", nullable: false),
                    EvidenceOrdinal = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExactResolutionProviderEvidenceContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExactResolutionProviderEvidenceContributions_CrossRepositor~",
                        column: x => x.ResolutionDecisionId,
                        principalSchema: "vulgata",
                        principalTable: "CrossRepositoryResolutionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExactResolutionProviderEvidenceContributions_SourceAnchors_~",
                        column: x => x.SourceAnchorId,
                        principalSchema: "vulgata",
                        principalTable: "SourceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass2JobExecutions",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pass2ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupOrder = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    StateVersion = table.Column<long>(type: "bigint", nullable: false),
                    CommittedDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletionFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass2JobExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pass2JobExecutions_DocumentVersions_CommittedDocumentVersio~",
                        column: x => x.CommittedDocumentVersionId,
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass2JobExecutions_FlowGroups_FlowGroupId",
                        column: x => x.FlowGroupId,
                        principalSchema: "vulgata",
                        principalTable: "FlowGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass2JobExecutions_Pass2Executions_Pass2ExecutionId",
                        column: x => x.Pass2ExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass2Executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass2Omissions",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pass2ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExactResolutionDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass2Omissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pass2Omissions_CrossRepositoryResolutionDecisions_ExactReso~",
                        column: x => x.ExactResolutionDecisionId,
                        principalSchema: "vulgata",
                        principalTable: "CrossRepositoryResolutionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass2Omissions_Pass2Executions_Pass2ExecutionId",
                        column: x => x.Pass2ExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass2Executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass2RetrievalBudgets",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pass2JobExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CallsReserved = table.Column<int>(type: "integer", nullable: false),
                    ResultsReturned = table.Column<int>(type: "integer", nullable: false),
                    AdditionalDocumentCount = table.Column<int>(type: "integer", nullable: false),
                    ReturnedUtf8Bytes = table.Column<int>(type: "integer", nullable: false),
                    StateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass2RetrievalBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pass2RetrievalBudgets_Pass2JobExecutions_Pass2JobExecutionId",
                        column: x => x.Pass2JobExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass2JobExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass2RetrievalReceipts",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pass2JobExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptOrdinal = table.Column<int>(type: "integer", nullable: false),
                    CallOrdinal = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass2RetrievalReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pass2RetrievalReceipts_Pass2JobExecutions_Pass2JobExecution~",
                        column: x => x.Pass2JobExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass2JobExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pass2RetrievalResults",
                schema: "vulgata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pass2JobExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptOrdinal = table.Column<int>(type: "integer", nullable: false),
                    ResultOrdinal = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass2RetrievalResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pass2RetrievalResults_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalSchema: "vulgata",
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass2RetrievalResults_Pass2JobExecutions_Pass2JobExecutionId",
                        column: x => x.Pass2JobExecutionId,
                        principalSchema: "vulgata",
                        principalTable: "Pass2JobExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pass2RetrievalResults_Pass2RetrievalReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "vulgata",
                        principalTable: "Pass2RetrievalReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrossRepositoryResolutionDecisions_DocumentEdgeId",
                schema: "vulgata",
                table: "CrossRepositoryResolutionDecisions",
                column: "DocumentEdgeId");

            migrationBuilder.CreateIndex(
                name: "IX_CrossRepositoryResolutionDecisions_EvidenceFingerprint",
                schema: "vulgata",
                table: "CrossRepositoryResolutionDecisions",
                column: "EvidenceFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrossRepositoryResolutionDecisions_UncertaintyId",
                schema: "vulgata",
                table: "CrossRepositoryResolutionDecisions",
                column: "UncertaintyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExactResolutionProviderEvidenceContributions_ResolutionDec~1",
                schema: "vulgata",
                table: "ExactResolutionProviderEvidenceContributions",
                columns: new[] { "ResolutionDecisionId", "ProviderOrdinal", "EvidenceOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExactResolutionProviderEvidenceContributions_ResolutionDeci~",
                schema: "vulgata",
                table: "ExactResolutionProviderEvidenceContributions",
                columns: new[] { "ResolutionDecisionId", "EvidenceFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExactResolutionProviderEvidenceContributions_SourceAnchorId",
                schema: "vulgata",
                table: "ExactResolutionProviderEvidenceContributions",
                column: "SourceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass2Executions_CoordinationId",
                schema: "vulgata",
                table: "Pass2Executions",
                column: "CoordinationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass2Executions_Pass1PairExecutionId",
                schema: "vulgata",
                table: "Pass2Executions",
                column: "Pass1PairExecutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass2Executions_SystemId",
                schema: "vulgata",
                table: "Pass2Executions",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass2JobExecutions_CommittedDocumentVersionId",
                schema: "vulgata",
                table: "Pass2JobExecutions",
                column: "CommittedDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass2JobExecutions_FlowGroupId",
                schema: "vulgata",
                table: "Pass2JobExecutions",
                column: "FlowGroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass2JobExecutions_Pass2ExecutionId_GroupOrder",
                schema: "vulgata",
                table: "Pass2JobExecutions",
                columns: new[] { "Pass2ExecutionId", "GroupOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass2Omissions_ExactResolutionDecisionId",
                schema: "vulgata",
                table: "Pass2Omissions",
                column: "ExactResolutionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass2Omissions_Pass2ExecutionId_ExactResolutionDecisionId",
                schema: "vulgata",
                table: "Pass2Omissions",
                columns: new[] { "Pass2ExecutionId", "ExactResolutionDecisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass2RetrievalBudgets_Pass2JobExecutionId",
                schema: "vulgata",
                table: "Pass2RetrievalBudgets",
                column: "Pass2JobExecutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass2RetrievalReceipts_Pass2JobExecutionId_CallOrdinal",
                schema: "vulgata",
                table: "Pass2RetrievalReceipts",
                columns: new[] { "Pass2JobExecutionId", "CallOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pass2RetrievalResults_DocumentVersionId",
                schema: "vulgata",
                table: "Pass2RetrievalResults",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass2RetrievalResults_Pass2JobExecutionId",
                schema: "vulgata",
                table: "Pass2RetrievalResults",
                column: "Pass2JobExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Pass2RetrievalResults_ReceiptId_ResultOrdinal",
                schema: "vulgata",
                table: "Pass2RetrievalResults",
                columns: new[] { "ReceiptId", "ResultOrdinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExactResolutionProviderEvidenceContributions",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass2Omissions",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass2RetrievalBudgets",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass2RetrievalResults",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "CrossRepositoryResolutionDecisions",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass2RetrievalReceipts",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass2JobExecutions",
                schema: "vulgata");

            migrationBuilder.DropTable(
                name: "Pass2Executions",
                schema: "vulgata");

            migrationBuilder.DropColumn(
                name: "LogicalDocumentStableKey",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "PlanFingerprint",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessLogicDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
