using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignScaffoldSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_incident_comments_incidents_IncidentId",
                table: "incident_comments");

            migrationBuilder.DropForeignKey(
                name: "FK_incident_status_changes_incidents_IncidentId",
                table: "incident_status_changes");

            migrationBuilder.DropForeignKey(
                name: "FK_incidents_users_OwnerUserId",
                table: "incidents");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "users",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "users",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "users",
                newName: "display_name");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "incidents",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "incidents",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Severity",
                table: "incidents",
                newName: "severity");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "incidents",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "incidents",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "incidents",
                newName: "owner_user_id");

            migrationBuilder.RenameColumn(
                name: "OccurredAtUtc",
                table: "incidents",
                newName: "occurred_at_utc");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "incidents",
                newName: "created_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_incidents_OwnerUserId",
                table: "incidents",
                newName: "IX_incidents_owner_user_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "incident_status_changes",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ToStatus",
                table: "incident_status_changes",
                newName: "to_status");

            migrationBuilder.RenameColumn(
                name: "IncidentId",
                table: "incident_status_changes",
                newName: "incident_id");

            migrationBuilder.RenameColumn(
                name: "FromStatus",
                table: "incident_status_changes",
                newName: "from_status");

            migrationBuilder.RenameColumn(
                name: "ChangedByDisplayName",
                table: "incident_status_changes",
                newName: "changed_by_display_name");

            migrationBuilder.RenameColumn(
                name: "ChangedAtUtc",
                table: "incident_status_changes",
                newName: "changed_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_incident_status_changes_IncidentId",
                table: "incident_status_changes",
                newName: "IX_incident_status_changes_incident_id");

            migrationBuilder.RenameColumn(
                name: "Body",
                table: "incident_comments",
                newName: "body");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "incident_comments",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "IsInternal",
                table: "incident_comments",
                newName: "is_internal");

            migrationBuilder.RenameColumn(
                name: "IncidentId",
                table: "incident_comments",
                newName: "incident_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "incident_comments",
                newName: "created_at_utc");

            migrationBuilder.RenameColumn(
                name: "AuthorDisplayName",
                table: "incident_comments",
                newName: "author_display_name");

            migrationBuilder.RenameIndex(
                name: "IX_incident_comments_IncidentId",
                table: "incident_comments",
                newName: "IX_incident_comments_incident_id");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at_utc",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddForeignKey(
                name: "FK_incident_comments_incidents_incident_id",
                table: "incident_comments",
                column: "incident_id",
                principalTable: "incidents",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_incident_status_changes_incidents_incident_id",
                table: "incident_status_changes",
                column: "incident_id",
                principalTable: "incidents",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_incidents_users_owner_user_id",
                table: "incidents",
                column: "owner_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_incident_comments_incidents_incident_id",
                table: "incident_comments");

            migrationBuilder.DropForeignKey(
                name: "FK_incident_status_changes_incidents_incident_id",
                table: "incident_status_changes");

            migrationBuilder.DropForeignKey(
                name: "FK_incidents_users_owner_user_id",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "incidents");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "users",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "users",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "display_name",
                table: "users",
                newName: "DisplayName");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "incidents",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "incidents",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "severity",
                table: "incidents",
                newName: "Severity");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "incidents",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "incidents",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "owner_user_id",
                table: "incidents",
                newName: "OwnerUserId");

            migrationBuilder.RenameColumn(
                name: "occurred_at_utc",
                table: "incidents",
                newName: "OccurredAtUtc");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "incidents",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_incidents_owner_user_id",
                table: "incidents",
                newName: "IX_incidents_OwnerUserId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "incident_status_changes",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "to_status",
                table: "incident_status_changes",
                newName: "ToStatus");

            migrationBuilder.RenameColumn(
                name: "incident_id",
                table: "incident_status_changes",
                newName: "IncidentId");

            migrationBuilder.RenameColumn(
                name: "from_status",
                table: "incident_status_changes",
                newName: "FromStatus");

            migrationBuilder.RenameColumn(
                name: "changed_by_display_name",
                table: "incident_status_changes",
                newName: "ChangedByDisplayName");

            migrationBuilder.RenameColumn(
                name: "changed_at_utc",
                table: "incident_status_changes",
                newName: "ChangedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_incident_status_changes_incident_id",
                table: "incident_status_changes",
                newName: "IX_incident_status_changes_IncidentId");

            migrationBuilder.RenameColumn(
                name: "body",
                table: "incident_comments",
                newName: "Body");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "incident_comments",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "is_internal",
                table: "incident_comments",
                newName: "IsInternal");

            migrationBuilder.RenameColumn(
                name: "incident_id",
                table: "incident_comments",
                newName: "IncidentId");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "incident_comments",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "author_display_name",
                table: "incident_comments",
                newName: "AuthorDisplayName");

            migrationBuilder.RenameIndex(
                name: "IX_incident_comments_incident_id",
                table: "incident_comments",
                newName: "IX_incident_comments_IncidentId");

            migrationBuilder.AddForeignKey(
                name: "FK_incident_comments_incidents_IncidentId",
                table: "incident_comments",
                column: "IncidentId",
                principalTable: "incidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_incident_status_changes_incidents_IncidentId",
                table: "incident_status_changes",
                column: "IncidentId",
                principalTable: "incidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_incidents_users_OwnerUserId",
                table: "incidents",
                column: "OwnerUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
