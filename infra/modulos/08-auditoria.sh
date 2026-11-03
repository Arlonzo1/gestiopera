#!/usr/bin/env bash
# 08 - Auditoria de accesos con auditd
source "$(dirname "$0")/lib.sh"

install -d -m 0700 -o root -g root "$DIR_BACKUPS"   # debe existir para vigilarla

paso "Reglas de auditoria /etc/audit/rules.d/50-gestiopera.rules"
plantilla audit-gestiopera.rules /etc/audit/rules.d/50-gestiopera.rules 0640

paso "Retencion y permisos de lectura para el grupo adm (equipo de TI)"
CONF=/etc/audit/auditd.conf
sed -i -E 's/^log_group *=.*/log_group = adm/; s/^max_log_file *=.*/max_log_file = 20/; s/^num_logs *=.*/num_logs = 10/; s/^max_log_file_action *=.*/max_log_file_action = ROTATE/' "$CONF"
grep -E '^(log_group|max_log_file|num_logs|max_log_file_action) ' "$CONF" | sed 's/^/     /'
chgrp adm /var/log/audit
chmod 0750 /var/log/audit
chgrp adm /var/log/audit/audit.log* 2>/dev/null || true
chmod 0640 /var/log/audit/audit.log* 2>/dev/null || true

paso "Cargando reglas"
if [[ $(auditctl -s | awk '/^enabled/{print $2}') == 2 ]]; then
  echo "     AVISO: las reglas estan bloqueadas (-e 2); se aplicaran tras reiniciar"
fi
augenrules --load || echo "     AVISO: augenrules devolvio advertencias (reglas duplicadas de otros archivos)"
pkill -HUP -x auditd || true     # recarga auditd.conf sin detener el servicio
systemctl enable auditd >/dev/null 2>&1 || true

paso "Reglas activas de GestiOpera"
auditctl -l | grep -E 'key=(acceso_|cambios_|identidad|sudoers|ssh_config|comandos_usuarios|auditoria_config|backups)' | sed 's/^/     /' || true
