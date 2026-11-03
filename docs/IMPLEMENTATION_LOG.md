# Implementation Log

This document records verified changes and decisions for each increment. ARCHITECTURE.md describes the current architecture.

## 2026-09-07 - Foundation and Phase 2 increment 1

### Scope

- Initialized the .NET 10 modular monolith with Domain, Application, Infrastructure, API and IntegrationTests projects.
- Implemented Tenant, ASP.NET Core Identity, OWNER/ADMINISTRATOR/CASHIER roles, JWT access tokens and refresh-token rotation.
- Implemented tenant isolation with TenantId, EF Core global query filters and tenant resolution from the JWT claim.
- Added Serilog structured logging, centralized exception handling, Docker Compose, initial migration and development seed.
- Started Phase 2 with products and a ledger-based inventory model.

### Decisions

- InventoryMovement is the authoritative history; Product.CurrentStock is an optimized projection.
- Inventory commands verify tenant context in the backend and run inside database transactions.
- Initial inventory adjustments are limited to Owner/Administrator; purchases and sales will integrate with this ledger in later increments.
- Products are disabled through IsActive; physical deletion is not exposed.

### Verification

- dotnet build BillaresPos.slnx --no-restore: passed.
- dotnet test BillaresPos.slnx --no-restore: 10 tests passed.
- Docker Compose PostgreSQL smoke test: container healthy, migrations applied, seed completed, GET /health returned status ok.
- Generated migrations: ProductsInventory and Purchases.
- Purchase tests cover weighted-average cost, rollback and tenant isolation.
- NU1903 transitive dependency warnings remain and did not block execution.

### Known pending work

- Run a smoke test against real PostgreSQL with Docker Compose.
- Add GitHub Actions CI.
- Complete sales and PostgreSQL concurrency tests.
- Review transitive dependency vulnerability warnings.






### Verificacion manual en Postman

- Login del owner: `200 OK`.
- Consulta del tenant actual: `200 OK`, tenant `billar-demo`, moneda `COP`.
- Creacion de producto `Poker` con SKU `POKER-001`: `201 Created`.
- Creacion de compra `FAC-001`: `201 Created`, total `76800`, un item.
- Pendiente de confirmar en la siguiente consulta: stock `24` y costo promedio `3200`.
- Consulta de productos despu?s de la compra: `200 OK`, stock `24` y costo promedio `3200.00` confirmados.


## 2026-09-08 - Ventas y pagos

### Alcance

- Added Sale, SaleItem and SalePayment with tenant isolation.
- Added product sales with price and cost snapshots.
- Added split payments for CASH, NEQUI, DAVIPLATA, CARD, BANK_TRANSFER and OTHER.
- Added atomic stock decrement and payment validation.
- Added sale cancellation with reason, actor, timestamp and reversing inventory movements.
- Added SalesPayments migration and applied it against the local PostgreSQL database.

### Verification

- dotnet build BillaresPos.slnx --no-restore: passed.
- dotnet test BillaresPos.slnx --no-restore: 14 tests passed.
- PostgreSQL tables sales, sale_items and sale_payments confirmed in the local container.
- GET /health against the running API returned status ok.

### Assumptions and pending work

- CashShiftId remains nullable until the Cash Management module is implemented.
- Product sales are implemented first; BilliardTime items will be added with the billiards module.
- Audit events, asynchronous notifications and receipt PDFs remain pending modules.
- Sequential number generation is protected by a serializable transaction; concurrent PostgreSQL stress testing remains pending.


## 2026-09-08 - Correccion de entrada JSON de ventas

- Configured minimal API JSON options with JsonStringEnumConverter so payment methods can be sent as `Cash`, `Nequi`, `Daviplata`, `Card`, `BankTransfer` or `Other`.
- Invalid JSON bodies and unsupported enum values now return `400 Bad Request` instead of `500 Unexpected error`.
- Manual API verification: sale with `method: Cash` returned `201 Created`, total `2000 COP`, sequential number `1`.
- Regression test added; final suite result: 15 tests passed.
- Manual Postman sale verification: `201 Created`, sale sequential number `2`, total `10000 COP`, status `Completed`, one product item.
- Manual inventory verification after sale: `200 OK`, Poker stock `22.00`, average cost `3200.00 COP`.

## 2026-09-08 - Mesas de billar, sesiones y consumos

### Alcance

- Added tenant-owned BilliardTable, BilliardSession and BilliardConsumption entities.
- Added table lifecycle endpoints: create, list, start, pause and resume.
- Added SignalR hub at `/hubs/billiards`, isolated by tenant group.
- Added consumption snapshots; stock is reserved logically and decremented only when the session closes.
- Added transactional session closing that creates a Sale, BilliardTime and product SaleItems, payments and inventory movements atomically.
- Added Billiards migration `20260908043531_Billiards`.

