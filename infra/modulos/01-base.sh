#!/usr/bin/env bash
# 01 - Base del servidor: red LAN, zona horaria, paquetes, politicas generales
source "$(dirname "$0")/lib.sh"

paso "Configuracion central en /etc/gestiopera"
install -d -m 0755 /etc/gestiopera
install -m 0644 "$BASE/config.sh" /etc/gestiopera/config.sh

paso "Zona horaria $ZONA_HORARIA"
timedatectl set-timezone "$ZONA_HORARIA"
timedatectl set-ntp true || true

paso "Red local: $LAN_IF = $LAN_IP/$LAN_PREFIJO (modo ${LAN_MODO:-estatica})"
if [[ ${LAN_MODO:-estatica} == dhcp ]]; then
  plantilla netplan-lan-dhcp.yaml /etc/netplan/60-gestiopera-lan.yaml 0600
else
  plantilla netplan-lan.yaml /etc/netplan/60-gestiopera-lan.yaml 0600
fi
netplan generate
netplan apply
sleep 3
ip -br addr show "$LAN_IF" || true

paso "Nombre del servidor en /etc/hosts"
sed -i "/[[:space:]]$SERVIDOR_HOST\.$DOMINIO/d" /etc/hosts
echo "$LAN_IP	$SERVIDOR_HOST.$DOMINIO $SERVIDOR_HOST" >> /etc/hosts

paso "Instalando paquetes (puede tardar unos minutos)"
esperar_apt
apt-get update -q -o DPkg::Lock::Timeout=600
esperar_apt
apt-get install "${APT_OPC[@]}" \
  acl tree jq curl rsync sysstat htop unattended-upgrades \
  auditd libpam-pwquality \
  fail2ban python3-systemd \
  bind9 bind9-utils bind9-dnsutils

paso "Actualizaciones de seguridad automaticas"
cat > /etc/apt/apt.conf.d/20auto-upgrades <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
APT::Periodic::AutocleanInterval "7";
EOF

paso "Politica de contrasenas (pwquality + caducidad)"
plantilla pwquality.conf /etc/security/pwquality.conf.d/60-gestiopera.conf 0644
sed -i -E 's/^PASS_MAX_DAYS.*/PASS_MAX_DAYS\t90/; s/^PASS_MIN_DAYS.*/PASS_MIN_DAYS\t1/; s/^PASS_WARN_AGE.*/PASS_WARN_AGE\t7/' /etc/login.defs
grep -E '^PASS_(MAX|MIN|WARN)' /etc/login.defs

paso "Aviso legal de acceso (banner)"
plantilla banner.txt /etc/issue.net 0644

paso "Estadisticas de rendimiento (sysstat / sar)"
sed -i 's/^ENABLED=.*/ENABLED="true"/' /etc/default/sysstat
systemctl enable --now sysstat >/dev/null 2>&1 || true

paso "Swap de 1 GB (estabilidad al compilar la aplicacion)"
if ! swapon --show | grep -q /swapfile; then
  if [[ ! -f /swapfile ]]; then
    fallocate -l 1G /swapfile
    chmod 600 /swapfile
    mkswap /swapfile >/dev/null
  fi
  swapon /swapfile
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi
free -h | sed -n '1,3p'
