#!/usr/bin/env bash
# 09 - Herramientas de monitoreo, deteccion, auditoria e incidentes
source "$(dirname "$0")/lib.sh"

paso "Herramientas en /usr/local/sbin"
for h in "$BASE"/herramientas/gestiopera-*; do
  instalar_herramienta "$(basename "$h")"
  echo "     $(basename "$h")"
done

paso "Tareas programadas /etc/cron.d/gestiopera"
plantilla cron-gestiopera /etc/cron.d/gestiopera 0644

paso "Rotacion de bitacoras /etc/logrotate.d/gestiopera"
plantilla logrotate-gestiopera /etc/logrotate.d/gestiopera 0644
logrotate -d /etc/logrotate.d/gestiopera >/dev/null 2>&1 || echo "     AVISO: revisar logrotate"

paso "Primera medicion"
/usr/local/sbin/gestiopera-monitor || true
tail -n 1 "$DIR_LOGS/monitor.log" | sed 's/^/     /'
