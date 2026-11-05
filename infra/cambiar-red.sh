#!/usr/bin/env bash
# =====================================================================
#  GestiOpera - Cambiar la red local del servidor
#
#  Uso:  sudo bash cambiar-red.sh --dhcp <IP-pc-admin>          # la red asigna la IP (universidad, hotspot)
#        sudo bash cambiar-red.sh <IP-servidor>[/prefijo] <IP-pc-admin>   # IP fija
#        sudo bash cambiar-red.sh 192.168.20.50 192.168.20.18      # ejemplo red de casa /24
#        sudo bash cambiar-red.sh --hostonly                       # volver a 192.168.56.10
#
#  Actualiza LAN_IP, LAN_PREFIJO, LAN_RED, LAN_REVERSA, LAN_MODO y
#  CLIENTE_ADMIN_IP en config.sh y vuelve a ejecutar los modulos que
#  dependen de la IP: 01 red, 05 firewall, 07 DNS y 12 aplicacion.
#  Antes, en VirtualBox, el adaptador 2 de la VM debe estar en el modo
#  correspondiente (Adaptador puente o Solo anfitrion).
# =====================================================================
set -Eeuo pipefail
[[ $EUID -eq 0 ]] || { echo "Ejecute: sudo bash $0 $*" >&2; exit 1; }
BASE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONF="$BASE/config.sh"
# shellcheck source=config.sh
source "$CONF"
uso() { sed -n '3,15p' "$0" | sed 's/^# \{0,1\}//'; exit 1; }

es_ip() { [[ $1 =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]]; }
a_entero() { local a b c d; IFS=. read -r a b c d <<< "$1"; echo $(( (a << 24) | (b << 16) | (c << 8) | d )); }
a_ip() { local n=$1; echo "$(( (n >> 24) & 255 )).$(( (n >> 16) & 255 )).$(( (n >> 8) & 255 )).$(( n & 255 ))"; }
mascara() { echo $(( (0xFFFFFFFF << (32 - $1)) & 0xFFFFFFFF )); }

MODO="estatica"
case "${1:-}" in
  --hostonly) IP="192.168.56.10"; PREFIJO=24; CLIENTE="192.168.56.1" ;;
  --dhcp)
    MODO="dhcp"; CLIENTE="${2:-}"; es_ip "$CLIENTE" || uso
    echo "Solicitando IP por DHCP en $LAN_IF ..."
    tmp="$(mktemp)"
    cat > "$tmp" <<EOF
network:
  version: 2
  ethernets:
    $LAN_IF:
      dhcp4: true
      dhcp4-overrides: { use-routes: false, use-dns: false }
      dhcp6: false
EOF
    install -m 0600 "$tmp" /etc/netplan/60-gestiopera-lan.yaml; rm -f "$tmp"
    netplan apply
    actual=""
    for _ in $(seq 1 30); do
      actual="$(ip -4 -o addr show dev "$LAN_IF" scope global | awk '{print $4}' | head -1)"
      [[ -n $actual ]] && break
      sleep 1
    done
    if [[ -z $actual ]]; then
      echo "ERROR: la red no entrego una IP a la VM en 30 s." >&2
      echo "Puede que la red bloquee equipos virtuales (comun en redes de universidad)." >&2
      echo "Use el hotspot de un celular o vuelva a Host-Only:  sudo bash $0 --hostonly" >&2
      exit 1
    fi
    IP="${actual%/*}"; PREFIJO="${actual#*/}"
    echo "La red asigno: $IP/$PREFIJO"
    ;;
  *)
    [[ $# -eq 2 ]] || uso
    IP="${1%/*}"; PREFIJO=24; [[ $1 == */* ]] && PREFIJO="${1#*/}"; CLIENTE="$2"
    es_ip "$IP" && es_ip "$CLIENTE" && [[ $PREFIJO =~ ^[0-9]+$ ]] && (( PREFIJO >= 16 && PREFIJO <= 30 )) || uso
    ;;
esac

M="$(mascara "$PREFIJO")"
RED="$(a_ip $(( $(a_entero "$IP") & M )))/$PREFIJO"
if (( ( $(a_entero "$CLIENTE") & M ) != ( $(a_entero "$IP") & M ) )); then
  echo "ERROR: el PC administrador ($CLIENTE) no esta en la red del servidor ($RED)" >&2; exit 1
fi
IFS=. read -r o1 o2 o3 _ <<< "$IP"
REVERSA="$o3.$o2.$o1"

echo "Nueva red: servidor=$IP/$PREFIJO  red=$RED  modo=$MODO  pc-admin=$CLIENTE"
cp -a "$CONF" "$CONF.anterior"
grep -q '^LAN_MODO=' "$CONF" || sed -i '/^LAN_PREFIJO=/a LAN_MODO="estatica"          # estatica | dhcp' "$CONF"
sed -i -E \
  -e "s|^LAN_IP=.*|LAN_IP=\"$IP\"|" \
  -e "s|^LAN_PREFIJO=.*|LAN_PREFIJO=\"$PREFIJO\"|" \
  -e "s|^LAN_MODO=.*|LAN_MODO=\"$MODO\"          # estatica \| dhcp|" \
  -e "s|^LAN_RED=.*|LAN_RED=\"$RED\"|" \
  -e "s|^LAN_REVERSA=.*|LAN_REVERSA=\"$REVERSA\"          # zona inversa in-addr.arpa|" \
  -e "s|^CLIENTE_ADMIN_IP=.*|CLIENTE_ADMIN_IP=\"$CLIENTE\"   # PC Windows (equipo cliente de TI)|" \
  "$CONF"
grep -E '^(LAN_IP|LAN_PREFIJO|LAN_MODO|LAN_RED|LAN_REVERSA|CLIENTE_ADMIN_IP)=' "$CONF"

exec bash "$BASE/instalar.sh" 01 05 07 12
