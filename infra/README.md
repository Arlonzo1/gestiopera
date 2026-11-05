# GestiOpera – Infraestructura del servidor central

Infraestructura como código para el servidor **Ubuntu Server 24.04** que aloja la
empresa GestiOpera (POS para billares). Todo se instala con un solo comando y se puede
volver a ejecutar sin romper lo existente.

```bash
sudo bash ~/gestiopera-infra/instalar.sh          # todo
sudo bash ~/gestiopera-infra/instalar.sh 07 08    # solo DNS y auditoría
```

Bitácora de la instalación: `/var/log/gestiopera/instalacion.log`.

## 1. Arquitectura

```
                 Internet
                    │
          NAT VirtualBox (10.0.2.0/24) ── canal de gestión: 127.0.0.1:2222 → 22
                    │ enp0s3 10.0.2.15
        ┌───────────┴────────────────────────────────────────────┐
        │  servidor.gestiopera.local  (Ubuntu Server 24.04)      │
        │  ─ SSH endurecido + fail2ban + UFW                     │
        │  ─ DNS bind9 (zona gestiopera.local + inversa)         │
        │  ─ auditd + sudo.log + journal  → informes             │
        │  ─ /srv/empresa (carpetas por departamento + ACL)      │
        │  ─ Docker: GestiOpera (nginx + API .NET + PostgreSQL)  │
        │  ─ Copias diarias en /var/backups/gestiopera           │
        └───────────┬────────────────────────────────────────────┘
                    │ enp0s8 192.168.56.10/24
        Red local de la empresa (Host-Only 192.168.56.0/24)
                    │
          pc-admin.gestiopera.local 192.168.56.1 (Windows 11, equipo cliente)
```

| Equipo | IP | Rol |
|---|---|---|
| `servidor.gestiopera.local` | 192.168.56.10 | Servidor central (SSH, DNS, archivos, app, backups, auditoría) |
| `pc-admin.gestiopera.local` | 192.168.56.1 | Equipo cliente Windows de la LAN |

## 2. Requisitos del proyecto y cómo se cumplen

| Requisito | Implementación | Módulo |
|---|---|---|
| Servidor central Ubuntu Server | Ubuntu 24.04, zona horaria America/Bogota, IP fija en la LAN, swap, actualizaciones automáticas | `01-base.sh` |
| Equipos clientes en red local | Adaptador Host-Only 192.168.56.0/24; PC Windows como cliente (`cliente-windows/probar-cliente.ps1`) | `01-base.sh` |
| Usuarios, grupos y permisos | 4 departamentos + `empleados` + `sshusuarios`, 5 usuarios, política de contraseñas, permisos POSIX + ACL | `02`, `03` |
| SSH | Solo grupo `sshusuarios`, sin root, 3 intentos, banner legal, `LogLevel VERBOSE` | `04-ssh.sh` |
| DNS (opcional) | bind9 autoritativo para `gestiopera.local` + zona inversa, reenvío recursivo solo para la LAN, registro de consultas | `07-dns.sh` |
| Seguridad y monitoreo | UFW (denegar por defecto), fail2ban, monitor cada 5 min con alertas, sysstat, `gestiopera-estado`, `gestiopera-eventos` | `05`, `06`, `09` |
| Gestión de incidentes | `gestiopera-incidente` (nuevo/nota/cerrar) con recolección automática de evidencias + SHA-256; INC-2026-001 documentado | `10-incidentes.sh` |
| Backups y recuperación | `gestiopera-backup` diario (archivos + ACL, configuración, PostgreSQL), verificación SHA-256, retención de 14 días, `gestiopera-restaurar` | `11-backups.sh` |
| Auditoría e informes | auditd (lecturas de Contabilidad y Gerencia, accesos denegados, comandos, cambios de identidad), `gestiopera-auditoria` | `08-auditoria.sh` |
| Aplicación de la empresa | GestiOpera en Docker publicada en `http://servidor.gestiopera.local` | `12-app.sh` |

## 3. Usuarios, grupos y matriz de acceso

| Usuario | Grupo principal | Cargo |
|---|---|---|
| `gerente1` | gerencia | Gerente General |
| `contador1` | contabilidad | Contador |
| `auxcontable1` | contabilidad | Auxiliar Contable |
| `vendedor1` | ventas | Asesor Comercial |
| `vendedor2` | ventas | Asesora Comercial |
| `arlen` | (ti, sudo, adm) | Administrador de TI |

Las contraseñas iniciales son aleatorias y quedan solo en `/root/gestiopera-credenciales.txt`
(`sudo cat /root/gestiopera-credenciales.txt`). Política: mínimo 10 caracteres con mayúscula,
minúscula, dígito y símbolo; vencen a los 90 días.

| Carpeta (`/srv/empresa/…`) | gerencia | contabilidad | ventas | ti | Resto |
|---|---|---|---|---|---|
| `gerencia/` | rwx | — | — | — | — |
| `contabilidad/` | r-x | rwx | **—** | — | — |
| `ventas/` | r-x | r-x | rwx | — | — |
| `ti/` (incidentes, informes) | — | — | — | rwx | — |
| `compartido/` (sticky) | rwx | rwx | rwx | rwx | — |

