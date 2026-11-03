#!/usr/bin/env bash
# 06 - Fail2ban: bloqueo automatico de IPs que fuerzan el SSH
source "$(dirname "$0")/lib.sh"

paso "Carcel sshd (3 intentos en 10 min => bloqueo 15 min)"
plantilla jail-gestiopera.local /etc/fail2ban/jail.d/gestiopera.local 0644

fail2ban-client -t >/dev/null
systemctl enable fail2ban >/dev/null 2>&1
systemctl restart fail2ban
sleep 3
fail2ban-client status sshd | sed 's/^/     /'
