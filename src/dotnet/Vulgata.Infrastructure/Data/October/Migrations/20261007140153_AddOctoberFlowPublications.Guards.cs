using Microsoft.EntityFrameworkCore.Migrations;

namespace Vulgata.Infrastructure.Data.October.Migrations;

public partial class AddOctoberFlowPublications
{
    private static void InstallFlowGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE october."OctoberBusinessFinalizations" ADD CONSTRAINT "CK_Finalization_Operation" CHECK ("OperationKind"=3);
            ALTER TABLE october."OctoberLogicalFlows" ADD CONSTRAINT "CK_LogicalFlow_Operation" CHECK ("OperationKind"=3);
            ALTER TABLE october."OctoberFlowGroupVersions" ADD CONSTRAINT "CK_FlowGroup_Operation" CHECK ("OperationKind"=3);

            CREATE OR REPLACE FUNCTION october."RequireNewOctoberKnowledgeParent"() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE parent_xid xid;
            BEGIN
                CASE TG_TABLE_NAME
                WHEN 'AcceptedFileManifests' THEN
                    SELECT xmin INTO parent_xid FROM october."KnowledgeOperations" p
                    WHERE p."BatchId"=NEW."BatchId" AND p."Kind"=NEW."OperationKind" AND p."OperationId"=NEW."OperationId";
                WHEN 'OctoberCodeUnits' THEN
                    SELECT xmin INTO parent_xid FROM october."AcceptedFileManifests" p
                    WHERE p."BatchId"=NEW."BatchId" AND p."ManifestId"=NEW."ManifestId";
                WHEN 'OctoberLogicalDocuments' THEN
                    IF NEW."Kind"=0 THEN
                        SELECT xmin INTO parent_xid FROM october."OctoberCodeUnits" p
                        WHERE p."BatchId"=NEW."BatchId" AND p."UnitId"=NEW."UnitId";
                    ELSE
                        SELECT xmin INTO parent_xid FROM october."OctoberLogicalFlows" p
                        WHERE p."BatchId"=NEW."BatchId" AND p."LogicalFlowId"=NEW."LogicalFlowId";
                    END IF;
                WHEN 'OctoberDocumentVersions' THEN
                    IF NEW."Kind"=0 THEN
                        SELECT xmin INTO parent_xid FROM october."AcceptedFileManifests" p
                        WHERE p."BatchId"=NEW."BatchId" AND p."ManifestId"=NEW."ManifestId";
                    ELSE
                        SELECT xmin INTO parent_xid FROM october."OctoberBusinessFinalizations" p
                        WHERE p."BatchId"=NEW."BatchId" AND p."OperationKind"=NEW."FinalizationOperationKind" AND p."OperationId"=NEW."FinalizationOperationId";
                    END IF;
                WHEN 'OctoberDocumentSections' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberDocumentVersions" p
                    WHERE p."BatchId"=NEW."BatchId" AND p."VersionId"=NEW."VersionId";
                WHEN 'OctoberQualifiedClaimGroups' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberDocumentSections" p
                    WHERE p."BatchId"=NEW."BatchId" AND p."VersionId"=NEW."VersionId" AND p."SectionId"=NEW."SectionId";
                WHEN 'KnowledgeContributors' THEN
                    SELECT xmin INTO parent_xid FROM october."AcceptedFileManifests" p
                    WHERE p."BatchId"=NEW."BatchId" AND p."ManifestId"=NEW."ManifestId";
                WHEN 'OctoberCommunicationObservations' THEN
                    SELECT xmin INTO parent_xid FROM october."OctoberCodeUnits" p
                    WHERE p."BatchId"=NEW."BatchId" AND p."ManifestId"=NEW."ManifestId" AND p."UnitId"=NEW."UnitId";
                WHEN 'OctoberKnowledgeUncertainties' THEN
                    SELECT xmin INTO parent_xid FROM october."AcceptedFileManifests" p
                    WHERE p."BatchId"=NEW."BatchId" AND p."ManifestId"=NEW."ManifestId";
                ELSE RAISE EXCEPTION 'Unknown October Knowledge parent' USING ERRCODE='23514';
                END CASE;
                IF parent_xid IS NULL OR parent_xid <> pg_current_xact_id()::xid THEN
                    RAISE EXCEPTION 'Cannot extend committed October Knowledge' USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END $body$;

            CREATE FUNCTION october."RequireNewOctoberFlowParent"() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE parent_xid xid;
            BEGIN
                IF TG_TABLE_NAME IN ('OctoberBusinessFinalizations','OctoberLogicalFlows','OctoberFlowGroupVersions') THEN
                    SELECT xmin INTO parent_xid FROM october."KnowledgeOperations" p
                    WHERE p."BatchId"=NEW."BatchId" AND p."Kind"=NEW."OperationKind" AND p."OperationId"=NEW."OperationId";
                ELSE
                    SELECT xmin INTO parent_xid FROM october."OctoberFlowGroupVersions" p
                    WHERE p."BatchId"=NEW."BatchId" AND p."CorpusId"=NEW."CorpusId" AND p."GroupVersionId"=NEW."GroupVersionId";
                END IF;
                IF parent_xid IS NOT NULL AND parent_xid <> pg_current_xact_id()::xid THEN
                    RAISE EXCEPTION 'Cannot extend committed flow inputs or results' USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END $body$;

