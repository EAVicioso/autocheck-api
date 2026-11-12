# Seguimiento de la revisión del PR #1

Fuente: [observaciones de Engels Vicioso](https://github.com/EAVicioso/autocheck-api/pull/1#issuecomment-6812208638) sobre `96f0ff9`. El PR fue aprobado con observaciones y se fusionó mediante `c172ba9`. El merge no convierte los pendientes en completados.

| Pendiente | Responsable de seguimiento | Criterio de cierre | Estado en esta entrega documental |
|---|---|---|---|
| Límites directos de retención | Manuel Polo (B) | Conservar concurrencia/reasignación y añadir casos separados a 300 y 301 segundos sin reemplazar antes la retención. Verificar reversión, ausencia de cita/bloque y deduplicación del evento. | Pendiente; el caso de 300 con reasignación no aísla expiración y no existía caso directo de 301 |
| Índice de expiración | Manuel Polo (B), coordinado con Sebastián por el esquema común | Índice sobre `BookingBlock(State, Expires)` o alternativa justificada, nueva migración aditiva y snapshot; verificar creación/upgrade y plan con datos representativos | Implementado en `20261112050410_HardenDatabaseBoundary` y aplicado a Supabase el 12/11/2026. Creación/upgrade verificados; falta medir plan con datos representativos para cerrar aceptación de rendimiento. |
| Ruleset y revisor automático | Engels Vicioso como propietario; Sebastián mantiene configuración de CI | Activar/verificar controles siguiendo `docs/github-setup.md` y demostrar bloqueo real de PR inválidos | Pendiente de verificar configuración remota; un JSON versionado no basta |
| Proveedores y firma en staging | A/B/C según el adaptador | Configurar secretos fuera del repo y verificar Supabase, Gemini, SendGrid y CMS reales | Pendiente; CI utiliza dobles y clave académica temporal |
| Rendimiento RS-08 | Equipo A/B/C | Ejecutar arnés k6, perfil de red individual y conservar evidencia de umbrales SRS | Pendiente; no declarar aceptación formal |

La revisión verificó 20 pruebas unitarias y 21 de integración aprobadas en aquel commit. Esta cifra no acredita los dos casos de expiración faltantes ni verifica cambios posteriores. Cualquier PR que los incorpore debe enlazar esta tabla y aportar evidencia nueva. No ampliar afirmaciones de cobertura a partir de una condición de código sin probarla.

Esta entrega incorpora la guía compartida y los responsables reales; registra las observaciones sin modificar lógica, migraciones ni configuración remota. Los responsables son puntos de coordinación, no asignaciones automáticas en GitHub.
