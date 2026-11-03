#!/usr/bin/env bash
# 07 - Servidor DNS interno (bind9) para la zona @DOMINIO@
source "$(dirname "$0")/lib.sh"

paso "Opciones y zonas de bind9"
plantilla named.conf.options /etc/bind/named.conf.options 0644 root bind
plantilla named.conf.local   /etc/bind/named.conf.local   0644 root bind
plantilla db.directa         "/etc/bind/db.$DOMINIO"      0644 root bind
plantilla db.inversa         "/etc/bind/db.$LAN_REVERSA"  0644 root bind
install -d -m 0750 -o bind -g adm /var/log/named

paso "Validacion"
named-checkconf
named-checkzone "$DOMINIO" "/etc/bind/db.$DOMINIO"
named-checkzone "$LAN_REVERSA.in-addr.arpa" "/etc/bind/db.$LAN_REVERSA"

systemctl enable named >/dev/null 2>&1
systemctl restart named
sleep 2

paso "Pruebas de resolucion"
for n in "$SERVIDOR_HOST.$DOMINIO" "app.$DOMINIO" "pc-admin.$DOMINIO"; do
  printf '     %-28s -> %s\n' "$n" "$(dig +short @"$LAN_IP" "$n" | tail -1)"
done
printf '     %-28s -> %s\n' "inversa $LAN_IP" "$(dig +short @"$LAN_IP" -x "$LAN_IP")"
printf '     %-28s -> %s\n' "externa ubuntu.com" "$(dig +short @"$LAN_IP" ubuntu.com | head -1)"
