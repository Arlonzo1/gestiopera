#!/usr/bin/env bash
# 05 - Cortafuegos UFW: denegar todo lo entrante salvo los servicios de la empresa
source "$(dirname "$0")/lib.sh"

paso "Reglas anteriores (quedan respaldadas por ufw en /etc/ufw/*.rules.<fecha>)"
ufw status numbered | sed 's/^/     /' || true

paso "Politica nueva"
ufw --force reset >/dev/null
ufw default deny incoming
ufw default allow outgoing
ufw logging low

# Canal de gestion (NAT VirtualBox): primera regla para no bloquear al administrador
ufw allow from "$CANAL_GESTION_IP" to any port 22 proto tcp comment 'SSH gestion (NAT VirtualBox)'
# SSH desde cualquier origen con limite de conexiones (6 en 30 s)
ufw limit 22/tcp comment 'SSH con limite anti fuerza bruta'
# DNS y aplicacion web solo para la red local de la empresa
ufw allow from "$LAN_RED" to any port 53 comment 'DNS LAN'
ufw allow from "$LAN_RED" to any port 80 proto tcp comment 'GestiOpera web LAN'

ufw --force enable
ufw status verbose | sed 's/^/     /'
