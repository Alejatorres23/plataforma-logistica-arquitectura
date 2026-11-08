# Demostración del flujo "asignar una entrega" contra la API en ejecución.
# 1) En una terminal:  dotnet run --project src/Logistica.Entregas.Api
# 2) En otra terminal: ./scripts/demo.ps1
# Usa el simulador local del servicio de turnos (solo en el entorno Development).

$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5080'
$headers = @{ 'X-Api-Key' = 'clave-local-solo-desarrollo'; 'X-Correlation-Id' = "demo-$(Get-Random -Maximum 9999)" }

# Repartidores sembrados por el simulador
$ana    = '11111111-1111-1111-1111-111111111111'   # activa, 2 de 5
$luis   = '22222222-2222-2222-2222-222222222222'   # activo, 1 de 5
$carlos = '33333333-3333-3333-3333-333333333333'   # saturado, 5 de 5

# Funciona en Windows PowerShell 5.1 y en PowerShell 7 (no usa -SkipHttpErrorCheck).
function Call($method, $path, $body) {
    $params = @{ Method = $method; Uri = "$api$path"; Headers = $headers; UseBasicParsing = $true }
    if ($body) {
        $params.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 5))
        $params.ContentType = 'application/json; charset=utf-8'
    }
    try {
        $r = Invoke-WebRequest @params
        $status = [int]$r.StatusCode
        $content = $r.Content
    } catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { throw }
        $status = [int]$resp.StatusCode
        $content = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else {
            (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() }
    }
    Write-Host ("  -> HTTP {0}" -f $status) -ForegroundColor Yellow
    if ($content) { Write-Host "  $content"; return $content | ConvertFrom-Json }
}

function Step($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }

Step 'Salud de la API'
Call GET '/health' | Out-Null

Step '1. Llega un pedido confirmado: se crea la entrega'
$now = [DateTimeOffset]::UtcNow
$created = Call POST '/api/v1/deliveries' @{
    orderId = [guid]::NewGuid().ToString(); address = 'Carrera 50 # 10-30, Medellín'
    windowStart = $now.AddHours(1).ToString('o'); windowEnd = $now.AddHours(3).ToString('o'); priority = 'Normal'
}
$id = $created.deliveryId

Step '2. Intento asignar a Carlos (saturado) -> 422 REPARTIDOR_SATURADO'
Call POST "/api/v1/deliveries/$id/assignment" @{ courierId = $carlos } | Out-Null

Step '3. Asigno a Ana -> 200 Asignada'
Call POST "/api/v1/deliveries/$id/assignment" @{ courierId = $ana } | Out-Null

Step '4. Intento asignarla otra vez a Luis -> 409 ENTREGA_YA_ASIGNADA (invariante I1)'
Call POST "/api/v1/deliveries/$id/assignment" @{ courierId = $luis } | Out-Null

Step '5. Tormenta: reasigno a Luis con motivo -> 200'
Call POST "/api/v1/deliveries/$id/reassignment" @{ courierId = $luis; motivo = 'Tormenta: vía cerrada' } | Out-Null

Step '6. Consulto la entrega (estado e historial, sin dirección)'
Call GET "/api/v1/deliveries/$id" | Out-Null

Step '7. Simulo la caída del servicio de turnos y pruebo asignar otra entrega -> 503'
Call POST '/simulador/fallas' @{ servicioTurnos = $true } | Out-Null
$other = Call POST '/api/v1/deliveries' @{
    orderId = [guid]::NewGuid().ToString(); address = 'Calle 10 # 43-20'; windowStart = $now.AddHours(1).ToString('o'); windowEnd = $now.AddHours(2).ToString('o')
}
Call POST "/api/v1/deliveries/$($other.deliveryId)/assignment" @{ courierId = $ana } | Out-Null
Call POST '/simulador/fallas' @{ servicioTurnos = $false } | Out-Null

Step '8. Sin clave de API -> 401'
$headers.Remove('X-Api-Key')
Call GET "/api/v1/deliveries/$id" | Out-Null

Write-Host "`nListo. Revise en la consola de la API los logs con el CorrelationId $($headers['X-Correlation-Id'])." -ForegroundColor Green
