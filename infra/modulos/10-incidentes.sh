#!/usr/bin/env bash
# 10 - Registro de incidentes: plantilla y carga del incidente historico INC-2026-001
source "$(dirname "$0")/lib.sh"

paso "Plantilla de incidentes"
install -m 0640 -o root -g ti "$BASE/archivos/plantilla-incidente.md" "$DIR_INCIDENTES/PLANTILLA.md"

paso "Incidente historico INC-2026-001 (intento de acceso SSH fallido del 14/10/2026)"
INC="$DIR_INCIDENTES/INC-2026-001"
if [[ ! -d $INC ]]; then
  install -d -m 2770 -o root -g ti "$INC" "$INC/evidencias"
  install -m 0640 -o root -g ti "$BASE/archivos/INC-2026-001.md" "$INC/informe.md"
  ORIGEN="/home/$ADMIN_TI/evidencias_incidente_ssh"
  if [[ -d $ORIGEN ]]; then
    cp -p "$ORIGEN"/* "$INC/evidencias/"
    chgrp -R ti "$INC/evidencias"; chmod 0440 "$INC/evidencias"/*
    ( cd "$INC/evidencias" && sha256sum -c SHA256SUMS.txt ) | sed 's/^/     /' || true
  fi
fi
ls -la "$DIR_INCIDENTES" | sed 's/^/     /'
