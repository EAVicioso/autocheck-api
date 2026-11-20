Feature: DEC-LIFECYCLE-01 contratos de los seis módulos
  # Gherkin determinista; trazabilidad de pruebas en docs/lifecycle-slices.md.

  @A01 @CU-01
  Scenario: A01 Auth/RegistrarCiudadano
    Given un correo sintético único y contraseña válida
    When solicita POST /api/auth/registro
    Then crea un Ciudadano sin permisos y devuelve Location

  @A02 @CU-02
  Scenario: A02 Auth/IniciarSesion
    Given credenciales confirmadas, cuenta activa y no bloqueada
    When solicita POST /api/auth/login
    Then registra session_id y versión de seguridad, sin guardar tokens

  @A03 @CU-02
  Scenario: A03 Auth/RenovarSesion
    Given un refresh token y sesión local con menos de 60 minutos de inactividad
    When solicita POST /api/auth/refresh
    Then rota tokens sin actualizar LastActivity

  @A04 @IS-04
  Scenario: A04 Auth/SolicitarRecuperacion
    Given un correo con formato válido
    When solicita POST /api/auth/recuperacion
    Then responde genéricamente sin revelar si existe la cuenta

  @A05 @RS-09
  Scenario: A05 Auth/RestablecerContrasena
    Given un bearer de recuperación y nueva contraseña válida
    When solicita POST /api/auth/restablecimiento
    Then confirma primero la barrera durable y revoca sesiones al finalizar

  @A06 @CU-02
  Scenario: A06 Auth/CerrarSesion
    Given un JWT propio criptográficamente válido
    When solicita POST /api/auth/logout
    Then revoca localmente una vez y permite reintentar la revocación remota

  @A07 @RS-10
  Scenario: A07 Auth/RegistrarActividad
    Given una sesión activa y una acción explícita del usuario
    When solicita POST /api/auth/actividad
    Then actualiza LastActivity y añade auditoría atómicamente

  @AU01 @CU-13
  Scenario: AU01 Auditorias/ConsultarAuditorias
    Given un auditor global y filtros autorizados
    When solicita GET /api/auditorias
    Then devuelve hasta 20 eventos y un cursor estable de fecha e id

  @AU02 @CU-13
  Scenario: AU02 Auditorias/ObtenerAuditoria
    Given un auditor global y evento existente
    When solicita GET /api/auditorias/{id}
    Then devuelve IP, correlación y snapshots JSON sanitizados

  @U01 @CU-11
  Scenario: U01 Usuarios/ObtenerPerfil
    Given una sesión activa vinculada a un usuario
    When solicita GET /api/usuarios/me
    Then devuelve solo su perfil y versión

  @U02 @CU-11
  Scenario: U02 Usuarios/ActualizarPerfil
    Given un perfil propio con version vigente
    When solicita PATCH /api/usuarios/me
    Then actualiza nombre y celular y aumenta version una vez

  @U03 @CU-11
  Scenario: U03 Usuarios/ListarUsuarios
    Given un administrador con usuarios.gestionar
    When solicita GET /api/usuarios
    Then filtra rol, activo, nombre y archivados dentro de su alcance

  @U04 @CU-11
  Scenario: U04 Usuarios/ObtenerUsuario
    Given un administrador con acceso al centro del usuario
    When solicita GET /api/usuarios/{id}
    Then devuelve el perfil administrativo sin credenciales

  @U05 @CU-11
  Scenario: U05 Usuarios/ArchivarUsuario
    Given un usuario sin trabajo pendiente, no último administrador y version vigente
    When solicita DELETE /api/usuarios/{id}?version=N
    Then conserva identidad e historial, archiva y revoca sesiones

  @P01 @CU-11
  Scenario: P01 Permisos/ListarRoles
    Given un administrador con usuarios.gestionar
    When solicita GET /api/roles
    Then devuelve exactamente Ciudadano, Inspector y Administrador

  @P02 @CU-11
  Scenario: P02 Permisos/ListarPermisos
    Given un administrador con usuarios.gestionar
    When solicita GET /api/permisos
    Then devuelve los cuatro permisos del catálogo aprobado

  @P03 @CU-11
  Scenario: P03 Permisos/ObtenerAcceso
    Given un administrador autorizado sobre el usuario
    When solicita GET /api/usuarios/{id}/acceso
    Then devuelve rol, permisos, centro, activo y version actuales

  @P04 @CU-11
  Scenario: P04 Permisos/CambiarAcceso
    Given un acceso editable con version vigente y centro compatible
    When solicita PATCH /api/usuarios/{id}/acceso
    Then aplica rol y permisos y los hace efectivos en la siguiente solicitud

  @V01 @IU-01
  Scenario: V01 Vehiculos/ConsultarCatalogo
    Given un tipo marcas, modelos, colores o categorias válido
    When solicita GET /api/catalogos/{tipo}
    Then devuelve catálogos paginados por nombre y padreId

  @V02 @IU-01
  Scenario: V02 Vehiculos/RegistrarVehiculo
    Given un propietario activo, VIN y matrícula únicos y catálogos válidos
    When solicita POST /api/vehiculos
    Then crea el vehículo propio con version 1 y Location

  @V03 @CU-10
  Scenario: V03 Vehiculos/ListarVehiculos
    Given una sesión con vehículos autorizados
    When solicita GET /api/vehiculos
    Then filtra matrícula y archivados y ordena por id o matrícula

  @V04 @CU-10
  Scenario: V04 Vehiculos/ObtenerVehiculo
    Given un vehículo existente dentro del alcance del actor
    When solicita GET /api/vehiculos/{id}
    Then devuelve una proyección del vehículo sin entidades EF

  @V05 @IU-01
  Scenario: V05 Vehiculos/ActualizarVehiculo
    Given un vehículo propio activo y version vigente
    When solicita PATCH /api/vehiculos/{id}
    Then actualiza los campos permitidos y aumenta version

  @V06 @IU-01
  Scenario: V06 Vehiculos/ArchivarVehiculo
    Given un vehículo propio sin reservas ni inspecciones pendientes
    When solicita DELETE /api/vehiculos/{id}?version=N
    Then archiva conservando unicidad, historial y certificados

  @V07 @CU-10
  Scenario: V07 Inspecciones/ConsultarHistorial
    Given un vehículo autorizado con cierres inmutables
    When solicita GET /api/vehiculos/{id}/historial
    Then filtra resultado y fechas locales y pagina por cierre e id

  @C01 @CU-06
  Scenario: C01 Certificados/SolicitarCertificado
    Given una inspección propia cerrada y aprobada con clave idempotente
    When solicita POST /api/inspecciones/{id}/certificado
    Then reserva o recupera la misma identidad y Location sin regenerar un PDF publicado

  @C02 @CU-06
  Scenario: C02 Certificados/ListarCertificados
    Given certificados de un propietario autenticado
    When solicita GET /api/certificados
    Then filtra vehículo, estado y rango UTC con cursor de emisión e id

  @C03 @CU-06
  Scenario: C03 Certificados/ObtenerCertificado
    Given un certificado propio existente
    When solicita GET /api/certificados/{id}
    Then devuelve estado y hashes sin leer los bytes PDF

  @C04 @CU-06
  Scenario: C04 Certificados/DescargarCertificado
    Given un certificado propio emitido y firmado
    When solicita GET /api/certificados/{id}/pdf
    Then devuelve exactamente los bytes persistidos sin regeneración

  @C05 @CU-07
  Scenario: C05 Certificados/VerificarCertificado
    Given un certificado emitido con hash recibido, almacenado y recalculado iguales
    When solicita GET /api/certificados/{id}/verificacion?hash=sha256
    Then expone exactamente cinco campos públicos y resultado VALIDA

  Scenario Outline: Falta de autenticación en una ruta protegida
    Given que no se envía Authorization
    When solicita <metodo> <ruta>
    Then devuelve 401 sin ejecutar el handler
    And Cache-Control es no-store
    Examples:
      | metodo | ruta |
      | POST | /api/auth/logout |
      | POST | /api/auth/actividad |
      | GET | /api/auditorias |
      | GET | /api/auditorias/{id} |
      | GET | /api/usuarios/me |
      | PATCH | /api/usuarios/me |
      | GET | /api/usuarios |
      | GET | /api/usuarios/{id} |
      | DELETE | /api/usuarios/{id}?version=N |
      | GET | /api/roles |
      | GET | /api/permisos |
      | GET | /api/usuarios/{id}/acceso |
      | PATCH | /api/usuarios/{id}/acceso |
      | GET | /api/catalogos/{tipo} |
      | POST | /api/vehiculos |
      | GET | /api/vehiculos |
      | GET | /api/vehiculos/{id} |
      | PATCH | /api/vehiculos/{id} |
      | DELETE | /api/vehiculos/{id}?version=N |
      | GET | /api/vehiculos/{id}/historial |
      | POST | /api/inspecciones/{id}/certificado |
      | GET | /api/certificados |
      | GET | /api/certificados/{id} |
      | GET | /api/certificados/{id}/pdf |

  Scenario: Version obsoleta bajo concurrencia
    Given dos comandos con la misma version de un perfil propio
    When se ejecutan concurrentemente
    Then solo uno confirma y el otro devuelve 409
    And la version aumenta una vez y existe una sola auditoría de actualización

  Scenario: Escalación mediante metadata o mass assignment
    Given un ciudadano con metadata editable role Administrador
    When consulta permisos o envía campos de rol en registro
    Then la consulta devuelve 403 y el registro con campos desconocidos devuelve 400
    And no cambia el rol funcional en PostgreSQL

  Scenario: Límite y tamaño en identidad
    Given una IP que agotó 30 solicitudes de identidad durante un minuto
    When solicita otra operación de identidad
    Then devuelve 429 con Retry-After
    And un JSON que supera 128 KiB se rechaza con 413 antes de efectos

  Scenario: Integridad de certificado
    Given un certificado firmado y publicado
    When se altera el manifiesto o se envía otro hash SHA-256
    Then la verificación devuelve 409 y registra el rechazo crítico
    And las descargas repetidas autorizadas conservan exactamente los bytes firmados

  Scenario: Inmutabilidad en PostgreSQL
    Given auditorías y un certificado emitido
    When el rol ordinario intenta cambiar o eliminar su evidencia
    Then PostgreSQL rechaza la operación y conserva el historial
