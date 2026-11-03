# Transferencia de trabajo: GestiOpera / Billares POS

Fecha de corte: 2026-09-11

Este documento permite continuar el proyecto desde otro computador o desde una nueva sesión de ChatGPT/Codex. El código fuente está en el repositorio privado:

`https://github.com/JuanSe2436/billares-pos.git`

## 1. Objetivo del proyecto

Construir un POS SaaS multi-tenant para billares y establecimientos similares. Debe permitir administrar mesas de billar, sesiones por tiempo, consumos de productos, ventas, pagos, caja, inventario, reportes, auditoría y notificaciones.

El establecimiento de demostración actual es `Billar Demo`, con moneda COP y zona horaria `America/Bogota`. El dominio productivo es `https://gestiopera.com`.

## 2. Contexto funcional acordado

- El cliente final probablemente no conoce conceptos técnicos como tenant, Meta Business Manager o API de WhatsApp.
- En el login se decidió quitar el campo “Tenant”/“Código del negocio”. El usuario debe identificarse mediante su correo; el backend resuelve el tenant automáticamente cuando el correo corresponde a un único usuario activo.
- La primera alternativa de notificaciones fue WhatsApp, pero se pospuso porque el cliente no tiene WhatsApp Business ni configuración de Meta. Se implementó correo electrónico como primera opción.
- Las notificaciones de ventas deben llegar al correo configurado del propietario y, cuando está habilitado, incluir el comprobante PDF adjunto.
- El correo receptor se configura en la pantalla de notificaciones/preferencias. Si queda vacío, se usa el correo de acceso del propietario.
- El remitente se configuró en Brevo. Las credenciales viven únicamente en variables de entorno del servidor y nunca deben entrar a Git.
- En una venta se agregó el campo opcional “Nombre para la venta o factura”. Ese nombre debe aparecer en el detalle, en el PDF y en la notificación cuando corresponda.
- El detalle de una venta debe mostrar número, fecha, cajero, productos, cantidades, total, medios de pago y a nombre de quién se hizo la venta.
- En sesiones de billar debe quedar claro qué mesa fue, cuánto tiempo transcurrió, cuánto se cobró por billar, qué consumos hubo y cuál fue el total.
- Los consumos de una sesión activa pueden corregirse antes del cobro, pero una venta ya cobrada no debe permitir borrar consumos ni alterar su histórico. La eliminación accidental de un consumo durante una prueba dejó una sesión que no podía cerrarse; este flujo debe validarse y blindarse.

## 3. Funcionalidades implementadas

### Backend

- Solución .NET 10 con proyectos Domain, Application, Infrastructure, API e IntegrationTests.
- Multi-tenancy con `TenantId`, `ITenantOwned`, filtros globales de EF Core y tenant del JWT.
- ASP.NET Core Identity, roles `OWNER`, `ADMINISTRATOR` y `CASHIER`.
- JWT access tokens y refresh-token rotation.
- Manejo centralizado de excepciones, validaciones y logging estructurado.
- Productos activos, SKU, costo, precio de venta, mínimos y críticos.
- Libro de movimientos de inventario, compras con costo promedio ponderado, ajustes y conteos físicos.
- Ventas con snapshots históricos de nombre/precio/costo del producto.
- Pagos `Cash`, `Nequi`, `Daviplata`, `Card`, `BankTransfer` y `Other`.
- Cancelación de ventas con razón y movimientos reversos.
- Mesas de billar, sesiones, iniciar/pausar/reanudar/cerrar y consumos.
- Cobro de billar por incremento configurado, usando redondeo hacia arriba y tarifa capturada al iniciar.
- Cierre transaccional de sesión: venta, consumos, inventario, pagos, caja y liberación de mesa.
- SignalR en `/hubs/billiards`, aislado por tenant.
- Caja: apertura, turno actual, gastos, cierre, diferencia e historial.
- Dashboard diario con ventas, productos, billar, gastos, efectivo esperado, mesas activas, stock bajo, medios de pago y ventas recientes.
- Reportes y generación de comprobantes PDF.
- Auditoría y notificaciones persistidas.
- Canales de notificación: push, correo, WhatsApp (implementación preparada, aún no es el canal operativo principal).
- Jobs de entrega de notificaciones posteriores a la operación.

### Frontend

- React 19, TypeScript, Vite, Tailwind CSS, PWA.
- TanStack Query para estado del servidor y Zustand para autenticación persistida.
- Shell compartido con navegación, usuario activo, cierre de sesión y responsive.
- Rutas principales: `/login`, `/dashboard`, `/pos`, `/tables`, `/cash`, `/products`, `/purchases`, `/inventory`, `/inventory/count`, `/notifications`, `/reports`, `/audit`, `/employees`, `/settings`.
- POS con catálogo, búsqueda, carrito, cantidades, eliminación, descuento, medio de pago y nombre de la venta/factura.
- Mesas con temporizador, pausa, reanudación, consumos y cierre.
- Notificaciones con pestañas “Todas”/“No leídas”, contador, botón “Detalle”, marcar como leída y modal de detalle de venta.
- En el encabezado móvil se agregó campana con contador de pendientes.
- Se tradujo el medio de pago `Cash` a “Efectivo” en la interfaz y el detalle.
- Se agregaron estilos responsive para que las notificaciones no desborden en pantallas pequeñas.

