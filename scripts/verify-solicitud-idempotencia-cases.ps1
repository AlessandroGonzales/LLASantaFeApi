# Solo dentro de verify-usuario.ps1 en su PostgreSQL temporal. No envía correos reales.
$idemStart=$script:checks
$idemUsers=@();$idemTokens=@();$idemIds=@()
foreach($number in 1..2){
    $account=@{nombre='Idempotencia';apellido='Prueba';email="idempotencia$number@example.invalid";password=$password;fechaNacimiento='1990-01-01'}
    Call POST '/api/Usuario' $account '' 202 | Out-Null
    Call POST '/api/Usuario/email/confirmar' @{email=$account.email;password=$password;codigo=(CuentaCodigo $account.email 'email_verificacion')} '' 204 | Out-Null
    $token=(Json (Call POST '/api/Usuario/login' @{email=$account.email;password=$password} '' 200)).token
    $idemUsers+=$account;$idemTokens+=$token;$idemIds+=(Json (Call GET '/api/Usuario/me' $null $token 200)).idUsuario
}
$idemBody=@{motivo='proyecto';mensaje="Proyecto de prueba:`nUna propuesta formal para mejorar el espacio público."}
$idemKey=[Guid]::NewGuid().ToString();$idemHeader=@{'Idempotency-Key'=$idemKey}
foreach($idemPath in @('/api/SolicitudCiudadana','/api/Usuario/me/solicitudes','/api/SolicitudCiudadana/{id}/pdf')){
    $idemParameter=@($swagger.paths.$idemPath.post.parameters | Where-Object { $_.name -eq 'Idempotency-Key' -and $_.in -eq 'header' })
    Assert ($idemParameter.Count -eq 1) "Swagger no muestra Idempotency-Key en $idemPath"
}
foreach($badKey in @('','invalid','00000000-0000-0000-0000-000000000000',"$idemKey,$idemKey")){
    Call POST '/api/SolicitudCiudadana' $idemBody $idemTokens[0] 400 @{'Idempotency-Key'=$badKey} | Out-Null
    Call POST '/api/Usuario/me/solicitudes' $idemBody $idemTokens[0] 400 @{'Idempotency-Key'=$badKey} | Out-Null
}
$idemNul=$idemBody.Clone();$idemNul.mensaje='Mensaje con carácter '+[char]0+' inválido.'
Call POST '/api/SolicitudCiudadana' $idemNul $idemTokens[0] 400 $idemHeader | Out-Null
$idemCreated=Call POST '/api/SolicitudCiudadana' $idemBody $idemTokens[0] 201 $idemHeader
$idemFirst=(Json $idemCreated).id
Assert ($idemCreated.Headers.ContainsKey('Location')) 'Alta de solicitud sin Location.'
$idemReplay=Call POST '/api/SolicitudCiudadana' $idemBody $idemTokens[0] 200 $idemHeader
Assert ($idemReplay.Content -eq $idemCreated.Content) 'Reintento devuelve otro acuse o ID.'
Call POST '/api/SolicitudCiudadana' $idemBody $idemTokens[0] 200 @{'Idempotency-Key'=$idemKey.ToUpperInvariant()} | Out-Null
Assert ($idemReplay.Headers['Cache-Control'] -match 'no-store') 'Acuse idempotente en caché pública.'
$idemTrim=$idemBody.Clone();$idemTrim.mensaje='  '+$idemBody.mensaje+'  '
Assert ((Json (Call POST '/api/Usuario/me/solicitudes' $idemTrim $idemTokens[0] 200 $idemHeader)).id -eq $idemFirst) 'Alias o espacios exteriores crean otra solicitud.'
$idemDifferent=$idemBody.Clone();$idemDifferent.motivo='duda'
$idemConflict=Call POST '/api/SolicitudCiudadana' $idemDifferent $idemTokens[0] 409 $idemHeader
Assert ((Json $idemConflict).code -eq 'idempotency_conflict') 'No se informa conflicto de clave para React.'
Assert ((Sql "SELECT count(*) FROM public.solicitudes_ciudadanas WHERE usuario_id='$($idemIds[0])'") -eq '1') 'Repetir clave o cambiar datos consume cupo.'
$idemRaceKey=[Guid]::NewGuid().ToString()
ConcurrentPost '/api/SolicitudCiudadana' $idemBody $idemTokens[0] '200,201' @{'Idempotency-Key'=$idemRaceKey}
$idemSecond=(Json (Call POST '/api/Usuario/me/solicitudes' $idemBody $idemTokens[0] 200 @{'Idempotency-Key'=$idemRaceKey})).id
$idemThird=(Json (Call POST '/api/SolicitudCiudadana' $idemBody $idemTokens[0] 201 @{'Idempotency-Key'=[Guid]::NewGuid().ToString()})).id
Call POST '/api/SolicitudCiudadana' $idemBody $idemTokens[0] 429 @{'Idempotency-Key'=[Guid]::NewGuid().ToString()} | Out-Null
Call POST '/api/SolicitudCiudadana' $idemBody $idemTokens[0] 200 $idemHeader | Out-Null
Assert ((Sql "SELECT count(*) FROM public.solicitudes_ciudadanas WHERE usuario_id='$($idemIds[0])'") -eq '3') 'Idempotencia o carrera modifica cuota de tres altas.'
$idemOther=(Json (Call POST '/api/SolicitudCiudadana' $idemBody $idemTokens[1] 201 $idemHeader)).id
Assert ($idemOther -ne $idemFirst) 'La clave de otro usuario revela o reutiliza su solicitud.'
Call GET "/api/SolicitudCiudadana/$idemFirst" $null $idemTokens[1] 404 | Out-Null

