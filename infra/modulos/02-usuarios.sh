#!/usr/bin/env bash
# 02 - Usuarios y grupos de la empresa
source "$(dirname "$0")/lib.sh"

CRED=/root/gestiopera-credenciales.txt

paso "Grupos: ${DEPARTAMENTOS[*]} empleados sshusuarios"
for g in "${DEPARTAMENTOS[@]}" empleados sshusuarios; do
  groupadd -f "$g"
done

paso "Administrador de TI ($ADMIN_TI)"
usermod -aG ti,empleados,sshusuarios,adm,sudo "$ADMIN_TI"

# Cuenta preexistente del laboratorio: conserva el acceso SSH (no es empleado)
if id estudiante >/dev/null 2>&1; then
  usermod -aG sshusuarios estudiante
fi

generar_clave() {
  # 10 caracteres aleatorios + sufijo que garantiza mayuscula, digito y simbolo
  local r
  r="$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | cut -c1-10)"
  echo "${r}-Go$(( RANDOM % 90 + 10 ))"
}

if [[ ! -f $CRED ]]; then
  install -m 600 /dev/null "$CRED"
  {
    echo "# GestiOpera - Credenciales iniciales (solo root). Generado $(date '+%F %T')"
    echo "# usuario | grupo | contrasena inicial"
  } >> "$CRED"
fi

paso "Usuarios de la empresa"
for linea in "${USUARIOS[@]}"; do
  IFS=: read -r u grupo nombre <<< "$linea"
  if id "$u" >/dev/null 2>&1; then
    usermod -g "$grupo" -aG empleados,sshusuarios -c "$nombre" "$u"
    echo "     $u ya existe (grupos actualizados)"
  else
    useradd -m -s /bin/bash -c "$nombre" -g "$grupo" -G empleados,sshusuarios "$u"
    clave="$(generar_clave)"
    echo "$u:$clave" | chpasswd
    echo "$u | $grupo | $clave" >> "$CRED"
    echo "     $u creado (grupo $grupo)"
  fi
  chmod 700 "/home/$u"   # el grupo principal es el departamento: el home es privado
  chage -M 90 -m 1 -W 7 "$u"
done

paso "Registro de comandos sudo en /var/log/sudo.log"
touch /var/log/sudo.log; chown root:adm /var/log/sudo.log; chmod 640 /var/log/sudo.log
tmp="$(mktemp)"
cat > "$tmp" <<'EOF'
# GestiOpera - auditoria de sudo
Defaults logfile="/var/log/sudo.log"
Defaults log_year
Defaults lecture="always"
Defaults passwd_tries=3
Defaults badpass_message="Contrasena incorrecta. El intento queda registrado."
EOF
if visudo -cf "$tmp" >/dev/null; then
  install -m 0440 "$tmp" /etc/sudoers.d/10-gestiopera-auditoria
else
  echo "     ERROR: la configuracion de sudo no es valida; no se instala" >&2
fi
rm -f "$tmp"

paso "Resumen de grupos"
for g in "${DEPARTAMENTOS[@]}" empleados sshusuarios; do
  gid="$(getent group "$g" | cut -d: -f3)"
  miembros="$( { getent group "$g" | cut -d: -f4 | tr ',' '\n'; awk -F: -v g="$gid" '$4==g{print $1}' /etc/passwd; } | sed '/^$/d' | sort -u | paste -sd, )"
  printf '     %-13s %s\n' "$g" "$miembros"
done
echo "     Credenciales iniciales: sudo cat $CRED"
