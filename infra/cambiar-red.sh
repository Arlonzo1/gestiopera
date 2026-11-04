#!/usr/bin/env bash
# =====================================================================
#  GestiOpera - Cambiar la red local del servidor
#
#  Uso:  sudo bash cambiar-red.sh <IP-servidor> <IP-pc-admin>
#        sudo bash cambiar-red.sh 192.168.20.50 192.168.20.18   # Wi-Fi (adaptador puente)
#        sudo bash cambiar-red.sh --hostonly                     # volver a 192.168.56.10
#
#  Reescribe LAN_IP, LAN_RED, LAN_REVERSA y CLIENTE_ADMIN_IP en config.sh
#  (red /24) y vuelve a ejecutar los modulos que dependen de la IP:
#  01 red, 05 firewall, 07 DNS y 12 aplicacion.
#  Antes, en VirtualBox, el adaptador 2 de la VM debe estar en el modo
#  correspondiente (Adaptador puente o Solo anfitrion).
# =====================================================================
set -Eeuo pipefail
[[ $EUID -eq 0 ]] || { echo "Ejecute: sudo bash $0 $*" >&2; exit 1; }
BASE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONF="$BASE/config.sh"

if [[ ${1:-} == --hostonly ]]; then
  set -- 192.168.56.10 192.168.56.1
fi
IP="${1:-}"; CLIENTE="${2:-}"
valida() { [[ $1 =~ ^([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})$ ]]; }
valida "$IP" && valida "$CLIENTE" || { sed -n '3,14p' "$0" | sed 's/^# \{0,1\}//'; exit 1; }

IFS=. read -r a b c d <<< "$IP"
RED="$a.$b.$c.0/24"
REVERSA="$c.$b.$a"
[[ ${CLIENTE%.*} == "$a.$b.$c" ]] || { echo "El PC administrador ($CLIENTE) debe estar en la misma red $RED" >&2; exit 1; }

echo "Nueva red: servidor=$IP  red=$RED  inversa=$REVERSA.in-addr.arpa  pc-admin=$CLIENTE"
cp -a "$CONF" "$CONF.anterior"
sed -i -E \
  -e "s|^LAN_IP=.*|LAN_IP=\"$IP\"|" \
  -e "s|^LAN_RED=.*|LAN_RED=\"$RED\"|" \
  -e "s|^LAN_REVERSA=.*|LAN_REVERSA=\"$REVERSA\"          # zona inversa in-addr.arpa|" \
  -e "s|^CLIENTE_ADMIN_IP=.*|CLIENTE_ADMIN_IP=\"$CLIENTE\"   # PC Windows (equipo cliente de TI)|" \
  "$CONF"
grep -E '^(LAN_IP|LAN_RED|LAN_REVERSA|CLIENTE_ADMIN_IP)=' "$CONF"


exec bash "$BASE/instalar.sh" 01 05 07 12
