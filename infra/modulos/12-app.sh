#!/usr/bin/env bash
# 11 - Despliegue de la aplicacion GestiOpera (Docker: PostgreSQL + API .NET + frontend)
source "$(dirname "$0")/lib.sh"

FUENTE="$BASE/app/gestiopera-src.tar.gz"
APP="$DIR_APP/app"

if [[ ! -f $FUENTE ]]; then
  echo "     No se encontro $FUENTE; se omite el despliegue de la aplicacion."
  exit 0
fi

paso "Docker"
if ! command -v docker >/dev/null 2>&1 || ! docker compose version >/dev/null 2>&1; then
  esperar_apt
  apt-get install "${APT_OPC[@]}" docker.io docker-compose-v2
fi
systemctl enable --now docker >/dev/null 2>&1
docker --version; docker compose version

paso "Codigo fuente en $APP"
install -d -m 0750 -o root -g ti "$DIR_APP"
rm -rf "$APP.nuevo"; install -d "$APP.nuevo"
tar -xzf "$FUENTE" -C "$APP.nuevo"
[[ -f $APP/.env ]] && cp -a "$APP/.env" "$APP.nuevo/.env"
rm -rf "$APP"; mv "$APP.nuevo" "$APP"
plantilla docker-compose.servidor.yml "$APP/docker-compose.servidor.yml" 0640 root ti

if [[ ! -f $APP/.env ]]; then
  paso "Secretos nuevos y aleatorios en $APP/.env (solo root)"
  BD_CLAVE="$(openssl rand -hex 24)"
  JWT_CLAVE="$(openssl rand -base64 48 | tr -d '\n')"
  umask 077
  cat > "$APP/.env" <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
POSTGRES_DB=$APP_BD
POSTGRES_USER=$APP_BD_USUARIO
POSTGRES_PASSWORD=$BD_CLAVE
Jwt__Issuer=BillaresPos
Jwt__Audience=BillaresPos.Api
Jwt__SigningKey=$JWT_CLAVE
Jwt__AccessTokenMinutes=15
Jwt__RefreshTokenDays=14
Firebase__Enabled=false
WhatsApp__Enabled=false
Email__Enabled=false
Database__ApplyMigrations=true
Database__SeedDemoData=true
EOF
  umask 022
fi
chmod 600 "$APP/.env"

paso "Liberando el puerto 80 para la aplicacion"
if systemctl is-active --quiet apache2 2>/dev/null; then
  echo "     Apache2 (pagina por defecto) ocupa el puerto 80: se detiene y deshabilita (no se desinstala)"
  systemctl disable --now apache2
fi

paso "Construyendo y levantando contenedores (10-20 minutos la primera vez)"
cd "$APP"
docker compose -p "$APP_PROYECTO" -f docker-compose.servidor.yml up -d --build
docker builder prune -af >/dev/null 2>&1 || true

paso "Esperando a la aplicacion"
for i in $(seq 1 60); do
  if curl -fsS -m 5 "http://127.0.0.1/" >/dev/null 2>&1 && \
     docker exec gestiopera-api wget -qO- http://127.0.0.1:8080/health >/dev/null 2>&1; then
    echo "     Aplicacion disponible en http://$SERVIDOR_HOST.$DOMINIO  (http://$LAN_IP)"
    break
  fi
  sleep 5
done
docker compose -p "$APP_PROYECTO" -f docker-compose.servidor.yml ps

paso "Copia de seguridad con la base de datos incluida"
/usr/local/sbin/gestiopera-backup || true