            CREATE FUNCTION october."ValidateOctoberFlowOwnership"() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE document_kind integer; document_flow uuid; group_flow uuid; group_corpus uuid; document_group uuid;
            BEGIN
                IF TG_TABLE_NAME='OctoberDocumentVersions' THEN
                    IF NEW."Kind"=1 THEN
                    SELECT "LogicalFlowId" INTO document_flow FROM october."OctoberLogicalDocuments"
                    WHERE "BatchId"=NEW."BatchId" AND "LogicalDocumentId"=NEW."LogicalDocumentId";
                    SELECT "LogicalFlowId","CorpusId" INTO group_flow,group_corpus FROM october."OctoberFlowGroupVersions"
                    WHERE "BatchId"=NEW."BatchId" AND "GroupVersionId"=NEW."FlowGroupVersionId";
                    IF document_flow IS NOT NULL AND group_flow IS NOT NULL AND document_flow <> group_flow THEN
                        RAISE EXCEPTION 'BL group does not own this logical document' USING ERRCODE='23503';
                    END IF;
                    IF group_corpus IS NOT NULL AND NOT EXISTS (SELECT 1 FROM october."OctoberBusinessFinalizations"
                        WHERE "BatchId"=NEW."BatchId" AND "OperationKind"=NEW."FinalizationOperationKind"
                        AND "OperationId"=NEW."FinalizationOperationId" AND "CorpusId"=group_corpus) THEN
                        RAISE EXCEPTION 'BL finalization does not own this corpus' USING ERRCODE='23503';
                    END IF;
                    END IF;
                ELSIF TG_TABLE_NAME='OctoberPublicationMembers' THEN
                    SELECT "Kind","FlowGroupVersionId" INTO document_kind,document_group FROM october."OctoberDocumentVersions"
                    WHERE "BatchId"=NEW."BatchId" AND "LogicalDocumentId"=NEW."LogicalDocumentId" AND "VersionId"=NEW."VersionId";
                    IF document_kind=0 AND NOT EXISTS (SELECT 1 FROM october."OctoberCorpusDocuments"
                        WHERE "BatchId"=NEW."BatchId" AND "CorpusId"=NEW."CorpusId"
                        AND "LogicalDocumentId"=NEW."LogicalDocumentId" AND "VersionId"=NEW."VersionId") THEN
                        RAISE EXCEPTION 'CL publication requires exact frozen corpus membership' USING ERRCODE='23503';
                    ELSIF document_kind=1 AND NOT EXISTS (SELECT 1 FROM october."OctoberFlowGroupVersions"
                        WHERE "BatchId"=NEW."BatchId" AND "CorpusId"=NEW."CorpusId" AND "GroupVersionId"=document_group) THEN
                        RAISE EXCEPTION 'BL publication requires exact corpus group membership' USING ERRCODE='23503';
                    END IF;
                ELSIF TG_TABLE_NAME='OctoberFlowGroupVersions' THEN
                    IF NOT EXISTS (SELECT 1 FROM october."OctoberLogicalFlows" f
                        JOIN october."OctoberLogicalDocuments" d ON d."BatchId"=f."BatchId" AND d."UnitId"=f."RootUnitId"
                        WHERE f."BatchId"=NEW."BatchId" AND f."LogicalFlowId"=NEW."LogicalFlowId"
                        AND d."LogicalDocumentId"=NEW."RootLogicalDocumentId") THEN
                        RAISE EXCEPTION 'Flow group must retain its exact root unit document' USING ERRCODE='23503';
                    END IF;
                ELSIF TG_TABLE_NAME='OctoberBusinessFinalizations' THEN
                    IF NEW."PublicationId" IS NOT NULL THEN
                    IF NOT EXISTS (SELECT 1 FROM october."OctoberKnowledgePublications" p
                        WHERE p."BatchId"=NEW."BatchId" AND p."PublicationId"=NEW."PublicationId" AND p."CorpusId"=NEW."CorpusId"
                        AND p."OperationKind"=NEW."OperationKind" AND p."OperationId"=NEW."OperationId") THEN
                        RAISE EXCEPTION 'Finalization must own its exact publication' USING ERRCODE='23503';
                    END IF;
                    END IF;
                END IF;
                RETURN NEW;
            END $body$;

            CREATE TRIGGER "FlowOwnerAgreement" BEFORE INSERT ON october."OctoberDocumentVersions"
                FOR EACH ROW EXECUTE FUNCTION october."ValidateOctoberFlowOwnership"();
            CREATE TRIGGER "FlowOwnerAgreement" BEFORE INSERT ON october."OctoberPublicationMembers"
                FOR EACH ROW EXECUTE FUNCTION october."ValidateOctoberFlowOwnership"();
            CREATE TRIGGER "FlowOwnerAgreement" BEFORE INSERT ON october."OctoberFlowGroupVersions"
                FOR EACH ROW EXECUTE FUNCTION october."ValidateOctoberFlowOwnership"();
            CREATE TRIGGER "FlowOwnerAgreement" BEFORE INSERT ON october."OctoberBusinessFinalizations"
                FOR EACH ROW EXECUTE FUNCTION october."ValidateOctoberFlowOwnership"();
            """);
        foreach (string table in new[] { "OctoberBusinessFinalizations", "OctoberLogicalFlows", "OctoberFlowGroupVersions",
            "OctoberFlowDocumentDependencies", "OctoberFlowRelationshipDependencies", "OctoberFlowResolutionUncertaintyDependencies", "OctoberFlowFileUncertaintyDependencies" })
        {
            migrationBuilder.Sql($"""
                CREATE TRIGGER "ImmutableFlowHistory" BEFORE UPDATE OR DELETE ON october."{table}"
                    FOR EACH ROW EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
                CREATE TRIGGER "ImmutableFlowHistoryTruncate" BEFORE TRUNCATE ON october."{table}"
                    FOR EACH STATEMENT EXECUTE FUNCTION october."RejectOctoberKnowledgeMutation"();
                CREATE TRIGGER "NewFlowParentOnly" BEFORE INSERT ON october."{table}"
                    FOR EACH ROW EXECUTE FUNCTION october."RequireNewOctoberFlowParent"();
                """);
        }
    }
}