### Decisions

- Billiard time uses ceiling billing by the configured increment, with the rate captured when the session starts.
- Consumptions preserve product name, sale price and average cost snapshots for historical reporting.
- `CashShiftId` remains nullable until Cash Management is implemented.
- Audit records, receipt generation and asynchronous notifications remain pending.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `dotnet test BillaresPos.slnx --no-restore`: 18 tests passed.
- Added tenant isolation test for a billiard table and transactional closing test with inventory decrement.

## 2026-09-08 - React/PWA para mesas de billar

### Alcance

- Created `frontend/` with React 19, TypeScript, Vite, Tailwind CSS and PWA manifest.
- Added login against the existing JWT endpoint.
- Added responsive billiard table board with start, pause, resume and close actions.
- Added elapsed timers and COP formatting.
- Added SignalR client connection with automatic table refresh after `tableUpdated` events.
- Added Zustand only for persisted client authentication state and TanStack Query for server state.
- Added development CORS policy for `http://localhost:5173`.

### Verification

- `npm install`: completed without vulnerabilities reported by npm audit.
- `npm run build`: passed.
- Backend tests remain at 18 passing tests before the CORS-only API change.

### Known scope

- The close dialog asks the operator for the final amount because the current API does not yet expose a session detail/calculation endpoint with pending consumptions.
- Product consumption entry and the owner dashboard will be added in subsequent frontend iterations.


## 2026-09-08 - Consumos de mesa en frontend

### Alcance

- Added `GET /api/billiards/sessions/{sessionId}` with calculated elapsed time, billed minutes, billiard charge, product consumptions and total.
- Added React product selector, quantity input and consumption list inside active table management.
- Closing a table now defaults to the API-calculated total and validates it transactionally in the backend.

### Verification

- `npm run build`: passed.
- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `dotnet test BillaresPos.slnx --no-restore`: 18 tests passed.


## 2026-09-08 - Correccion del seed de desarrollo

- Added automatic creation of `Mesa 1` through `Mesa 4` for the demo tenant when no tables exist.
- API startup verified and logged four seeded tables.


## 2026-09-08 - Caja y gastos

### Alcance

- Added tenant-owned CashShift and Expense entities.
- Added cash opening, current summary, closing and expense endpoints.
- Cash expected value is opening cash plus CASH sale payments minus CASH expenses.
- Sales are automatically associated with the authenticated user's open shift.
- Added React cash screen at `/cash` with opening, summary, expenses and closing.
- Added CashManagement migration.

### Verification

- `dotnet test BillaresPos.slnx --no-restore`: 20 tests passed.
- `npm run build`: passed.
- API started successfully and applied pending migrations.


## 2026-09-08 - Correccion de caja en frontend

- Added `/api/cash/current/close` so closing resolves the authenticated user's current open shift instead of relying on a cached shift id.
- Owner/Administrator expense registration can use an open tenant shift while cashiers remain restricted to their own shift.
- Added `step="0.01"` to monetary inputs to accept COP cents without browser validation errors.

### Verification

- `dotnet test BillaresPos.slnx --no-restore`: 20 tests passed.
- `npm run build`: passed.


## 2026-09-08 - Correccion de pausa y cobro de mesas

- La tarjeta de mesa ahora recibe `pausedAtUtc` y el temporizador descuenta el intervalo de pausa actualmente activo.
- Cerrar una mesa pausada ya no exige reanudarla; el backend calcula y persiste los segundos pausados hasta el momento del cierre.
- El cobro de una mesa se asocia al turno de caja abierto del usuario mediante `Sale.CashShiftId`.
- El cierre continúa siendo transaccional: venta, pagos, consumos, inventario y disponibilidad de la mesa se confirman juntos.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `dotnet test BillaresPos.slnx --no-restore`: 20 tests passed.
- `npm run build`: passed.
## 2026-09-08 - Prevencion de doble cierre de caja

- El boton de cierre de turno queda bloqueado mientras la solicitud esta en curso.
- Se evita que un doble clic genere una segunda solicitud que responda `Open cash shift not found` despues de un cierre exitoso.

### Verification

- `npm run build`: passed.
## 2026-09-08 - Correccion definitiva de doble cierre de caja

- Se corrigio la funcion de cierre de la pantalla React para bloquear reentrada mientras `closeCashShift` esta pendiente.
- El boton muestra `Cerrando...` y no permite una segunda solicitud concurrente.
- El frontend fue recompilado correctamente.
## 2026-09-08 - Mensajes operativos en español

