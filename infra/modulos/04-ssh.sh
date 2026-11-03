#!/usr/bin/env bash
# 04 - Servicio SSH endurecido
source "$(dirname "$0")/lib.sh"

DST=/etc/ssh/sshd_config.d/10-gestiopera.conf

# Seguridad: nunca dejar fuera al administrador
id -nG "$ADMIN_TI" | grep -qw sshusuarios || { echo "ERROR: $ADMIN_TI no esta en sshusuarios" >&2; exit 1; }

paso "Configuracion endurecida en $DST"
[[ -f $DST ]] && cp -a "$DST" "$DST.anterior"
plantilla sshd-gestiopera.conf "$DST" 0644

if sshd -t; then
  paso "Sintaxis valida; reiniciando ssh"
  systemctl restart ssh
else
  echo "ERROR: configuracion SSH invalida, se revierte" >&2
  if [[ -f $DST.anterior ]]; then mv "$DST.anterior" "$DST"; else rm -f "$DST"; fi
  exit 1
fi

paso "Parametros efectivos"
sshd -T | grep -Ei '^(permitrootlogin|maxauthtries|allowgroups|passwordauthentication|pubkeyauthentication|x11forwarding|loglevel|banner|logingracetime) ' | sed 's/^/     /'
systemctl is-active ssh
