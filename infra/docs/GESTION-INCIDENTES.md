# Procedimiento de gestión de incidentes

Basado en el ciclo de NIST SP 800-61: preparación → detección y análisis → contención,
erradicación y recuperación → actividades posteriores.

## 1. Detección

| Fuente | Dónde |
|---|---|
| Monitor automático (cada 5 min) | `/var/log/gestiopera/alertas.log`, syslog con etiqueta `gestiopera-alerta` |
| Informe diario de eventos | `/srv/empresa/ti/informes/eventos-*.txt` (06:00) |
| Bloqueos de fail2ban | `sudo fail2ban-client status sshd`, `journalctl -u fail2ban` |
| Autenticaciones | `/var/log/auth.log`, `journalctl -u ssh`, `/var/log/sudo.log` |
| Auditoría de archivos y comandos | `sudo ausearch -k <clave> -i` |
| DNS | `/var/log/named/consultas.log` |

Revisión manual en cualquier momento: `sudo gestiopera-eventos --desde hoy`.

## 2. Clasificación

| Severidad | Ejemplo | Tiempo de respuesta |
|---|---|---|
| Baja | Un intento de acceso fallido aislado | 72 h |
| Media | Varios intentos fallidos; acceso denegado repetido a otra área | 24 h |
| Alta | Fuerza bruta, cuenta comprometida, borrado de información | 4 h |
| Crítica | Servicio caído, ransomware, fuga de información confidencial | Inmediato |

## 3. Registro y seguimiento

```bash
sudo gestiopera-incidente nuevo "Fuerza bruta SSH desde 192.168.56.1" Alta
sudo gestiopera-incidente nota INC-2026-002 "IP bloqueada por fail2ban y verificada en UFW"
sudo gestiopera-incidente cerrar INC-2026-002 "Origen identificado como prueba controlada"
sudo gestiopera-incidente listar
```

`nuevo` crea `/srv/empresa/ti/incidentes/<ID>/` con:
- `informe.md`: plantilla con descripción, evidencias, análisis, contención, erradicación, lecciones y bitácora.
- `evidencias/`: informe de eventos, journal de ssh y fail2ban, accesos denegados de auditd,
  últimos accesos y conexiones activas, todo de solo lectura y con `SHA256SUMS.txt` para la
  cadena de custodia.

## 4. Acciones de contención habituales

| Situación | Acción |
|---|---|
| IP atacante | Bloqueo automático de fail2ban; permanente: `sudo ufw insert 1 deny from <IP>` |
| Cuenta comprometida | `sudo usermod -L <usuario>` y `sudo pkill -KILL -u <usuario>` |
| Archivo alterado o borrado | `sudo gestiopera-restaurar archivo <ruta>` |
| Cambio no autorizado de configuración | `sudo ausearch -k ssh_config -i` / `-k sudoers`, restaurar desde `configuracion.tar.gz` |

## 5. Incidentes registrados

| ID | Fecha | Severidad | Resumen | Estado |
|---|---|---|---|---|
| INC-2026-001 | 2026-10-14 | Media | Contraseña incorrecta en SSH sobre la cuenta administradora desde 192.168.20.21 | Cerrado |
| INC-2026-002 | 2026-11-03 | Alta | Fuerza bruta SSH simulada desde el cliente 192.168.56.1 (admin, root, oracle); bloqueada por fail2ban | Abierto (prueba controlada) |
