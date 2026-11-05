# =====================================================================
#  GestiOpera - Parametros de la infraestructura del servidor central
#  Este archivo se instala en /etc/gestiopera/config.sh y lo leen todos
#  los modulos de instalacion y las herramientas gestiopera-*.
# =====================================================================

EMPRESA="GestiOpera"
DOMINIO="gestiopera.local"
SERVIDOR_HOST="servidor"

# Red local (adaptador Host-Only de VirtualBox)
LAN_IF="enp0s8"
LAN_IP="192.168.56.10"
LAN_PREFIJO="24"
LAN_MODO="estatica"          # estatica | dhcp
LAN_RED="192.168.56.0/24"
LAN_REVERSA="56.168.192"          # zona inversa in-addr.arpa
CLIENTE_ADMIN_IP="192.168.56.1"   # PC Windows (equipo cliente de TI)

# Canal de gestion fuera de banda: NAT de VirtualBox (reenvio 127.0.0.1:2222 -> 22)
CANAL_GESTION_IP="10.0.2.2"

ZONA_HORARIA="America/Bogota"
DNS_REENVIADORES="1.1.1.1; 8.8.8.8;"

ADMIN_TI="arlen"

# Rutas
RAIZ_EMPRESA="/srv/empresa"
DIR_BACKUPS="/var/backups/gestiopera"
DIR_LOGS="/var/log/gestiopera"
DIR_APP="/opt/gestiopera"
DIR_INCIDENTES="$RAIZ_EMPRESA/ti/incidentes"
DIR_INFORMES="$RAIZ_EMPRESA/ti/informes"

# Copias de seguridad
RETENCION_DIAS=14

# Aplicacion GestiOpera (Docker)
APP_PROYECTO="gestiopera"
APP_CONTENEDOR_BD="gestiopera-postgres"
APP_BD="billares_pos"
APP_BD_USUARIO="billares_pos"

# Departamentos (grupo -> carpeta en $RAIZ_EMPRESA)
DEPARTAMENTOS=(gerencia contabilidad ventas ti)

# Usuarios de la empresa:  usuario:grupo_principal:Nombre - Cargo
USUARIOS=(
  "gerente1:gerencia:Carolina Gomez - Gerente General"
  "contador1:contabilidad:Luis Perez - Contador"
  "auxcontable1:contabilidad:Diana Rios - Auxiliar Contable"
  "vendedor1:ventas:Juan Rodriguez - Asesor Comercial"
  "vendedor2:ventas:Ana Martinez - Asesora Comercial"
)