- Se tradujeron al español los mensajes de validación y error de mesas, sesiones, consumos, inventario y caja.
- Se mantuvieron sin cambios los nombres técnicos de rutas, DTOs y enums internos.
- API reiniciada con los mensajes actualizados.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `npm run build`: passed.
## 2026-09-08 - Estado posterior al cierre de caja

- Después de cerrar un turno, la pantalla limpia inmediatamente el turno activo y muestra el formulario para abrir el siguiente.
- El turno cerrado y su diferencia permanecen almacenados históricamente en la base de datos.
- La consulta de actualización se mantiene en segundo plano para confirmar el estado del servidor.

### Verification

- `npm run build`: passed.
## 2026-09-08 - Historial de turnos de caja

- Added tenant-scoped `GET /api/cash/history`.
- Owner/Administrator can view closed shifts for the tenant; Cashier can view only own shifts.
- History includes opening cash, cash sales, cash expenses, expected cash, counted cash and difference.
- Added the closed-shift history section to the Caja screen.
- No migration was required because the data already existed in `CashShift`.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `dotnet test BillaresPos.slnx --no-restore`: 20 tests passed.
- `npm run build`: passed.
## 2026-09-08 - Historial de caja en interfaz

- Se incorporo el historial de turnos cerrados en la pantalla de Caja.
- Se muestra fecha, efectivo inicial, ventas, gastos, esperado y diferencia.
- La consulta usa GET /api/cash/history con aislamiento por tenant y alcance por rol.
- Frontend y backend fueron validados despues de la integracion.


## 2026-09-08 - Turno obligatorio para iniciar mesas

- El backend ahora exige un turno de caja abierto antes de iniciar una mesa.
- La validaci�n aplica aunque la solicitud se haga directamente contra la API.
- Se agreg� una prueba de integraci�n para impedir iniciar mesas sin turno.
- La suite pas� de 20 a 21 pruebas correctas.


## 2026-09-08 - Primera iteracion de rediseño visual

- Se incorporo un shell compartido para las pantallas autenticadas: navegacion lateral, identidad del producto, usuario activo y cierre de sesion.
- Se aplico a Mesas de billar y Caja para mejorar jerarquia visual, agrupacion de acciones, espaciado y responsive en tablet/movil.
- Se mantuvo la logica funcional existente; el cambio es principalmente de presentacion y navegacion.
- Se corrigio el cierre JSX del nuevo shell y una secuencia literal pendiente en App.tsx.
- Se dejaron estilos de compatibilidad como overrides acotados para no reescribir aun toda la interfaz.

### Verification

- `npm run build`: passed.
- Advertencias existentes de Rollup sobre anotaciones PURE de SignalR, sin impacto en la compilacion.

### Next step

- Revisar visualmente Mesas y Caja con usuario real y continuar con el dashboard del propietario usando este mismo shell.

## 2026-09-08 - Dashboard de supervision inicial

- Se creo el modulo `Dashboard` en la API con `GET /api/dashboard`, protegido para OWNER y ADMINISTRATOR.
- El resumen calcula el dia de negocio usando la zona horaria configurada por tenant y convierte sus limites a UTC.
- Incluye ventas del dia, ingresos de productos y billar, gastos, efectivo esperado, mesas activas, productos bajo minimo, pagos agrupados y ventas recientes.
- Las consultas usan el `AppDbContext` tenant-scoped y no aceptan un TenantId enviado por el cliente.
- Se agrego la pantalla React `/dashboard` con metricas, actividad, mesas activas, stock bajo y medios de pago.
- El dashboard es ahora la ruta inicial despues del login y comparte el shell visual con Mesas y Caja.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `npm run build`: passed.
- Persisten unicamente las advertencias conocidas de vulnerabilidades de paquetes y anotaciones PURE de SignalR.

### Runtime verification

- API reiniciada en `http://localhost:5080`.
- `GET /api/dashboard` probado con el usuario Owner de `billar-demo` contra PostgreSQL local.
- Respuesta demo validada: ventas del dia 57.000 COP, producto 22.000 COP, billar 35.000 COP, gastos 5.000 COP, 4 mesas configuradas y 2 productos bajo minimo.
- `dotnet test BillaresPos.slnx --no-restore`: 21 pruebas correctas.

## 2026-09-08 - Ajustes visuales posteriores al dashboard

- Se agrego el acceso visible a `Resumen` en la navegacion lateral.
- Se reorganizo el historial de Caja en dos columnas con separacion clara entre fecha, movimiento, esperado y diferencia.
- Se agrego comportamiento responsive para que cada registro se apile correctamente en pantallas pequenas.
- Se reemplazo el separador con caracteres corruptos por un separador legible.

