# SMTP: API y Supabase Auth

La API envía confirmaciones/cancelaciones mediante MailKit 5.0 y el outbox PostgreSQL. Supabase Auth genera y envía sus propias confirmaciones de cuenta y recuperación. Ambos usan SMTP de SendGrid, con **dos claves independientes limitadas a Mail Send**. No crear endpoints propios para generar tokens de recuperación ni duplicar los correos de Auth desde la API.

## Configuración de la API

| Configuración | Valor para SendGrid |
|---|---|
| `Smtp__Host` | `smtp.sendgrid.net` |
| `Smtp__Port` | `587` |
| `Smtp__Transport` | `StartTls`; alternativamente `SslOnConnect` con puerto `465` |
| `Smtp__Username` | `apikey` (literal) |
| `Smtp__Password` | Clave Mail Send destinada exclusivamente a notificaciones de API |
| `Smtp__From` | Correo verificado en Sender Authentication |
| `Smtp__SenderName` | `AutoCheck` |

No existen modos sin TLS ni TLS opcional. Se valida el certificado del servidor; las pruebas usan un certificado efímero fijado exclusivamente en su cliente aislado. Cada entrega tiene un presupuesto total de cinco segundos y respeta cancelación. Los clientes SMTP son scoped porque no admiten envíos concurrentes sobre la misma conexión.

PowerShell 7 permite configurar la máquina sin poner contraseñas en argumentos o archivos del repositorio:

```powershell
pwsh -File scripts/configure-smtp.ps1 -From 'remitente@dominio-del-equipo.example'
dotnet run --project AutoCheck.Api -c Release -- --check-smtp
```

El script solicita la clave mediante entrada protegida y guarda únicamente la configuración de API en user-secrets. También admite `AUTOCHECK_SMTP_PASSWORD` desde un almacén de secretos. No guardar credenciales en `appsettings`, `.http`, GitHub Actions YAML o el PR. En Azure usar configuración protegida/Key Vault; user-secrets solo resuelve desarrollo local.

`--check-smtp` comprueba conexión, TLS, autenticación y NOOP: **no envía correo**, no consulta PostgreSQL ni inicia HTTP/workers. La falta de configuración conserva pendientes los mensajes del outbox y no impide desarrollar los demás endpoints.

## Supabase Auth

En Authentication → Email → SMTP settings del proyecto AutoCheckBD, activar custom SMTP con el mismo host/puerto/usuario/remitente y **la clave separada de Auth**. Mantener confirmación de email habilitada y conservar los redirects exactos del frontend. Desactivar click tracking del proveedor para evitar que reescriba enlaces de autenticación.

También se puede automatizar con el API oficial de administración:

```powershell
pwsh -File scripts/configure-smtp.ps1 -From 'remitente@dominio-del-equipo.example' -ConfigureSupabase
```

Además de la contraseña de API, solicita por entrada protegida `AUTOCHECK_AUTH_SMTP_PASSWORD` y `SUPABASE_ACCESS_TOKEN` si esas variables no existen. El token de administración necesita acceso al proyecto y no se guarda. El script lee configuración, cambia solamente los campos SMTP y comprueba el resultado; no imprime respuestas ni secretos y no modifica Site URL/redirects o límites de envío.

El límite inicial de custom SMTP de Supabase y el cupo del proveedor son independientes. Revisarlos en sus paneles antes de una demostración con todo el equipo. La prueba de SendGrid tiene vencimiento/cupo; no representa un servicio gratuito indefinido. Usar el Gmail del titular como remitente individual sirve para validar desarrollo, pero SendGrid advierte de posibles fallos DMARC. Para entrega estable se necesita un dominio autorizado por el equipo con SPF/DKIM/DMARC, sin comprar ni modificar un dominio institucional sin autorización.

## Entrega, reintentos y pruebas

Se preserva el `Message-Id` derivado del ID del outbox y el header `X-AutoCheck-Delivery-Id`. Un rechazo SMTP deja el mensaje pendiente para reintentar. Un fallo incierto después de la aceptación SMTP y antes del commit PostgreSQL puede duplicar correo: el protocolo no ofrece exactamente una entrega. No se repiten pagos/citas por este motivo. Una confirmación superada por cancelación no se envía.

`SmtpDeliveryTests` comprueba STARTTLS obligatorio, rechazo de certificados no confiables, timeout, configuración segura y diagnóstico sin envío. `SmtpFlowTests` recorre Kestrel → pago → cita → outbox/worker → SMTP TLS para confirmar/cancelar, y comprueba rechazo/reintento con identidad estable. El servidor aislado se enlaza exclusivamente a loopback, recibe sin retransmitir y no instala certificados en el sistema. Las pruebas no necesitan SendGrid ni claves reales; CI conserva la cobertura con servicios efímeros.

La recuperación ahora responde 503 si Supabase rechaza la solicitud (incluidos errores de correo o rate limit), sin exponer la respuesta del proveedor ni confirmar la existencia de una cuenta. Una respuesta válida de Auth conserva el mensaje genérico 202.

La aceptación SMTP demuestra que el servidor recibió el mensaje, no que llegó a la bandeja principal. Verificar también Event Activity/spam y el enlace real de confirmación/recuperación antes de declarar entrega externa validada.

## Documentación oficial

- [Supabase: SMTP personalizado](https://supabase.com/docs/guides/auth/auth-smtp).
- [Supabase: configuración de Auth mediante Management API](https://supabase.com/docs/reference/api/v1-update-auth-service-config).
- [SendGrid: integración SMTP](https://www.twilio.com/docs/sendgrid/for-developers/sending-email/integrating-with-the-smtp-api).
- [SendGrid: identidad de remitente](https://www.twilio.com/docs/sendgrid/ui/sending-email/sender-verification).
- [Microsoft: SmtpClient de System.Net.Mail no recomendado para desarrollos nuevos](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient?view=net-10.0).
- [MailKit: opciones TLS](https://mimekit.net/docs/html/T_MailKit_Security_SecureSocketOptions.htm).
