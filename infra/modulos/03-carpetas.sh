#!/usr/bin/env bash
# 03 - Carpetas departamentales con permisos POSIX + ACL
#
#  Matriz de acceso (rwx = lectura/escritura, r-x = solo lectura, --- = sin acceso)
#                  gerencia  contabilidad  ventas  ti   empleados  otros
#   gerencia/        rwx         ---         ---   ---     ---      ---
#   contabilidad/    r-x         rwx         ---   ---     ---      ---
#   ventas/          r-x         r-x         rwx   ---     ---      ---
#   ti/              ---         ---         ---   rwx     ---      ---
#   compartido/      rwx         rwx         rwx   rwx     rwx      ---
source "$(dirname "$0")/lib.sh"

R="$RAIZ_EMPRESA"

paso "Raiz $R (solo empleados pueden entrar)"
install -d -m 0750 -o root -g empleados "$R"
setfacl -m g:ti:r-x "$R"

# carpeta <ruta> <grupo dueño> <modo> [entradas ACL de solo lectura...]
carpeta() {
  local dir="$1" grupo="$2" modo="$3"; shift 3
  install -d -o root -g "$grupo" "$dir"
  chmod "$modo" "$dir"
  setfacl -b "$dir"
  # grupo dueño con rwx tambien para archivos nuevos (ACL por defecto)
  setfacl -m "g:$grupo:rwx,o::---" -m "d:g:$grupo:rwx,d:o::---,d:u::rwx" "$dir"
  local lector
  for lector in "$@"; do
    setfacl -m "g:$lector:r-x" -m "d:g:$lector:r-x" "$dir"
  done
}

paso "Carpetas departamentales"
carpeta "$R/gerencia"     gerencia     2770
carpeta "$R/contabilidad" contabilidad 2770 gerencia
carpeta "$R/ventas"       ventas       2770 gerencia contabilidad
carpeta "$R/ti"           ti           2770
carpeta "$R/compartido"   empleados    3770     # 3 = setgid + sticky (nadie borra lo ajeno)

install -d -o root -g ti -m 2770 "$DIR_INCIDENTES" "$DIR_INFORMES"

paso "Archivos empresariales de ejemplo (solo si no existen)"
copiar_ejemplo() {   # copiar_ejemplo <archivo en datos-ejemplo/> <carpeta destino>
  local origen="$BASE/datos-ejemplo/$1" destino="$2/$(basename "$1")"
  if [[ ! -e $destino ]]; then
    install -m 0660 -o root -g "$(stat -c %G "$2")" "$origen" "$destino"
    echo "     + $destino"
  fi
}
copiar_ejemplo contabilidad/balance_general_2026.csv  "$R/contabilidad"
copiar_ejemplo contabilidad/nomina_octubre_2026.csv   "$R/contabilidad"
copiar_ejemplo contabilidad/cuentas_por_pagar.csv     "$R/contabilidad"
copiar_ejemplo ventas/clientes.csv                    "$R/ventas"
copiar_ejemplo ventas/ventas_octubre_2026.csv         "$R/ventas"
copiar_ejemplo gerencia/plan_estrategico_2027.txt     "$R/gerencia"
copiar_ejemplo ti/inventario_equipos.csv              "$R/ti"
copiar_ejemplo compartido/politica_seguridad.txt      "$R/compartido"

# Reaplica la ACL efectiva sobre archivos existentes (la mascara debe permitir rw)
for d in gerencia contabilidad ventas ti compartido; do
  find "$R/$d" -type f -exec chmod g+rw,o-rwx {} +
done
for lector in gerencia; do setfacl -R -m "g:$lector:r-X" "$R/contabilidad" "$R/ventas"; done
setfacl -R -m "g:contabilidad:r-X" "$R/ventas"

paso "Estado final"
ls -la "$R"
getfacl -p "$R/contabilidad" 2>/dev/null | sed 's/^/     /'