### Verification

- `npm run build`: passed.

## 2026-09-08 - Primera pantalla POS funcional

- Se agrego la ruta `/pos` y el acceso `Ventas` al menu lateral.
- La pantalla permite buscar productos, agregarlos al carrito, modificar cantidades, quitar lineas, aplicar descuento y seleccionar medio de pago.
- La confirmacion usa `POST /api/sales`, por lo que stock, movimiento de inventario, pagos y caja permanecen bajo la transaccion del backend.
- Al completar una venta se invalidan productos y dashboard para refrescar existencias e indicadores.
- Se agregaron estilos funcionales responsive; el refinamiento visual queda para la pasada final.

### Verification

- `npm run build`: passed.
- Las reglas de inventario y pagos siguen centralizadas en backend.

## 2026-09-08 - Correccion de marcador visible en navegacion

- Se elimino la secuencia literal `` `r`n `` que aparecia entre las opciones Ventas y Mesas de billar.
- Se verifico que no quedaran marcadores de salto de linea literales en `AppLayout.tsx`.

### Verification

- `npm run build`: passed.

## 2026-09-08 - Administracion inicial de productos e inventario

- Se agrego la ruta `/products` para listar productos activos con busqueda por nombre o SKU.
- Se agrego creacion de productos con precio y umbrales de stock.
- Se agregaron ajustes controlados de inventario usando `POST /api/inventory/{productId}/adjust` y movimiento `Adjustment`.
- Se agrego desactivacion de productos sin borrado fisico.
- Se mantuvo la edicion de precios fuera de esta iteracion porque el backend aun necesita un endpoint con auditoria para esa operacion sensible.
- Se documentaron los cambios de interfaz; el refinamiento visual se deja para la etapa final.

### Verification

- `npm run build`: passed.

## 2026-09-08 - Registro visible de Productos en navegacion

- Se corrigio el registro de `ProductsPage` en `App.tsx`.
- Se agrego la ruta protegida `/products`.
- Se agrego la opcion `Productos` bajo Administracion en el shell compartido.
- Se eliminaron marcadores literales de salto de linea que podian mostrarse en la interfaz.

### Verification

- `npm run build`: passed.

## 2026-09-08 - Compras y entradas de inventario

- Se agrego la ruta `/purchases` y el acceso Compras en el menu lateral.
- La pantalla permite registrar proveedor, referencia, productos, cantidades y costo unitario.
- El total se calcula antes de confirmar y la compra se envia a `POST /api/purchases`.
- Al completar la compra se refrescan productos y dashboard para reflejar stock y costos.
- La transaccion, el movimiento de inventario y el costo promedio siguen siendo responsabilidad del backend.

### Verification

- `npm run build`: passed.

## 2026-09-08 - Historial de movimientos de inventario

- Se agrego `GET /api/inventory/movements`, protegido para OWNER y ADMINISTRATOR y limitado al tenant actual.
- Se muestran los ultimos 100 movimientos con producto, tipo, cantidad, costo, nota y fecha.
- Se agrego la ruta `/inventory` y la pantalla de trazabilidad.
- El endpoint fue probado contra PostgreSQL local y devolvio 11 movimientos del tenant demo.
- El conteo fisico queda como siguiente paso porque requiere persistir actas de conteo con cantidades del sistema, fisicas, diferencia, usuario y fecha.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `npm run build`: passed.
- Historial de movimientos consultado correctamente en runtime.

## 2026-09-08 - Conteos físicos de inventario

- Se agregaron PhysicalInventoryCount y PhysicalInventoryCountItem para conservar actas de conteo con tenant, usuario, fecha, cantidad del sistema, cantidad física y diferencia.
- Se creó la migración 20260908162904_PhysicalInventoryCounts y se aplicó a PostgreSQL local.
- Se agregó POST /api/inventory/physical-counts, protegido para OWNER y ADMINISTRATOR, con aislamiento por tenant.
- La confirmación usa una transacción Serializable, genera movimientos PHYSICAL_COUNT_ADJUSTMENT únicamente cuando existe diferencia y actualiza la existencia proyectada sin sobrescribir el historial.
- Se agregó la pantalla /inventory/count y el acceso Conteo físico en el menú administrativo.
- La pantalla permite revisar existencias activas, registrar cantidades físicas y ver la diferencia antes de confirmar.
- La confirmación invalida productos e historial de movimientos para reflejar el resultado inmediatamente.

### Verification

