# GestiOpera - Verificacion del equipo cliente (PC Windows en la red local 192.168.56.0/24)
# Uso:  powershell -ExecutionPolicy Bypass -File .\probar-cliente.ps1 [-Servidor 192.168.20.50]
param([string]$Servidor = '192.168.56.10', [string]$Dominio = 'gestiopera.local')

function ANumero([string]$ip) {
    $b = ([Net.IPAddress]$ip).GetAddressBytes()
    ([uint64]$b[0] * 16777216) + ([uint64]$b[1] * 65536) + ([uint64]$b[2] * 256) + [uint64]$b[3]
}
function MismaRed([string]$a, [string]$b, [int]$prefijo) {
    $bloque = [uint64][math]::Pow(2, 32 - $prefijo)      # tamano de la red
    [math]::Floor((ANumero $a) / $bloque) -eq [math]::Floor((ANumero $b) / $bloque)
}

function Resultado($nombre, $ok, $detalle) {
    $estado = if ($ok) { 'OK   ' } else { 'FALLO' }
    $color  = if ($ok) { 'Green' } else { 'Red' }
    Write-Host ("[{0}] {1,-38} {2}" -f $estado, $nombre, $detalle) -ForegroundColor $color
}

Write-Host "`nGestiOpera - prueba del equipo cliente $env:COMPUTERNAME`n" -ForegroundColor Cyan

$ip = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '169.254.*' -and (MismaRed $_.IPAddress $Servidor $_.PrefixLength) } | Select-Object -First 1
Resultado 'IP del cliente en la red del servidor' ($null -ne $ip) $(if ($ip) { "$($ip.IPAddress)/$($ip.PrefixLength)" } else { 'este equipo no esta en la red del servidor' })

$ping = Test-Connection -ComputerName $Servidor -Count 2 -Quiet
Resultado "Ping al servidor $Servidor" $ping ''

foreach ($nombre in "servidor.$Dominio", "app.$Dominio", "pc-admin.$Dominio") {
    try {
        $r = Resolve-DnsName -Name $nombre -Server $Servidor -DnsOnly -ErrorAction Stop | Where-Object { $_.IPAddress } | Select-Object -First 1
        Resultado "DNS $nombre" $true $r.IPAddress
    } catch { Resultado "DNS $nombre" $false $_.Exception.Message }
}

foreach ($puerto in 22, 53, 80) {
    $t = Test-NetConnection -ComputerName $Servidor -Port $puerto -WarningAction SilentlyContinue
    Resultado "Puerto TCP $puerto" $t.TcpTestSucceeded ''
}

try {
    $web = Invoke-WebRequest -Uri "http://$Servidor/" -UseBasicParsing -TimeoutSec 10
    Resultado 'Aplicacion web GestiOpera' ($web.StatusCode -eq 200) "http://servidor.$Dominio (HTTP $($web.StatusCode))"
} catch { Resultado 'Aplicacion web GestiOpera' $false $_.Exception.Message }

Write-Host "`nPara usar los nombres DNS en el navegador, configure el DNS del adaptador" -ForegroundColor Yellow
Write-Host "de red (Wi-Fi o Host-Only) con el servidor $Servidor (sufijo $Dominio)." -ForegroundColor Yellow
