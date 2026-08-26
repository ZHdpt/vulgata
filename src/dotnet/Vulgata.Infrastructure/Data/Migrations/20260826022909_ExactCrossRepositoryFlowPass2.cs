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
            static string PortableLowerHex(string column)
            {
                string stripped = string.Concat(Enumerable.Repeat("replace(", 16)) + column;
                foreach (char character in "0123456789abcdef")
                {
                    stripped += $", '{character}', '')";
                }

                return $"length({column}) = 64 AND lower({column}) = {column} AND length({stripped}) = 0";
            }

            bool isPostgreSql = ActiveProvider.Contains("Npgsql", StringComparison.Ordinal);
            string completionFingerprintCheck = isPostgreSql
                ? "\"CompletionFingerprint\" ~ '^[0-9a-f]{64}$'"
                : PortableLowerHex("\"CompletionFingerprint\"");
            string receiptFingerprintCheck = isPostgreSql
                ? "\"ToolCallIdFingerprint\" ~ '^[0-9a-f]{64}$' AND \"RequestFingerprint\" ~ '^[0-9a-f]{64}$' AND \"ResultSetFingerprint\" ~ '^[0-9a-f]{64}$'"
                : $"{PortableLowerHex("\"ToolCallIdFingerprint\"")} AND {PortableLowerHex("\"RequestFingerprint\"")} AND {PortableLowerHex("\"ResultSetFingerprint\"")}";
            string resultFingerprintCheck = isPostgreSql
                ? "\"DescriptorFingerprint\" ~ '^[0-9a-f]{64}$' AND \"EvidenceFingerprint\" ~ '^[0-9a-f]{64}$'"
                : $"{PortableLowerHex("\"DescriptorFingerprint\"")} AND {PortableLowerHex("\"EvidenceFingerprint\"")}";
            migrationBuilder.AddColumn<Guid>(
                name: "ExactResolutionDecisionId",
                schema: "vulgata",
                table: "FlowTraceSteps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessLogicDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ConsumerDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsumerEntryIdentity",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsumerEntryStableKey",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExactResolutionDecisionId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExactResolutionEdgeId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogicalDocumentStableKey",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LogicalKeySchemaIdentity",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MembershipFingerprint",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEndpointIdentifier",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Pass2ExecutionId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: true);

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
                name: "Protocol",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderImplementationDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderRepositoryId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderXmlDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchemaIdentity",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "vulgata",
                table: "FlowGroups",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SystemId",
                schema: "vulgata",
                table: "FlowGroups",
                type: "uuid",
                nullable: true);

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
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CommittedDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletionFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    StateChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass2JobExecutions", x => x.Id);
                    table.CheckConstraint("CK_Pass2JobExecutions_Bounds", "\"GroupOrder\" BETWEEN 1 AND 10 AND \"AttemptCount\" BETWEEN 0 AND 2 AND \"StateVersion\" >= 0");
                    table.CheckConstraint("CK_Pass2JobExecutions_FailureShape", "(\"FailureCategory\" IS NULL) = (\"FailureCode\" IS NULL)");
                    table.CheckConstraint("CK_Pass2JobExecutions_StateShape", "(\"State\" = 'Running' AND \"LeaseToken\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL AND \"NextAttemptAt\" IS NULL) OR (\"State\" = 'RetryScheduled' AND \"LeaseToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NOT NULL) OR (\"State\" IN ('Pending','Failed','Cancelled') AND \"LeaseToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NULL) OR (\"State\" = 'Completed' AND \"LeaseToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"NextAttemptAt\" IS NULL AND \"CommittedDocumentVersionId\" IS NOT NULL AND " + completionFingerprintCheck + ")");
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
                    Pass2JobExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CallsReserved = table.Column<int>(type: "integer", nullable: false),
                    ResultsReturned = table.Column<int>(type: "integer", nullable: false),
                    AdditionalDocumentCount = table.Column<int>(type: "integer", nullable: false),
                    ReturnedUtf8Bytes = table.Column<int>(type: "integer", nullable: false),
                    StateVersion = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass2RetrievalBudgets", x => x.Pass2JobExecutionId);
                    table.CheckConstraint("CK_Pass2RetrievalBudgets_Bounds", "\"CallsReserved\" BETWEEN 0 AND 8 AND \"ResultsReturned\" BETWEEN 0 AND 10 AND \"AdditionalDocumentCount\" BETWEEN 0 AND 4 AND \"ReturnedUtf8Bytes\" BETWEEN 0 AND 65536 AND \"StateVersion\" >= 0");
                    table.CheckConstraint("CK_Pass2RetrievalBudgets_State", "\"State\" IN ('Available','Exhausted') AND ((\"CallsReserved\" = 8 AND \"State\" = 'Exhausted') OR (\"CallsReserved\" < 8 AND \"State\" = 'Available'))");
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
                    ToolCallIdFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultCount = table.Column<int>(type: "integer", nullable: false),
                    ReturnedUtf8Bytes = table.Column<int>(type: "integer", nullable: false),
                    NewAdditionalDocumentCount = table.Column<int>(type: "integer", nullable: false),
                    ResultSetFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass2RetrievalReceipts", x => x.Id);
                    table.CheckConstraint("CK_Pass2RetrievalReceipts_Bounds", "\"AttemptOrdinal\" BETWEEN 1 AND 2 AND \"CallOrdinal\" BETWEEN 1 AND 8 AND \"ResultCount\" BETWEEN 0 AND 10 AND \"ReturnedUtf8Bytes\" BETWEEN 0 AND 65536 AND \"NewAdditionalDocumentCount\" BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_Pass2RetrievalReceipts_ClosedValues", "\"State\" IN ('Accepted','NoResults','BoundExceeded','Refused','Unavailable') AND \"Operation\" IN ('Search','Sections','SourceAnchors','SourceExcerpt','RelatedDocuments')");
                    table.CheckConstraint("CK_Pass2RetrievalReceipts_Fingerprints", receiptFingerprintCheck);
                    table.CheckConstraint("CK_Pass2RetrievalReceipts_OutcomeDeltas", "\"State\" = 'Accepted' OR (\"ResultCount\" = 0 AND \"ReturnedUtf8Bytes\" = 0 AND \"NewAdditionalDocumentCount\" = 0)");
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
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    EdgeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ByteStart = table.Column<long>(type: "bigint", nullable: true),
                    ByteEnd = table.Column<long>(type: "bigint", nullable: true),
                    DescriptorFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReturnedUtf8Bytes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pass2RetrievalResults", x => x.Id);
                    table.CheckConstraint("CK_Pass2RetrievalResults_Bounds", "\"AttemptOrdinal\" BETWEEN 1 AND 2 AND \"ResultOrdinal\" BETWEEN 1 AND 10 AND \"ReturnedUtf8Bytes\" BETWEEN 0 AND 65536");
                    table.CheckConstraint("CK_Pass2RetrievalResults_ClosedValues", "\"State\" = 'Admitted' AND \"Kind\" IN ('Document','Section','SourceAnchor','SourceExcerpt','Relationship')");
                    table.CheckConstraint("CK_Pass2RetrievalResults_Coordinates", "(\"Kind\" = 'Document' AND \"SectionId\" IS NULL AND \"SourceAnchorId\" IS NULL AND \"EdgeId\" IS NULL AND \"ByteStart\" IS NULL AND \"ByteEnd\" IS NULL) OR (\"Kind\" = 'Section' AND \"SectionId\" IS NOT NULL AND \"SourceAnchorId\" IS NULL AND \"EdgeId\" IS NULL AND \"ByteStart\" IS NULL AND \"ByteEnd\" IS NULL) OR (\"Kind\" = 'SourceAnchor' AND \"SectionId\" IS NULL AND \"SourceAnchorId\" IS NOT NULL AND \"EdgeId\" IS NULL AND \"ByteStart\" IS NULL AND \"ByteEnd\" IS NULL) OR (\"Kind\" = 'SourceExcerpt' AND \"SectionId\" IS NULL AND \"SourceAnchorId\" IS NOT NULL AND \"EdgeId\" IS NULL AND \"ByteStart\" >= 0 AND \"ByteEnd\" > \"ByteStart\") OR (\"Kind\" = 'Relationship' AND \"SectionId\" IS NULL AND \"SourceAnchorId\" IS NULL AND \"EdgeId\" IS NOT NULL AND \"ByteStart\" IS NULL AND \"ByteEnd\" IS NULL)");
                    table.CheckConstraint("CK_Pass2RetrievalResults_Fingerprints", resultFingerprintCheck);
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
                name: "IX_FlowTraceSteps_ExactResolutionDecisionId",
                schema: "vulgata",
                table: "FlowTraceSteps",
                column: "ExactResolutionDecisionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FlowTraceSteps_ExactDecision_RelationshipOnly",
                schema: "vulgata",
                table: "FlowTraceSteps",
                sql: "\"ExactResolutionDecisionId\" IS NULL OR \"StepKind\" = 'Relationship'");

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_ConsumerDocumentVersionId_RepositoryId_ScanRunId",
                schema: "vulgata",
                table: "FlowGroups",
                columns: new[] { "ConsumerDocumentVersionId", "RepositoryId", "ScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_ExactResolutionDecisionId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "ExactResolutionDecisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_ExactResolutionEdgeId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "ExactResolutionEdgeId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_Pass2ExecutionId_GroupOrder",
                schema: "vulgata",
                table: "FlowGroups",
                columns: new[] { "Pass2ExecutionId", "GroupOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_Pass2ExecutionId_PlanFingerprint",
                schema: "vulgata",
                table: "FlowGroups",
                columns: new[] { "Pass2ExecutionId", "PlanFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_ProviderImplementationDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "ProviderImplementationDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_ProviderRepositoryId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "ProviderRepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_ProviderXmlDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "ProviderXmlDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowGroups_SystemId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "SystemId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FlowGroups_Fingerprint_Lengths",
                schema: "vulgata",
                table: "FlowGroups",
                sql: "(\"PlanFingerprint\" = '' OR length(\"PlanFingerprint\") = 64) AND (\"MembershipFingerprint\" IS NULL OR length(\"MembershipFingerprint\") = 64) AND (\"ConsumerEntryStableKey\" IS NULL OR length(\"ConsumerEntryStableKey\") = 64)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FlowGroups_LogicalKeySchemaIdentity_Closed",
                schema: "vulgata",
                table: "FlowGroups",
                sql: "\"LogicalKeySchemaIdentity\" IS NULL OR \"LogicalKeySchemaIdentity\" = 'business-flow-logical-key/1.0'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FlowGroups_Protocol_Closed",
                schema: "vulgata",
                table: "FlowGroups",
                sql: "\"Protocol\" IS NULL OR \"Protocol\" IN ('CustomRpc', 'Http')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FlowGroups_ProviderRepository_CrossRepository",
                schema: "vulgata",
                table: "FlowGroups",
                sql: "\"ProviderRepositoryId\" IS NULL OR \"ProviderRepositoryId\" <> \"RepositoryId\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FlowGroups_SchemaIdentity_Closed",
                schema: "vulgata",
                table: "FlowGroups",
                sql: "\"SchemaIdentity\" IS NULL OR \"SchemaIdentity\" = 'endpoint-flow-group/1.0'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FlowGroups_State_Closed",
                schema: "vulgata",
                table: "FlowGroups",
                sql: "\"SchemaIdentity\" IS NULL OR \"State\" IN ('Complete', 'Incomplete', 'BoundExcluded', 'Running', 'Published', 'Failed')");

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

            migrationBuilder.AddForeignKey(
                name: "FK_FlowGroups_CrossRepositoryResolutionDecisions_ExactResoluti~",
                schema: "vulgata",
                table: "FlowGroups",
                column: "ExactResolutionDecisionId",
                principalSchema: "vulgata",
                principalTable: "CrossRepositoryResolutionDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlowGroups_DocumentEdges_ExactResolutionEdgeId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "ExactResolutionEdgeId",
                principalSchema: "vulgata",
                principalTable: "DocumentEdges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlowGroups_DocumentVersions_ConsumerDocumentVersionId_Repos~",
                schema: "vulgata",
                table: "FlowGroups",
                columns: new[] { "ConsumerDocumentVersionId", "RepositoryId", "ScanRunId" },
                principalSchema: "vulgata",
                principalTable: "DocumentVersions",
                principalColumns: new[] { "Id", "RepositoryId", "ScanRunId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlowGroups_DocumentVersions_ProviderImplementationDocumentV~",
                schema: "vulgata",
                table: "FlowGroups",
                column: "ProviderImplementationDocumentVersionId",
                principalSchema: "vulgata",
                principalTable: "DocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlowGroups_DocumentVersions_ProviderXmlDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "ProviderXmlDocumentVersionId",
                principalSchema: "vulgata",
                principalTable: "DocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlowGroups_Pass2Executions_Pass2ExecutionId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "Pass2ExecutionId",
                principalSchema: "vulgata",
                principalTable: "Pass2Executions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlowGroups_Repositories_ProviderRepositoryId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "ProviderRepositoryId",
                principalSchema: "vulgata",
                principalTable: "Repositories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlowGroups_Systems_SystemId",
                schema: "vulgata",
                table: "FlowGroups",
                column: "SystemId",
                principalSchema: "vulgata",
                principalTable: "Systems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlowTraceSteps_CrossRepositoryResolutionDecisions_ExactReso~",
                schema: "vulgata",
                table: "FlowTraceSteps",
                column: "ExactResolutionDecisionId",
                principalSchema: "vulgata",
                principalTable: "CrossRepositoryResolutionDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FlowGroups_CrossRepositoryResolutionDecisions_ExactResoluti~",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowGroups_DocumentEdges_ExactResolutionEdgeId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowGroups_DocumentVersions_ConsumerDocumentVersionId_Repos~",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowGroups_DocumentVersions_ProviderImplementationDocumentV~",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowGroups_DocumentVersions_ProviderXmlDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowGroups_Pass2Executions_Pass2ExecutionId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowGroups_Repositories_ProviderRepositoryId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowGroups_Systems_SystemId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowTraceSteps_CrossRepositoryResolutionDecisions_ExactReso~",
                schema: "vulgata",
                table: "FlowTraceSteps");

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

            migrationBuilder.DropIndex(
                name: "IX_FlowTraceSteps_ExactResolutionDecisionId",
                schema: "vulgata",
                table: "FlowTraceSteps");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FlowTraceSteps_ExactDecision_RelationshipOnly",
                schema: "vulgata",
                table: "FlowTraceSteps");

            migrationBuilder.DropIndex(
                name: "IX_FlowGroups_ConsumerDocumentVersionId_RepositoryId_ScanRunId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropIndex(
                name: "IX_FlowGroups_ExactResolutionDecisionId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropIndex(
                name: "IX_FlowGroups_ExactResolutionEdgeId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropIndex(
                name: "IX_FlowGroups_Pass2ExecutionId_GroupOrder",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropIndex(
                name: "IX_FlowGroups_Pass2ExecutionId_PlanFingerprint",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropIndex(
                name: "IX_FlowGroups_ProviderImplementationDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropIndex(
                name: "IX_FlowGroups_ProviderRepositoryId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropIndex(
                name: "IX_FlowGroups_ProviderXmlDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropIndex(
                name: "IX_FlowGroups_SystemId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FlowGroups_Fingerprint_Lengths",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FlowGroups_LogicalKeySchemaIdentity_Closed",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FlowGroups_Protocol_Closed",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FlowGroups_ProviderRepository_CrossRepository",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FlowGroups_SchemaIdentity_Closed",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FlowGroups_State_Closed",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "ExactResolutionDecisionId",
                schema: "vulgata",
                table: "FlowTraceSteps");

            migrationBuilder.DropColumn(
                name: "ConsumerDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "ConsumerEntryIdentity",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "ConsumerEntryStableKey",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "ExactResolutionDecisionId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "ExactResolutionEdgeId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "LogicalDocumentStableKey",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "LogicalKeySchemaIdentity",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "MembershipFingerprint",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "NormalizedEndpointIdentifier",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "Pass2ExecutionId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "PlanFingerprint",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "Protocol",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "ProviderImplementationDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "ProviderRepositoryId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "ProviderXmlDocumentVersionId",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "SchemaIdentity",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "vulgata",
                table: "FlowGroups");

            migrationBuilder.DropColumn(
                name: "SystemId",
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
