using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iOSClub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finance_account",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    academic_year = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    budget_amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    remark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_by = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_account", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "finance_flow",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    material_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    material_no = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    activity_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    academic_year = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    operator_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    operator_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_flow", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "finance_log",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    operator_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    operator_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    target_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    before_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    after_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "finance_material",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    activity_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    activity_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activity_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    location = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    headcount = table.Column<int>(type: "integer", nullable: true),
                    owner = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    activity_budget_cents = table.Column<long>(type: "bigint", nullable: false),
                    total_amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    remark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    academic_year = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    form_version = table.Column<int>(type: "integer", nullable: false),
                    apply_form_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    reimburse_form_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_by = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_material", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "finance_template",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    file_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_template", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "finance_invoice",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    material_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    buyer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    seller = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    item = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    qty = table.Column<decimal>(type: "numeric", nullable: true),
                    price_cents = table.Column<long>(type: "bigint", nullable: true),
                    unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    tax_cents = table.Column<long>(type: "bigint", nullable: false),
                    total_cents = table.Column<long>(type: "bigint", nullable: false),
                    confidence_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    file_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recognition_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recognized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_invoice", x => x.id);
                    table.ForeignKey(
                        name: "FK_finance_invoice_finance_material_material_id",
                        column: x => x.material_id,
                        principalTable: "finance_material",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finance_account_academic_year",
                table: "finance_account",
                column: "academic_year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_flow_academic_year_created_at",
                table: "finance_flow",
                columns: new[] { "academic_year", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_flow_material_id",
                table: "finance_flow",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "IX_finance_flow_status_type",
                table: "finance_flow",
                columns: new[] { "status", "type" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoice_code_number",
                table: "finance_invoice",
                columns: new[] { "code", "number" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoice_material_id_is_deleted",
                table: "finance_invoice",
                columns: new[] { "material_id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoice_status",
                table: "finance_invoice",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_finance_log_action",
                table: "finance_log",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "IX_finance_log_created_at",
                table: "finance_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_finance_log_target_type_target_id",
                table: "finance_log",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_material_created_at",
                table: "finance_material",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_finance_material_status_academic_year",
                table: "finance_material",
                columns: new[] { "status", "academic_year" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_template_kind_enabled",
                table: "finance_template",
                columns: new[] { "kind", "enabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finance_account");

            migrationBuilder.DropTable(
                name: "finance_flow");

            migrationBuilder.DropTable(
                name: "finance_invoice");

            migrationBuilder.DropTable(
                name: "finance_log");

            migrationBuilder.DropTable(
                name: "finance_template");

            migrationBuilder.DropTable(
                name: "finance_material");
        }
    }
}