## 4. Decisiones técnicas importantes

- El inventario histórico es `InventoryMovement`; `Product.CurrentStock` es una proyección optimizada.
- Las operaciones financieras/inventario se ejecutan en transacciones.
- Las entidades tenant-owned siempre deben consultarse mediante el `AppDbContext` filtrado, salvo casos administrativos explícitos.
- Las ventas y cierres de mesa deben generar notificaciones después del commit, usando jobs.
- Las sesiones de billar reservan lógicamente consumos y descuentan stock al cerrar.
- La sesión debe tener un turno de caja abierto antes de iniciarse.
- No se borran físicamente productos; se desactivan.
- El nombre de cliente es opcional y se almacena como `Sale.CustomerName`.
- `PaymentMethod` y DTOs conservan nombres técnicos en inglés; la interfaz debe traducirlos al español.

## 5. Estructura relevante

- `src/BillaresPos.Domain`: entidades y enums de negocio.
- `src/BillaresPos.Application`: abstracciones como `IEmailService`, `IPdfService`, `ICurrentTenant` e `ICurrentUser`.
- `src/BillaresPos.Infrastructure`: EF Core/PostgreSQL, Identity, migraciones, PDFs y proveedores de notificación.
- `src/BillaresPos.Api`: endpoints, middleware, autenticación, jobs y composición.
- `frontend/src/pages`: pantallas del producto.
- `frontend/src/components/AppLayout.tsx`: shell y navegación.
- `frontend/src/api.ts`: cliente HTTP y tipos de operaciones.
- `frontend/src/types.ts`: contratos TypeScript.
- `frontend/src/styles.css`: estilos globales y responsive.
- `tests/BillaresPos.IntegrationTests`: pruebas de aislamiento, ventas, caja, inventario, compras, reportes, auditoría y billar.
- `src/BillaresPos.Infrastructure/Persistence/Migrations`: migraciones EF, incluyendo `NotificationEmailAddress`, `TenantBranding` y `SaleCustomerName`.
- `docker-compose.production.yml`: despliegue productivo con PostgreSQL, API, frontend y Caddy.
- `Caddyfile`: HTTPS y enrutamiento del dominio.
- `README.md`: instalación local básica.
- `ARCHITECTURE.md`: decisiones arquitectónicas iniciales.
- `docs/IMPLEMENTATION_LOG.md`: historial detallado de incrementos.

## 6. Integración de correo

La selección del proveedor se controla por configuración:

- `Email:Enabled=true`
- `Email:Provider=Brevo`
- `Email:BrevoApiKey=<secreto del servidor>`
- `Email:From=<remitente verificado>`
- `Email:FromName=GestiOpera`
- `Email:SendReceipt=true` para adjuntar comprobantes de venta cuando el PDF ya está generado.

Implementación principal: `src/BillaresPos.Infrastructure/Notifications/BrevoEmailService.cs`.

Entrega: `src/BillaresPos.Api/Jobs/NotificationJobs.cs`.

El receptor se obtiene desde `NotificationPreferences.EmailAddress`; si no existe, se usa el email del usuario propietario. La configuración del remitente en Brevo quedó verificada y se observaron correos recibidos en Gmail. Algunos mensajes llegaron sin adjunto, por lo que el estado de generación/orden de los PDFs debe revisarse.

No guardar API keys, contraseñas SMTP/Gmail ni archivos `.env` en el repositorio. Una clave de Brevo fue expuesta durante la conversación anterior y debe considerarse comprometida: revocarla y generar una nueva antes de usar producción.

## 7. Problemas y errores encontrados

- Cloudflare inicialmente mostraba DNS sin resolver; se corrigieron los registros A de raíz y `www` hacia `161.35.54.58`.
- La pantalla de login mostraba credenciales precargadas. Ya se eliminaron los valores por defecto del frontend.
- El campo Tenant/Código de negocio se consideró confuso; el login actual usa el correo para resolver automáticamente el tenant.
- Apareció `An unexpected error occurred` en POS y en login. Se debe revisar siempre la pestaña Network y los logs del API; el frontend no debe ocultar errores HTTP útiles.
- Hubo errores de despliegue/migración al agregar `Sale.CustomerName`; la columna y el registro de migración fueron regularizados en producción.
- Los textos con tildes aparecieron corruptos en algunos dispositivos por problemas de codificación o entrega de archivos. Verificar UTF-8, `charset=utf-8`, archivos TSX/CSS y navegador móvil.
- En móvil, el menú lateral no podía desplazarse de forma independiente y el scroll se aplicaba a la página. El drawer debe tener altura limitada, `overflow-y: auto`, y el body debe bloquearse mientras está abierto.
- La campana móvil debía estar arriba a la derecha, junto al título/actualizar, aprovechando el espacio disponible.
- La lista de notificaciones debía estar dentro de un contenedor desplazable para evitar bajar toda la página. Revisar que ese contenedor no tenga altura o ancho que rompa el modal.
- Las tarjetas móviles de notificaciones se desbordaban cuando se añadió el botón “Detalle”; deben mantener una disposición legible en 320px-430px.
- En detalles de venta se corrigió `Cash` a “Efectivo”. Revisar que no quede ningún texto técnico visible.
- Las notificaciones de venta necesitan identificar claramente número, nombre de cliente, mesa/sesión y detalle de lo vendido.
- README y documentos de fases contienen partes antiguas que dicen que PDFs, caja, reportes o notificaciones están pendientes; actualizar documentación cuando se cierre esta etapa.