- dotnet build BillaresPos.slnx --no-restore: passed.
- npm run build: passed.
- dotnet test BillaresPos.slnx --no-build --no-restore: 21 passed.
- Prueba runtime contra PostgreSQL y API local: acta creada correctamente con itemCount=1 y adjustedItemCount=0.
## 2026-09-08 - Reportes diarios y mensuales

- Se creó el módulo backend Reports con GET /api/reports?from=YYYY-MM-DD&to=YYYY-MM-DD.
- El endpoint está protegido para OWNER y ADMINISTRATOR y utiliza el tenant autenticado.
- Los límites del período respetan America/Bogota del tenant, almacenando y consultando timestamps en UTC.
- Se incluyen ventas brutas, ingresos de productos, ingresos de billar, COGS, utilidad bruta, gastos, resultado operativo, efectivo, pagos electrónicos, horas de billar y ventas canceladas.
- Se incluyen desgloses por medio de pago, día, empleado y producto.
- La comparación se calcula contra el período anterior con la misma cantidad de días.
- El COGS utiliza SaleItem.UnitCostAtSale, por lo que los cambios futuros de costo no alteran la rentabilidad histórica.
- Se agregó la pantalla /reports con filtros de fecha y comparación financiera.
- Se agregó Reportes al menú administrativo.
- Se agregó prueba de integración para verificar el uso del costo histórico de venta.
- Para mantener compatibilidad con el proveedor SQLite usado en pruebas, el rango máximo es 367 días y las agregaciones temporales se materializan después de aplicar los filtros de tenant.

### Verification

- dotnet build BillaresPos.slnx --no-restore: passed.
- npm run build: passed.
- dotnet test BillaresPos.slnx --no-restore: 22 passed.
- Prueba runtime contra PostgreSQL: ventas 67000, COGS 17200, ventas completadas 8.
## 2026-09-08 - Auditoría y administración de empleados

- Se agregó la entidad AuditLog con tenant, usuario actor, acción, entidad, valores anterior/nuevo, IP y fecha UTC.
- Se creó y aplicó la migración 20260908191056_Auditing.
- Se agregó GET /api/audit con paginación, protegido para OWNER y ADMINISTRATOR.
- No existe endpoint normal para eliminar auditoría.
- Se agregaron GET /api/users, POST /api/users y PATCH /api/users/{id}/role.
- OWNER puede crear y asignar cualquier rol; ADMINISTRATOR no puede crear ni asignar OWNER.
- Las operaciones USER_CREATED y USER_ROLE_CHANGED generan auditoría dentro de la operación de persistencia.
- Se agregaron las pantallas /employees y /audit.
- Se mantuvo el aislamiento por tenant mediante filtros de EF y validaciones explícitas de TenantId.

### Verification

- dotnet build BillaresPos.slnx --no-restore: passed.
- npm run build: passed.
- dotnet test BillaresPos.slnx --no-restore: 22 passed.
- Prueba runtime PostgreSQL: creación de cajero CASHIER y registro USER_CREATED confirmado.
## 2026-09-08 - Auditoría automática de operaciones financieras

- Se integró auditoría automática en AppDbContext antes de cada persistencia.
- Las auditorías se guardan en la misma transacción de negocio.
- Se cubren las acciones SALE_CREATED, SALE_CANCELLED, PURCHASE_CREATED, INVENTORY_ADJUSTED, PRODUCT_PRICE_CHANGED, EXPENSE_CREATED, CASH_SHIFT_OPENED, CASH_SHIFT_CLOSED, BILLIARD_SESSION_STARTED y BILLIARD_SESSION_CLOSED.
- Se almacenan valores anteriores y nuevos en JSON, usuario actor, tenant, entidad, IP cuando está disponible y timestamp UTC.
- La auditoría no se genera para operaciones sin usuario autenticado, como el seed de desarrollo.
- Se corrigió el recorrido del ChangeTracker para que agregar auditorías no modifique la colección durante la enumeración.
- Se mantuvo compatibilidad con SQLite en pruebas y PostgreSQL local.
- Se agregó prueba de integración para confirmar INVENTORY_ADJUSTED.

### Verification

- dotnet build BillaresPos.slnx --no-restore: passed.
- npm run build: passed.
- dotnet test BillaresPos.slnx --no-build --no-restore: 23 passed.
## 2026-09-08 - Auditoría de autenticación

- Se agregó el evento LOGIN para autenticaciones exitosas.
- El registro incluye tenant, usuario, IP disponible y timestamp UTC.
- Las autenticaciones fallidas no exponen datos sensibles ni crean registros de negocio.

### Verification

