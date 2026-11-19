using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCheck.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SectionCDefectCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
INSERT INTO autocheck."CatalogItem" ("Id", "Type", "Name", "ParentId") VALUES
('30000000-0000-0000-0000-000000000001', 'defectos-categorias-v1', 'IDENTIFICACION', NULL),
('40000000-0000-0000-0000-000000000101', 'defectos-subcategorias-v1', 'VIN', '30000000-0000-0000-0000-000000000001'),
('40000000-0000-0000-0000-000000000102', 'defectos-subcategorias-v1', 'MATRICULA', '30000000-0000-0000-0000-000000000001'),
('30000000-0000-0000-0000-000000000002', 'defectos-categorias-v1', 'ALUMBRADO', NULL),
('40000000-0000-0000-0000-000000000201', 'defectos-subcategorias-v1', 'FAROS', '30000000-0000-0000-0000-000000000002'),
('40000000-0000-0000-0000-000000000202', 'defectos-subcategorias-v1', 'INTERMITENTES', '30000000-0000-0000-0000-000000000002'),
('30000000-0000-0000-0000-000000000003', 'defectos-categorias-v1', 'FRENOS', NULL),
('40000000-0000-0000-0000-000000000301', 'defectos-subcategorias-v1', 'SERVICIO', '30000000-0000-0000-0000-000000000003'),
('40000000-0000-0000-0000-000000000302', 'defectos-subcategorias-v1', 'ESTACIONAMIENTO', '30000000-0000-0000-0000-000000000003'),
('30000000-0000-0000-0000-000000000004', 'defectos-categorias-v1', 'DIRECCION', NULL),
('40000000-0000-0000-0000-000000000401', 'defectos-subcategorias-v1', 'HOLGURAS', '30000000-0000-0000-0000-000000000004'),
('40000000-0000-0000-0000-000000000402', 'defectos-subcategorias-v1', 'ASISTENCIA', '30000000-0000-0000-0000-000000000004'),
('30000000-0000-0000-0000-000000000005', 'defectos-categorias-v1', 'SUSPENSION', NULL),
('40000000-0000-0000-0000-000000000501', 'defectos-subcategorias-v1', 'AMORTIGUADORES', '30000000-0000-0000-0000-000000000005'),
('40000000-0000-0000-0000-000000000502', 'defectos-subcategorias-v1', 'ANCLAJES', '30000000-0000-0000-0000-000000000005'),
('30000000-0000-0000-0000-000000000006', 'defectos-categorias-v1', 'NEUMATICOS', NULL),
('40000000-0000-0000-0000-000000000601', 'defectos-subcategorias-v1', 'DIBUJO', '30000000-0000-0000-0000-000000000006'),
('40000000-0000-0000-0000-000000000602', 'defectos-subcategorias-v1', 'RUEDAS', '30000000-0000-0000-0000-000000000006'),
('30000000-0000-0000-0000-000000000007', 'defectos-categorias-v1', 'CHASIS', NULL),
('40000000-0000-0000-0000-000000000701', 'defectos-subcategorias-v1', 'ESTRUCTURA', '30000000-0000-0000-0000-000000000007'),
('40000000-0000-0000-0000-000000000702', 'defectos-subcategorias-v1', 'CARROCERIA', '30000000-0000-0000-0000-000000000007'),
('30000000-0000-0000-0000-000000000008', 'defectos-categorias-v1', 'EMISIONES', NULL),
('40000000-0000-0000-0000-000000000801', 'defectos-subcategorias-v1', 'GASES', '30000000-0000-0000-0000-000000000008'),
('40000000-0000-0000-0000-000000000802', 'defectos-subcategorias-v1', 'ESCAPE', '30000000-0000-0000-0000-000000000008'),
('30000000-0000-0000-0000-000000000009', 'defectos-categorias-v1', 'SEGURIDAD', NULL),
('40000000-0000-0000-0000-000000000901', 'defectos-subcategorias-v1', 'CINTURONES', '30000000-0000-0000-0000-000000000009'),
('40000000-0000-0000-0000-000000000902', 'defectos-subcategorias-v1', 'VISIBILIDAD', '30000000-0000-0000-0000-000000000009')
ON CONFLICT ("Id") DO NOTHING;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Academic catalog references are preserved; use a reviewed forward migration.");
        }
    }
}
