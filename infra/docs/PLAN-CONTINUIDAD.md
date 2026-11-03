# Plan de continuidad y recuperación ante desastres

## 1. Qué se respalda

| Componente | Archivo en la copia | Contenido |
|---|---|---|
| Información empresarial | `empresa.tar.gz` | `/srv/empresa` completo con dueños, permisos, **ACL** y atributos |
| Configuración del servidor | `configuracion.tar.gz` | SSH, UFW, fail2ban, bind9, auditd, netplan, sudoers, cron, cuentas y grupos, `.env` de la app |
| Base de datos de la app | `basedatos.sql.gz` | `pg_dump` de PostgreSQL (ventas, inventario, caja, usuarios del POS) |
| Control | `SHA256SUMS`, `MANIFIESTO-empresa.txt`, `INFO.txt` | Huellas de integridad, lista de archivos y datos de la copia |

Ubicación: `/var/backups/gestiopera/gestiopera-AAAAMMDD-HHMMSS/` (solo root, permisos 700).
El enlace `ultimo` apunta a la copia más reciente.

## 2. Política

| Parámetro | Valor |
|---|---|
| Frecuencia | Diaria a la 01:30 (cron) y bajo demanda (`sudo gestiopera-backup`) |
| Retención | 14 días (`RETENCION_DIAS` en `config.sh`) |
| RPO (pérdida máxima de datos) | 24 horas |
| RTO (tiempo de recuperación) | Archivo: < 5 min · Servidor completo: < 1 hora |
| Verificación | SHA-256 al crear la copia y otra vez antes de cada restauración |
| Supervisión | `gestiopera-monitor` alerta si la última copia tiene más de 26 h |
| Auditoría | auditd vigila `/var/backups/gestiopera` (clave `backups`); cada restauración queda en `/var/log/gestiopera/backup.log` y syslog |

Recomendación 3-2-1 para producción: copiar diariamente `ultimo/` a un segundo disco o a un
almacenamiento externo cifrado (por ejemplo `rsync` a un NAS o `rclone` a la nube).

## 3. Procedimientos de recuperación

**Un archivo borrado o dañado**
```bash
sudo gestiopera-restaurar buscar balance_general
sudo gestiopera-restaurar archivo /srv/empresa/contabilidad/balance_general_2026.csv
# desde una copia concreta y sin sobrescribir:
sudo gestiopera-restaurar archivo /srv/empresa/contabilidad/balance_general_2026.csv gestiopera-20261103-153000 --destino /tmp/recuperado
```
Si el archivo existe, la versión actual se conserva como `*.antes-restauracion-<fecha>`.

**Una carpeta completa** (p. ej. tras un ransomware en Ventas)
```bash
sudo gestiopera-restaurar carpeta /srv/empresa/ventas
```

**Base de datos de la aplicación**
```bash
sudo gestiopera-restaurar bd            # pide escribir SI
```

**Pérdida total del servidor**
1. Instalar Ubuntu Server 24.04 y el usuario administrador `arlen`.
2. Copiar `infra/` del repositorio y la última carpeta de copia al nuevo servidor.
3. `sudo bash instalar.sh` (recrea red, usuarios, servicios, seguridad y la app).
4. `sudo mkdir -p /var/backups/gestiopera && sudo cp -a gestiopera-AAAAMMDD-HHMMSS /var/backups/gestiopera/ && sudo ln -sfn /var/backups/gestiopera/gestiopera-AAAAMMDD-HHMMSS /var/backups/gestiopera/ultimo`
5. `sudo gestiopera-restaurar carpeta /srv/empresa` y `sudo gestiopera-restaurar bd --si`
6. Verificar con `sudo gestiopera-estado` y con `probar-cliente.ps1` desde un equipo cliente.

## 4. Pruebas periódicas

Una vez al mes se restaura un archivo de prueba en `/tmp` y se compara su SHA-256 con el
manifiesto, y se deja constancia con `gestiopera-incidente nota` o en el informe mensual de TI.