- dotnet build BillaresPos.slnx --no-restore: passed.
- dotnet test BillaresPos.slnx --no-build --no-restore: 23 passed.
## 2026-09-08 - Comprobantes internos y reportes PDF

- Se agrego `IPdfService` en Application para ocultar la tecnologia de generacion de documentos.
- Se implemento `QuestPdfService` en Infrastructure con licencia Community configurada.
- Se agrego `GET /api/sales/{id}/receipt`, protegido para CASHIER, ADMINISTRATOR y OWNER.
- El comprobante incluye negocio, numero de venta, fecha local del tenant, cajero, items historicos, descuentos, total y medios de pago.
- Se agrego `GET /api/reports/pdf?from=YYYY-MM-DD&to=YYYY-MM-DD`, protegido para ADMINISTRATOR y OWNER.
- El reporte PDF reutiliza el calculo existente del reporte JSON y contiene comparativo del periodo actual contra el anterior.
- Los PDFs se identifican como documentos internos; no representan facturacion electronica DIAN.
- El frontend agrega descarga de comprobante despues de una venta y descarga de reporte desde `/reports`.
- Se declaro QuestPDF tambien en el proyecto ejecutable de API para asegurar la copia del ensamblado en runtime.
- No se agrego migracion: no hay nuevas entidades ni columnas.
- La generacion es bajo demanda en esta iteracion; la generacion programada/asinćronica con Hangfire queda para la integracion de jobs y notificaciones.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `dotnet test BillaresPos.slnx --no-build --no-restore`: 23 passed.
- `npm run build`: passed.
- Prueba runtime PostgreSQL de comprobante: HTTP 200, `application/pdf`, 42,370 bytes.
- Prueba runtime PostgreSQL de reporte: HTTP 200, `application/pdf`, 36,862 bytes.
## 2026-09-08 - Hangfire para documentos PDF

- Se agregaron Hangfire.AspNetCore 1.8.25 y Hangfire.PostgreSql 1.21.1.
- Hangfire usa PostgreSQL en el esquema `hangfire` y arranca un servidor de workers en la API.
- Se agrego `GeneratedDocument` con `TenantId`, referencia de negocio, estado, nombre, contenido PDF y error de procesamiento.
- Se creo la migracion `20260908195157_GeneratedDocuments`, aplicada a PostgreSQL local.
- Al confirmar una venta se crea el documento `Pending` dentro de la misma transaccion.
- Solo despues del commit se encola `GenerateSaleReceiptAsync`.
- El job genera el comprobante usando datos historicos y lo marca `Generated`; los fallos quedan como `Failed` y Hangfire puede reintentarlos.
- La descarga del comprobante prioriza el PDF persistido y mantiene generacion inmediata como respaldo durante la ventana de procesamiento.
- El panel local de Hangfire queda disponible en `/hangfire` solo en Development.

### Verification

- Hangfire creo correctamente sus tablas en PostgreSQL y arranco sus workers.
- Prueba runtime: una venta genero un documento `Generated` con contenido PDF persistido en `app.generated_documents`.
- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `dotnet test BillaresPos.slnx --no-build --no-restore`: 23 passed.
## 2026-09-08 - Jobs recurrentes de reportes

- Se agrego `GenerateDailyReportsAsync` para crear el reporte del dia anterior por tenant.
- Se agrego `GenerateMonthlyReportsAsync` para crear el reporte del mes anterior comparado contra el mes previo.
- Se agrego `RetryFailedPdfJobsAsync`, limitado a 50 comprobantes fallidos por ejecucion.
- Los reportes recurrentes son idempotentes por tenant, tipo y nombre de archivo.
- El calculo de reportes ahora usa `TenantId` explicito en jobs y conserva el aislamiento multi-tenant.
- Se registraron las tareas recurrentes con zona horaria de Bogota:
  - `reports-daily`: diario a las 02:00.
  - `reports-monthly`: primer dia del mes a las 03:00.
  - `pdf-retry-failed`: cada hora.
- Hangfire se omite en el entorno `Testing`; la cola es opcional en el endpoint de ventas para mantener las pruebas aisladas de infraestructura externa.

### Verification

- El panel `/hangfire/recurring` muestra `reports-daily`, `reports-monthly` y `pdf-retry-failed`.
- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `dotnet test BillaresPos.slnx --no-build --no-restore`: 23 passed.
## 2026-09-08 - Notificaciones internas

