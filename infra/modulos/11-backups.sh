#!/usr/bin/env bash
# 10 - Copias de seguridad y recuperacion
source "$(dirname "$0")/lib.sh"

paso "Repositorio de copias $DIR_BACKUPS (solo root)"
install -d -m 0700 -o root -g root "$DIR_BACKUPS"

paso "Herramientas gestiopera-backup y gestiopera-restaurar"
instalar_herramienta gestiopera-backup
instalar_herramienta gestiopera-restaurar

paso "Primera copia de seguridad"
/usr/local/sbin/gestiopera-backup
/usr/local/sbin/gestiopera-restaurar listar