$idemPdfA=TestPdf 'IDEMPOTENCY_A';$idemPdfB=TestPdf 'IDEMPOTENCY_B'
$idemPdfKey=[Guid]::NewGuid().ToString();$idemFailedKey=[Guid]::NewGuid().ToString()
$idemScan=$null
function StartIdemScanner {
    $script:idemScan=Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @('-NoProfile','-File',(Join-Path $PSScriptRoot 'test-clamd-protocol.ps1')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root '.artifacts/idem-clamd.stdout.log') -RedirectStandardError (Join-Path $root '.artifacts/idem-clamd.stderr.log')
    # Esperar al listener sin abrir una conexión incompleta al protocolo simulado.
    for($try=0;$try -lt 40;$try++){
        if($script:idemScan.HasExited){throw 'El scanner simulado no inició.'}
        if([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port -contains 13310){return}
        Start-Sleep -Milliseconds 50
    }
    throw 'El scanner simulado no respondió.'
}
try {
    StartIdemScanner
    PdfCall $idemFirst $idemTokens[0] $idemPdfA 400 -key 'invalid'
    PdfCall $idemFirst $idemTokens[0] $idemPdfA 201 -key $idemPdfKey
    Assert ((Sql "SELECT count(*) FROM public.solicitud_pdf_intentos WHERE usuario_id='$($idemIds[0])'") -eq '1') 'Clave inválida consumió intento PDF.'
    Stop-Process -Id $idemScan.Id;$idemScan.WaitForExit()
    # Antivirus detenido: un reintento confirmado debe verificar bytes sin volver a analizar.
    PdfCall $idemFirst $idemTokens[0] $idemPdfA 200 -key $idemPdfKey
    PdfCall $idemFirst $idemTokens[0] $idemPdfB 409 -key $idemPdfKey
    PdfCall $idemFirst $idemTokens[0] $idemPdfA 409 -key ([Guid]::NewGuid().ToString())
    PdfCall $idemFirst $idemTokens[1] $idemPdfA 404 -key $idemPdfKey
    $idemDetail=Json (Call GET "/api/SolicitudCiudadana/$idemFirst" $null $idemTokens[0] 200)
    $idemSha=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($idemPdfA))
    Assert ($idemDetail.estadoPdf -eq 'disponible' -and $idemDetail.pdfBytes -eq $idemPdfA.Length -and $idemDetail.pdfSha256 -eq $idemSha) 'Faltan metadatos para recuperar el resultado de una carga.'
    Assert ($null -eq $idemDetail.contenido -and $null -eq $idemDetail.pdfUrl) 'Detalle contiene bytes o enlaces públicos.'
    Assert ((Sql "SELECT count(*) FROM public.solicitud_pdf_intentos WHERE usuario_id='$($idemIds[0])'") -eq '1') 'Repetir PDF confirmado consume intentos.'
    PdfCall $idemSecond $idemTokens[0] $idemPdfB 503 -key $idemFailedKey
    $idemWithoutPdf=Json (Call GET "/api/SolicitudCiudadana/$idemSecond" $null $idemTokens[0] 200)
    Assert ($idemWithoutPdf.estadoPdf -eq 'sin_adjunto' -and $idemWithoutPdf.mensaje -eq $idemBody.mensaje -and $null -eq $idemWithoutPdf.pdfSha256) 'Fallo de antivirus pierde texto o publica adjunto.'
    Assert ((Sql "SELECT count(*) FROM public.solicitud_pdf_intentos WHERE usuario_id='$($idemIds[0])' AND NOT usado") -eq '0') 'Reserva fallida bloquea el reintento hasta caducar.'
    StartIdemScanner
    PdfCall $idemSecond $idemTokens[0] $idemPdfB 201 -key $idemFailedKey
    PdfCall $idemSecond $idemTokens[0] $idemPdfB 200 -key $idemFailedKey
    PdfCall $idemThird $idemTokens[0] (TestPdf 'IDEMPOTENCY_C') 429 -key ([Guid]::NewGuid().ToString())
    PdfCall $idemFirst $idemTokens[0] $idemPdfA 200 -key $idemPdfKey
    Assert ((Sql "SELECT count(*) FROM public.solicitud_pdf_intentos WHERE usuario_id='$($idemIds[0])'") -eq '3') 'Reintento exitoso no respeta cuota de intentos.'
    $idemBusyKey=[Guid]::NewGuid().ToString()
    $idemReservation=Sql "SELECT `"Intento`" FROM public.reservar_solicitud_pdf('$idemOther','$($idemIds[1])','$idemBusyKey')"
    $idemBusy=PdfCall $idemOther $idemTokens[1] $idemPdfA 409 -key $idemBusyKey -inspect $true
    Assert ($idemBusy.RetryAfter -eq '30' -and ($idemBusy.Body|ConvertFrom-Json).code -eq 'upload_in_progress') 'Carga en curso sin espera o código específico.'
    Assert ((Sql "SELECT count(*) FROM public.solicitud_pdf_intentos WHERE usuario_id='$($idemIds[1])'") -eq '1') 'Carga concurrente con misma clave consume otro intento.'
    Sql "UPDATE public.solicitud_pdf_intentos SET usado=true WHERE id='$idemReservation'" | Out-Null
    PdfCall $idemOther $idemTokens[1] $idemPdfA 201 -key $idemBusyKey
    $idemDuplicate=(Json (Call POST '/api/SolicitudCiudadana' $idemBody $idemTokens[1] 201 @{'Idempotency-Key'=[Guid]::NewGuid().ToString()})).id
    PdfCall $idemDuplicate $idemTokens[1] $idemPdfA 409 -key ([Guid]::NewGuid().ToString())
    Assert ((Sql "SELECT count(*) FROM public.solicitud_pdf WHERE solicitud_id='$idemDuplicate'") -eq '0') 'Hash repetido genera otro archivo del mismo usuario.'
    Call GET "/api/SolicitudCiudadana/$idemDuplicate" $null $idemTokens[1] 200 | Out-Null
} finally {
    if($idemScan -and -not $idemScan.HasExited){Stop-Process -Id $idemScan.Id;$idemScan.WaitForExit()}
}

# Un fallo al insertar revierte la operación: la misma clave puede confirmar después.
$idemRollbackKey=[Guid]::NewGuid().ToString()
$idemRollbackBody=@{motivo='duda';mensaje='Rollback de prueba de solicitud idempotente.'}
Sql @'
CREATE FUNCTION public.idempotency_test_fail() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.mensaje='Rollback de prueba de solicitud idempotente.' THEN
  RAISE EXCEPTION 'Simulated storage failure' USING ERRCODE='P0503';
 END IF;
 RETURN NEW;
END; $$;
CREATE TRIGGER idem_test_fail BEFORE INSERT ON public.solicitudes_ciudadanas
 FOR EACH ROW EXECUTE FUNCTION public.idempotency_test_fail();
'@ | Out-Null
try {
    Call POST '/api/SolicitudCiudadana' $idemRollbackBody $idemTokens[1] 503 @{'Idempotency-Key'=$idemRollbackKey} | Out-Null
    Assert ((Sql "SELECT count(*) FROM public.solicitudes_ciudadanas WHERE usuario_id='$($idemIds[1])'") -eq '2') 'Un fallo de alta consumió cuota o guardó una fila parcial.'
} finally {
    Sql 'DROP TRIGGER idem_test_fail ON public.solicitudes_ciudadanas; DROP FUNCTION public.idempotency_test_fail();' | Out-Null
}
Call POST '/api/SolicitudCiudadana' $idemRollbackBody $idemTokens[1] 201 @{'Idempotency-Key'=$idemRollbackKey} | Out-Null
Call POST '/api/Usuario/me/solicitudes' $idemRollbackBody $idemTokens[1] 200 @{'Idempotency-Key'=$idemRollbackKey} | Out-Null

# El acuse se conserva aunque la solicitud ya haya sido gestionada.
Call PATCH "/api/SolicitudCiudadana/$idemFirst/gestion" @{revision=1;estado='resuelta';respuesta='Respuesta administrativa de prueba.'} $at 204 | Out-Null
Call POST '/api/SolicitudCiudadana' $idemBody $idemTokens[0] 200 $idemHeader | Out-Null
Assert ((Json (Call GET "/api/SolicitudCiudadana/$idemFirst" $null $idemTokens[0] 200)).estado -eq 'resuelta') 'El reintento reabre el expediente.'
PdfCall $idemFirst $idemTokens[0] $idemPdfA 200 -key $idemPdfKey
StopApi;StartApi
Call POST '/api/Usuario/me/solicitudes' $idemBody $idemTokens[0] 200 $idemHeader | Out-Null
PdfCall $idemFirst $idemTokens[0] $idemPdfA 200 -key $idemPdfKey
Assert ((Sql "SELECT count(*) FROM public.solicitudes_ciudadanas WHERE usuario_id='$($idemIds[0])'") -eq '3') 'La idempotencia desapareció después de reiniciar.'
Write-Output "OK: $($script:checks-$idemStart) comprobaciones de idempotencia y recuperación del flujo JSON/PDF."