- Se agrego la entidad `Notification` con aislamiento por `TenantId`, destinatario, canal, tipo, estado, referencia, timestamps y fecha de lectura.
- Se creo la migracion `20260908200826_Notifications`, aplicada a PostgreSQL local.
- Las ventas completadas y canceladas crean notificaciones `IN_APP` para usuarios con rol `OWNER` dentro del tenant, dentro de la misma transaccion.
- La entrega de la notificacion se ejecuta despues del commit mediante `NotificationJobs` y Hangfire; el job es idempotente y marca `Sent` o `Failed`.
- Se agregaron `GET /api/notifications` y `POST /api/notifications/{id}/read`, filtrados por el usuario autenticado y protegidos por tenant.
- Se agrego la pantalla frontend `/notifications` y su acceso en el menu lateral.
- FCM, WhatsApp y preferencias configurables siguen pendientes; esta iteracion establece el contrato persistido para incorporarlos sin cambiar el flujo de ventas.
- Se corrigio el registro de jobs recurrentes para usar `IRecurringJobManager`, evitando el fallo de arranque de la API cuando Hangfire esta configurado por DI.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed, con advertencias NU1903 preexistentes de dependencias.
- `dotnet test BillaresPos.slnx --no-build --no-restore`: 23 passed.
- `npm run build`: passed.
- Prueba runtime PostgreSQL: una venta creo una notificacion `SaleCompleted` para el propietario y Hangfire la marco `Sent`.
## 2026-09-08 - Preferencias y preparacion de push

- Se agregaron `NotificationPreference` y `PushDeviceToken`, ambos aislados por tenant.
- Se creo la migracion `20260908202140_NotificationPreferencesAndDevices`, aplicada a PostgreSQL local.
- Las preferencias de venta soportan `Never`, `AllSales` y `AboveAmount`, con monto minimo en COP.
- Se agregaron los endpoints `GET/PUT /api/notification-preferences`.
- Se agregaron `POST/DELETE /api/notification-devices`, validando que cada usuario solo administre sus propios dispositivos.
- Se agrego `IPushNotificationService` y `FakePushNotificationService` para desarrollo sin credenciales Firebase.
- Las ventas ahora pueden generar notificaciones `PUSH` cuando existe un dispositivo activo y la preferencia lo permite; el job se ejecuta despues del commit y no bloquea la venta.
- Se agrego configuracion de preferencias en `/notifications`.
- La integracion real con Firebase Cloud Messaging queda pendiente para la siguiente iteracion; no se almacenan secretos ni credenciales en el repositorio.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `dotnet test BillaresPos.slnx --no-build --no-restore`: 23 passed.
- `npm run build`: passed.
- Prueba runtime PostgreSQL: preferencias y dispositivo push registrados correctamente.
## 2026-09-08 - Firebase Cloud Messaging real

- Se agrego el paquete oficial `FirebaseAdmin` 3.6.0 al proyecto Infrastructure.
- Se implemento `FirebasePushNotificationService` usando credenciales ADC mediante `GOOGLE_APPLICATION_CREDENTIALS` y `Firebase:ProjectId`.
- El proveedor envia mensajes FCM por lotes de hasta 500 tokens, con titulo, cuerpo y datos de referencia.
- Los errores de entrega hacen fallar el job para que Hangfire aplique reintentos; la notificacion no bloquea la transaccion de negocio.
- La seleccion de proveedor es explicita: `Firebase__Enabled=true` activa Firebase real; cualquier otro valor usa `FakePushNotificationService`.
- Se agregaron variables documentadas en `.env.example`; nunca se incluye una clave JSON ni secretos en el repositorio.
- La configuracion del cliente web y su service worker requieren los valores publicos del proyecto Firebase y quedan como siguiente paso de integracion del frontend.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `dotnet test BillaresPos.slnx --no-build --no-restore`: 23 passed.
- `npm run build`: passed.
- La aplicacion mantiene el proveedor mock por defecto y no requiere credenciales Firebase para iniciar localmente.
## 2026-09-08 - Cliente web FCM y PWA

- Se agrego el SDK web `firebase` al frontend.
- Se creo `firebasePush.ts` para inicializar Firebase solo cuando existen variables `VITE_FIREBASE_*`, solicitar permiso mediante accion explicita y obtener el token FCM.
- Se agrego `frontend/public/firebase-messaging-sw.js` para notificaciones en segundo plano; recibe la configuracion publica por la URL del service worker.
- La pantalla `/notifications` ahora muestra `Activar avisos push` solo cuando Firebase esta configurado y registra el dispositivo en la API.
- Se agrego `frontend/.env.example` con las variables publicas de Firebase y la llave VAPID.
- No se solicita permiso durante el login ni al cargar la aplicacion; el usuario debe activar el canal desde la pantalla de notificaciones.
- Las claves publicas web no sustituyen la credencial administrativa del backend; la cuenta de servicio sigue configurada exclusivamente por ADC en servidor.

