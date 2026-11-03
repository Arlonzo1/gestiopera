# GestiOpera - Simulacion controlada de fuerza bruta contra el SSH del servidor (laboratorio propio)
# Genera intentos con usuarios inexistentes para que aparezcan en los registros y fail2ban
# bloquee la IP del cliente durante 15 minutos (solo el puerto 22; la web sigue disponible).
#
# Uso:  powershell -ExecutionPolicy Bypass -File .\simular-ataque.ps1
# Desbloquear despues (en el servidor):  sudo fail2ban-client set sshd unbanip 192.168.56.1
param([string]$Servidor = '192.168.56.10', [int]$Intentos = 5)

$usuarios = 'admin', 'root', 'oracle', 'contabilidad', 'soporte', 'test', 'backup'
Write-Host "Simulando $Intentos intentos de acceso SSH contra $Servidor ..." -ForegroundColor Yellow
for ($i = 1; $i -le $Intentos; $i++) {
    $u = $usuarios[($i - 1) % $usuarios.Count]
    Write-Host ("  intento {0}: {1}@{2}" -f $i, $u, $Servidor)
    & ssh -o BatchMode=yes -o ConnectTimeout=5 -o StrictHostKeyChecking=no -o UserKnownHostsFile=NUL "$u@$Servidor" exit 2>&1 |
        ForEach-Object { "      $_" }
    Start-Sleep -Milliseconds 700
}
Write-Host "`nComprobando si el servidor bloqueo esta IP (fail2ban)..." -ForegroundColor Yellow
$t = Test-NetConnection -ComputerName $Servidor -Port 22 -WarningAction SilentlyContinue
if ($t.TcpTestSucceeded) {
    Write-Host '  El puerto 22 aun responde (el bloqueo puede tardar unos segundos).' -ForegroundColor Yellow
} else {
    Write-Host '  BLOQUEADO: el servidor rechaza las conexiones SSH de este equipo.' -ForegroundColor Red
}
Write-Host "`nEn el servidor ejecute:  sudo gestiopera-eventos --desde hora" -ForegroundColor Cyan
