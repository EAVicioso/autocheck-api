# Activación de revisión y protección

Versión 1.0 · 2026-11-11 · Equipo AutoCheck. Estos pasos requieren administración del repositorio; un archivo versionado no activa una GitHub App ni un ruleset por sí solo.

1. Integrar el PR después de revisar sus checks. Habilitar GitHub Actions si está desactivado y mantener habilitada la dependency graph de NuGet.
2. Verificar los checks `build`, `unit`, `integration`, `api-compatibility`, `database-compatibility`, `docker`, `dependency-review` y `ci-required`. El checkout predeterminado de `pull_request` es el merge sintético; no cambiarlo por el SHA del head.
3. Verificar que el análisis CodeQL C# publicó resultados. El ruleset exige resultados CodeQL y bloquea seguridad high/critical y severidad error; no basta con un workflow que termine.
4. Desde una sesión GitHub CLI del propietario, ejecutar `pwsh ./scripts/enable-protection.ps1`. Usa `.github/rulesets/main.json`, actualiza el ruleset del mismo nombre y verifica reglas efectivas. También puede importarse el JSON desde Settings → Rules → Rulesets.
5. Instalar [CodeRabbit](https://github.com/apps/coderabbitai) con acceso **solo a EAVicioso/autocheck-api**, verificar acceso OSS para el repositorio público y que lea `.coderabbit.yaml`. No introducir tarjeta ni activar consumo pagado. Abrir un PR listo y comprobar la revisión; subir un nuevo commit y comprobar la revisión incremental.
6. Mantener una aprobación humana externa al autor. Los controles de autoaprobación y edición del bot están desactivados. Si el revisor alcanza su límite OSS, el humano documenta su revisión; las pruebas/protecciones siguen siendo obligatorias.

El ruleset bloquea push directo, force push, eliminación y merge sin aprobación/checks; invalida aprobaciones obsoletas y exige main actualizado. No tiene actores de bypass. El dueño conserva la capacidad administrativa de editar reglas; eso no es una garantía contra un administrador que deliberadamente las elimine.

El repositorio es personal: no se configura Merge Queue. Si en el futuro se transfiere a una organización y se activa esa función, añadir `merge_group` a los workflows obligatorios antes de exigir la cola.

## Comprobación de bloqueo

Usar PR de prueba sin integrar para inducir: una prueba fallida, un contrato OpenAPI incompatible, una migración inválida y una rama desactualizada. Confirmar que GitHub impide el merge en cada caso y luego cerrar los PR. No desactivar los checks para obtener verde.

## Costo y fuentes

Runners estándar gratuitos para repositorios públicos: https://docs.github.com/en/actions/reference/runners/github-hosted-runners

CodeRabbit OSS y límites: https://docs.coderabbit.ai/management/plans

Revisión incremental: https://docs.coderabbit.ai/configuration/auto-review

Protecciones: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches

Code scanning: https://docs.github.com/en/code-security/how-tos/find-and-fix-code-vulnerabilities/manage-your-configuration/set-merge-protection
