#!/usr/bin/env bash
# =====================================================================
#  GestiOpera - Instalador de la infraestructura del servidor central
#
#  Uso:  sudo bash instalar.sh              # todos los modulos
#        sudo bash instalar.sh 07 08        # solo los modulos 07 y 08
#
#  Es idempotente: se puede volver a ejecutar sin romper lo existente.
#  Bitacora completa: /var/log/gestiopera/instalacion.log
# =====================================================================
set -Eeuo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Este instalador debe ejecutarse como administrador:  sudo bash $0 $*" >&2
  exit 1
fi

BASE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export BASE
export DEBIAN_FRONTEND=noninteractive
# shellcheck source=config.sh
source "$BASE/config.sh"

# Normaliza finales de linea por si los archivos vienen de Windows
find "$BASE" -type f \( -name '*.sh' -o -path '*/herramientas/*' -o -path '*/archivos/*' -o -path '*/datos-ejemplo/*' \) \
  -exec sed -i 's/\r$//' {} +

install -d -m 2750 -o root -g adm "$DIR_LOGS"
LOG="$DIR_LOGS/instalacion.log"
touch "$LOG"; chown root:adm "$LOG"; chmod 640 "$LOG"
exec > >(tee -a "$LOG") 2>&1

echo
echo "#################################################################"
echo "#  Instalacion GestiOpera - $(date '+%F %T %Z')  host=$(hostname)"
echo "#################################################################"

mapfile -t TODOS < <(find "$BASE/modulos" -maxdepth 1 -name '[0-9][0-9]-*.sh' -printf '%f\n' | sort)
SELECCION=()
if [[ $# -eq 0 ]]; then
  SELECCION=("${TODOS[@]}")
else
  for arg in "$@"; do
    for m in "${TODOS[@]}"; do [[ $m == "$arg"* ]] && SELECCION+=("$m"); done
  done
fi

for m in "${SELECCION[@]}"; do
  echo
  echo "=================== [$(date '+%T')] $m ==================="
  if bash "$BASE/modulos/$m"; then
    echo "[OK] $m"
  else
    echo "[FALLO] $m  (revise $LOG)"
    exit 1
  fi
done

echo
echo "Instalacion finalizada $(date '+%F %T'). Resumen:  sudo gestiopera-estado"
