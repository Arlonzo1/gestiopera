# Funciones comunes para los modulos de instalacion (se carga con source)
set -Eeuo pipefail
BASE="${BASE:?BASE no definido}"
# shellcheck source=../config.sh
source "$BASE/config.sh"

paso() { echo "  -> $*"; }

# plantilla <origen en archivos/> <destino> <modo> [usuario] [grupo]
# Copia el archivo reemplazando las variables @NOMBRE@ por su valor.
plantilla() {
  local src="$BASE/archivos/$1" dst="$2" modo="$3" usr="${4:-root}" grp="${5:-root}" tmp
  tmp="$(mktemp)"
  sed -e "s|@DOMINIO@|$DOMINIO|g" \
      -e "s|@SERVIDOR_HOST@|$SERVIDOR_HOST|g" \
      -e "s|@LAN_IF@|$LAN_IF|g" \
      -e "s|@LAN_IP@|$LAN_IP|g" \
      -e "s|@LAN_PREFIJO@|$LAN_PREFIJO|g" \
      -e "s|@LAN_RED@|$LAN_RED|g" \
      -e "s|@LAN_REVERSA@|$LAN_REVERSA|g" \
      -e "s|@CLIENTE_ADMIN_IP@|$CLIENTE_ADMIN_IP|g" \
      -e "s|@CANAL_GESTION_IP@|$CANAL_GESTION_IP|g" \
      -e "s|@DNS_REENVIADORES@|$DNS_REENVIADORES|g" \
      -e "s|@RAIZ_EMPRESA@|$RAIZ_EMPRESA|g" \
      -e "s|@DIR_LOGS@|$DIR_LOGS|g" \
      -e "s|@SERIAL@|$(date +%y%m%d%H%M)|g" \
      "$src" > "$tmp"
  install -D -m "$modo" -o "$usr" -g "$grp" "$tmp" "$dst"
  rm -f "$tmp"
}

# Espera a que apt/dpkg queden libres (al arrancar Ubuntu ejecuta apt-daily/unattended-upgrades)
esperar_apt() {
  local i
  for i in $(seq 1 120); do
    if ! fuser /var/lib/apt/lists/lock /var/lib/dpkg/lock /var/lib/dpkg/lock-frontend >/dev/null 2>&1; then return 0; fi
    (( i == 1 )) && echo "     apt esta ocupado (actualizaciones automaticas); esperando..."
    sleep 5
  done
  echo "     AVISO: apt sigue ocupado tras 10 minutos" >&2
}
APT_OPC=(-y -q -o DPkg::Lock::Timeout=600)

# instalar_herramienta <nombre>  -> /usr/local/sbin/<nombre>
instalar_herramienta() {
  install -m 0750 -o root -g adm "$BASE/herramientas/$1" "/usr/local/sbin/$1"
}
