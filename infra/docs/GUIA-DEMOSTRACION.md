# Guía de demostración – 4 pruebas en vivo

Preparación (5 minutos antes):

1. Encender la VM `Ubuntus_Server` en VirtualBox.
2. En el PC Windows (equipo cliente) abrir PowerShell en la carpeta del proyecto y comprobar la red:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\infra\cliente-windows\probar-cliente.ps1
   ```
3. Entrar al servidor como administrador:
   ```powershell
   ssh arlen@192.168.56.10
   ```
4. Mostrar el panel general: `sudo gestiopera-estado`

Se puede seguir el guion automático (`sudo gestiopera-demo`) o hacer cada paso a mano como se
describe abajo. Hacerlo a mano demuestra mejor que la solución funciona.

---

## Prueba 1 – Seguridad: identificar un evento sospechoso

**Generar el evento** desde el cliente Windows (otra ventana de PowerShell):

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\cliente-windows\simular-ataque.ps1
```
(o a mano: `ssh intruso@192.168.56.10` y escribir contraseñas incorrectas 3 veces).

**Identificarlo** en el servidor:

```bash
sudo journalctl -u ssh --since "-15 min" | grep -E "Invalid user|Failed password|not allowed"
sudo grep -E "Invalid user|Failed password|not allowed" /var/log/auth.log | tail
sudo fail2ban-client status sshd            # la IP 192.168.56.1 aparece bloqueada
sudo gestiopera-eventos --desde hora        # informe con veredicto
```

Qué explicar: origen (IP), usuarios probados, cantidad de intentos, bloqueo automático por
fail2ban (3 intentos en 10 min → 15 min de bloqueo, solo el puerto 22) y alerta del monitor
en `/var/log/gestiopera/alertas.log`.

Registrar el incidente y desbloquear:

```bash
sudo gestiopera-incidente nuevo "Fuerza bruta SSH desde 192.168.56.1" Alta
sudo gestiopera-incidente listar
sudo fail2ban-client set sshd unbanip 192.168.56.1
```

## Prueba 2 – Continuidad: recuperar un archivo empresarial

```bash
F=/srv/empresa/contabilidad/balance_general_2026.csv
sudo sha256sum $F                       # huella original
sudo gestiopera-backup                  # copia de seguridad
sudo gestiopera-restaurar listar
sudo rm $F                              # DESASTRE: borrado accidental / ransomware
sudo ls -l /srv/empresa/contabilidad/
sudo gestiopera-restaurar archivo $F    # verifica SHA-256 de la copia y restaura
sudo sha256sum $F                       # misma huella => archivo íntegro
sudo getfacl $F                         # permisos y ACL recuperados
```

## Prueba 3 – Control de acceso: Ventas no entra a Contabilidad

Desde el cliente Windows, iniciar sesión como un empleado de Ventas (contraseña en
`/root/gestiopera-credenciales.txt`):

```powershell
ssh vendedor1@192.168.56.10
```
```bash
id                                                    # grupo ventas
ls /srv/empresa/contabilidad                          # Permiso denegado
cat /srv/empresa/contabilidad/balance_general_2026.csv   # Permiso denegado
cp /srv/empresa/contabilidad/nomina_octubre_2026.csv /tmp # Permiso denegado
ls /srv/empresa/ventas                                # su carpeta: OK
exit
```

Comparar con el contador (sí puede) y con el gerente (solo lectura):

```bash
sudo -u contador1 head -3 /srv/empresa/contabilidad/balance_general_2026.csv
sudo -u gerente1 touch /srv/empresa/contabilidad/x.txt   # denegado: solo lectura
sudo getfacl /srv/empresa/contabilidad
```

## Prueba 4 – Auditoría: actividades de un usuario

```bash
sudo gestiopera-auditoria vendedor1 --desde hoy --guardar
```

El informe muestra: ficha de la cuenta y carpetas a las que puede entrar, sesiones (`last`),
autenticaciones SSH, uso de sudo, **comandos ejecutados**, accesos a información sensible
y **los intentos denegados a Contabilidad de la prueba 3**, con fecha y hora.
Consulta directa de auditd:

```bash
sudo ausearch -k acceso_denegado --start today --format text | grep vendedor1
sudo ausearch -k comandos_usuarios -ul $(id -u vendedor1) --start today --format text
sudo aureport -au -i --start today          # autenticaciones
ls -l /srv/empresa/ti/informes/             # informes guardados + SHA256SUMS
```