### Verification

- `npm run build`: passed, PWA genera `firebase-messaging-sw.js` y service worker de precache.
- `dotnet build BillaresPos.slnx --no-restore`: passed.
- `dotnet test BillaresPos.slnx --no-build --no-restore`: 23 passed.
## 2026-09-08 - Bandeja de notificaciones sin duplicados

- La bandeja API ahora muestra únicamente notificaciones `IN_APP`.
- Las notificaciones `PUSH` siguen persistidas para entrega, reintentos de Hangfire y trazabilidad, pero no se presentan como una segunda venta en la interfaz.
- Se conserva el mismo comportamiento de preferencias y envío FCM.
## 2026-09-08 - Notificaciones push en primer plano

- Se agrego un listener `onMessage` del SDK Firebase para mensajes con la aplicacion abierta.
- El frontend muestra una notificacion del navegador en primer plano y conserva el service worker para segundo plano.
- El comportamiento evita confundir el mensaje push con una segunda notificacion de negocio en la bandeja interna.

### Verification

- `npm run build`: passed.\n- Prueba manual: venta #18 por 60.000 COP recibida como notificacion push en Chrome.
## 2026-09-08 - Base de WhatsApp Cloud API

- Se agrego `IWhatsAppService` al contrato de integraciones.
- Se agrego `FakeWhatsAppService` para desarrollo local sin credenciales.
- Se agrego `MetaWhatsAppService` para enviar plantillas directamente al endpoint Graph de Meta.
- La seleccion se controla con `WhatsApp__Enabled`; por defecto permanece desactivada.
- Se documentaron `WhatsApp__ApiVersion`, `WhatsApp__PhoneNumberId` y `WhatsApp__AccessToken` en `.env.example`.
- El proveedor real valida credenciales y registra errores HTTP para que la capa de jobs pueda aplicar reintentos.
- Todavia no se conectan eventos de negocio a WhatsApp: falta definir numero del propietario, plantillas aprobadas y preferencias de envio.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed, con advertencias existentes de dependencias vulnerables.
- `dotnet test BillaresPos.slnx --no-build --no-restore`: 23 passed.
## 2026-09-08 - Prueba manual de WhatsApp Cloud API

- Se creo correctamente la app de Meta y el portafolio comercial `Billares POS`.
- Se obtuvo un numero de prueba de WhatsApp y se registro un destinatario de prueba.
- La plantilla de prueba `Order confirmed` fue recibida correctamente en WhatsApp.
- El token temporal fue regenerado despues de exponerse accidentalmente en una captura; el token anterior debe considerarse revocado/comprometido.
- La integracion del POS con eventos de venta y plantillas propias queda como siguiente paso.
## 2026-09-09 - WhatsApp dinamico conectado al flujo de ventas

- `IWhatsAppService` ahora recibe un `WhatsAppMessage` dinamico con tipo `TEMPLATE` o `TEXT`, destinatario, plantilla, idioma, parametros y cuerpo de texto.
- `FakeWhatsAppService` permanece como proveedor local por defecto.
- `MetaWhatsAppService` envia plantillas o texto directamente a Graph API, segun el tipo de mensaje.
- Las preferencias del propietario ahora incluyen `WhatsAppEnabled` y `WhatsAppPhoneNumber`.
- Las ventas y cancelaciones generan notificaciones WhatsApp cuando el propietario lo habilita y tiene numero configurado.
- `NotificationJobs` envia WhatsApp de forma asincrona y conserva estados para reintentos.
- Las plantillas se seleccionan por configuracion: `WhatsApp:Templates:SaleCompleted` y `WhatsApp:Templates:SaleCancelled`.
- Se creo y aplico la migracion `20260909041002_WhatsAppPreferences`.

### Verification

- `dotnet build BillaresPos.slnx --no-restore`: passed, con advertencias existentes de dependencias vulnerables.
- `dotnet test BillaresPos.slnx --no-build --no-restore`: 23 passed.
- `npm run build`: passed.## 2026-09-09 - Mensaje WhatsApp con datos reales de venta

- Se agrego `WhatsApp:MessageType` para elegir entre `Template` y `Text`.
- Para la prueba local se configuro `Text`, enviando el cuerpo real de la notificacion, por ejemplo `Billares POS - Venta #23 por 55.000 COP.`.
- Se verifico que el envio de WhatsApp ya responde correctamente despues de renovar el token de Meta.
- Las plantillas de Meta quedan reservadas para el flujo de produccion, porque requieren aprobacion y parametros definidos.