## 8. Estado del repositorio y producción

- Rama principal: `main`.
- Repositorio remoto: `https://github.com/JuanSe2436/billares-pos.git`.
- Últimos commits conocidos:
  - `7c23ac6 Initial commit for Billares POS`
  - `9476689 Exclude local configuration and build artifacts`
- El árbol local estaba limpio al crear esta transferencia.
- `.gitignore` excluye `.env`, `frontend/.env.local`, `frontend/node_modules`, `frontend/dist`, `.artifacts`, `bin`, `obj` y archivos de usuario.
- Producción corre en `/opt/gestopera` con Docker Compose. Servicios: `api`, `frontend`, `caddy` y `postgres`.
- El API expone internamente el puerto 8080; Caddy publica 80/443.
- El dominio productivo es `https://gestiopera.com`.
- El estado observado fue: API, frontend, Caddy y PostgreSQL activos; PostgreSQL saludable.

## 9. Preparar el otro computador

Instalar Git, .NET SDK 10, Node.js/npm, Docker Desktop si se requiere desarrollo local y Codex. Luego:

```powershell
git clone https://github.com/JuanSe2436/billares-pos.git
cd billares-pos
codex
```

Para desarrollo local:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

Para frontend separado:

```powershell
cd frontend
npm install
npm run dev
```

No copiar secretos al repositorio. Crear `.env` local a partir de `.env.example` y solicitar los valores seguros por un canal privado.

## 10. Verificación recomendada al retomar

1. Ejecutar `git status` y confirmar que la rama sea `main`.
2. Leer `README.md`, `ARCHITECTURE.md`, este archivo y `docs/IMPLEMENTATION_LOG.md`.
3. Ejecutar `dotnet build BillaresPos.slnx`.
4. Ejecutar `dotnet test BillaresPos.slnx`.
5. Ejecutar `npm install` y `npm run build` en `frontend`.
6. Probar login sin tenant y confirmar que campos estén vacíos.
7. Probar venta con nombre, notificación y PDF adjunto.
8. Probar cierre de mesa con tiempo, mesa, consumos, total y nombre.
9. Probar eliminación/corrección de consumos antes del cobro y bloqueo después del cobro.
10. Probar en viewport móvil real: drawer, campana, tarjetas de notificaciones, modal de detalle y tildes.

## 11. Próximos pasos priorizados

1. Revisar por qué algunos emails de venta se entregan sin PDF adjunto y asegurar el orden entre generación del documento y `NotificationJobs`.
2. Mejorar el detalle de venta para mostrar mesa, duración de billar y nombre de cliente cuando la venta proviene de una sesión.
3. Blindar el flujo de consumos: corrección antes del cobro, no borrado después del cobro, validación de stock y mensajes claros.
4. Corregir definitivamente codificación UTF-8 y revisar todos los textos españoles en iOS/Android.
5. Validar responsive móvil con 320px, 375px, 390px y 430px, especialmente drawer y notificaciones.
6. Reemplazar mensajes genéricos `An unexpected error occurred` por mensajes operativos y detalles seguros.
7. Actualizar `README.md`, `ARCHITECTURE.md` y `docs/IMPLEMENTATION_PLAN.md` para reflejar el estado real.
8. Agregar CI de GitHub Actions para build, test y frontend build.
9. Revisar warnings de seguridad de dependencias, especialmente `System.Security.Cryptography.Xml`.
10. Rotar la clave Brevo expuesta y confirmar que la nueva solo esté en variables de entorno productivas.

## 12. Prompt inicial para una nueva sesión

Usar este texto al abrir Codex en el repositorio:

> Continuemos con GestiOpera / Billares POS. Lee `HANDOFF.md`, `README.md`, `ARCHITECTURE.md` y `docs/IMPLEMENTATION_LOG.md`. Inspecciona primero el estado actual del repositorio y no reviertas cambios existentes. El siguiente foco es revisar las notificaciones por correo con adjunto PDF, el detalle de ventas con mesa/duración/nombre y el responsive móvil. No expongas ni solicites secretos dentro de archivos versionados.
