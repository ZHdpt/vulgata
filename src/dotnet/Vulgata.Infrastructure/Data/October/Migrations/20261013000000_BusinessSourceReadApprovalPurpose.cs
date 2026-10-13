using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulgata.Infrastructure.Data.October.Migrations;

/// <summary>Adds the explicit Business read purpose without replacing any existing approval.</summary>
public partial class BusinessSourceReadApprovalPurpose : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_OctoberPreparedSourceApprovals_Purpose",
            schema: "october",
            table: "OctoberPreparedSourceApprovals");
        migrationBuilder.AddCheckConstraint(
            name: "CK_OctoberPreparedSourceApprovals_Purpose",
            schema: "october",
            table: "OctoberPreparedSourceApprovals",
            sql: "\"Purpose\" IN (0, 1)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Downgrading must not delete, repurpose or silently discard Business approvals,
        // including revoked/expired rows that still describe their original purpose.
        migrationBuilder.Sql("""
            LOCK TABLE october."OctoberPreparedSourceApprovals" IN ACCESS EXCLUSIVE MODE;
            DO $guard$
            BEGIN
                IF EXISTS (SELECT 1 FROM october."OctoberPreparedSourceApprovals" WHERE "Purpose" = 1) THEN
                    RAISE EXCEPTION 'Cannot remove Business source-read purpose while Business approvals remain.'
                        USING ERRCODE = '23514';
                END IF;
            END;
            $guard$;
            """);
        migrationBuilder.DropCheckConstraint(
            name: "CK_OctoberPreparedSourceApprovals_Purpose",
            schema: "october",
            table: "OctoberPreparedSourceApprovals");
        migrationBuilder.AddCheckConstraint(
            name: "CK_OctoberPreparedSourceApprovals_Purpose",
            schema: "october",
            table: "OctoberPreparedSourceApprovals",
            sql: "\"Purpose\" = 0");
    }
}