Las carpetas tienen el bit **setgid** (los archivos nuevos heredan el grupo del departamento)
y **ACL por defecto** (los archivos nuevos heredan los permisos). Los `home` son privados (700).

## 4. Herramientas de administración (`/usr/local/sbin`)

| Comando | Para qué |
|---|---|
| `sudo gestiopera-estado` | Panel: red, servicios, recursos, UFW, fail2ban, app, backups, accesos |
| `sudo gestiopera-eventos [--desde hora\|hoy\|semana] [--guardar]` | Detecta eventos sospechosos (fuerza bruta SSH, usuarios inexistentes, sudo fallido, accesos denegados, cambios de cuentas) |
| `sudo gestiopera-auditoria <usuario> [--desde …] [--guardar]` | Informe de actividades de un usuario |
| `sudo gestiopera-auditoria --general` | Informe general (aureport) |
| `sudo gestiopera-backup` | Copia de seguridad inmediata |
| `sudo gestiopera-restaurar listar\|verificar\|buscar\|archivo\|carpeta\|bd` | Recuperación |
| `sudo gestiopera-incidente nuevo\|listar\|ver\|nota\|cerrar` | Registro de incidentes |
| `sudo gestiopera-monitor` | Chequeo de salud (lo ejecuta cron cada 5 min) |
| `sudo gestiopera-demo [1-4]` | Guion de las 4 pruebas de la sustentación |

Tareas programadas (`/etc/cron.d/gestiopera`): monitor cada 5 min, backup diario 01:30,
informe de eventos diario 06:00, informe de auditoría semanal (lunes 07:00).

## 5. Notas de la instalación en esta VM

- Apache2 (instalado antes, solo con su página por defecto) ocupaba el puerto 80: el módulo
  `12-app.sh` lo **detiene y deshabilita** (no lo desinstala) para publicar GestiOpera.
- La regla de auditd duplicada que avisa `Rule exists` proviene de un archivo de reglas previo
  en `/etc/audit/rules.d/`; no afecta a las reglas de GestiOpera.
- `ausearch` no admite `-i` junto con `--format text`: use uno u otro.
- La app se publica con los datos de demostración del seeder del proyecto
  (`owner@billar-demo.local`); en producción se debe desactivar `Database__SeedDemoData`.
- Docker publica el puerto 80 directamente en iptables (no pasa por UFW); en este diseño solo
  hay acceso desde la LAN Host-Only y el NAT de VirtualBox.

### Varios computadores en la misma red Wi-Fi

Para que otros equipos reales se conecten, el adaptador 2 de la VM debe estar en
**Adaptador puente** sobre el Wi-Fi (la red Host-Only solo la ve el PC anfitrión).
Luego, dentro del servidor, con una IP libre de esa red y la IP del PC anfitrión:

```bash
# red de casa: IP fija libre + IP del PC anfitrion
sudo bash ~/gestiopera-infra/cambiar-red.sh 192.168.20.50 192.168.20.18
# red de universidad u hotspot: la red asigna la IP (acepta redes /22, /23...)
sudo bash ~/gestiopera-infra/cambiar-red.sh --dhcp 20.20.2.111
```

Al cambiar de red (otro Wi-Fi, otro lugar) hay que volver a ejecutarlo: la IP del
servidor cambia. Si una red de universidad aísla a los equipos entre sí, use el
hotspot de un celular.

Los clientes prueban con `probar-cliente.ps1 -Servidor 192.168.20.50` y simulan el
ataque con `simular-ataque.ps1 -Servidor 192.168.20.50`. Para volver a la red
Host-Only: `sudo bash ~/gestiopera-infra/cambiar-red.sh --hostonly`.

## 6. Documentos

- [Guía práctica en Word: servidor, usuarios y las 4 pruebas](docs/Guia_Practica_Servidor_y_Usuarios_GestiOpera.docx)
- [Guion de comandos en Word: las 4 pruebas paso a paso](docs/Guion_Comandos_Pruebas_GestiOpera.docx)
- [Guía de demostración (las 4 pruebas)](docs/GUIA-DEMOSTRACION.md)
- [Plan de continuidad y recuperación ante desastres](docs/PLAN-CONTINUIDAD.md)
- [Procedimiento de gestión de incidentes](docs/GESTION-INCIDENTES.md)

## 7. Estructura del directorio

```
infra/
├── instalar.sh            orquestador (ejecuta modulos/NN-*.sh en orden)
├── config.sh              parámetros (IPs, dominio, usuarios, rutas, retención)
├── cambiar-red.sh         cambia la red del servidor (Wi-Fi puente o Host-Only) en un comando
├── modulos/               01-base … 12-app
├── archivos/              plantillas de configuración (sshd, jail, bind, audit, cron…)
├── herramientas/          comandos gestiopera-* que se instalan en /usr/local/sbin
├── datos-ejemplo/         archivos empresariales de prueba por departamento
├── cliente-windows/       pruebas desde el equipo cliente
├── docs/                  guía de demostración, continuidad e incidentes
└── app/                   (generado) código de la app empaquetado con git archive
```
