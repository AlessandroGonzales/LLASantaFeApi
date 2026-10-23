# Solo en la DB sintética creada por verify-usuario.ps1. Gmail permanece deshabilitado.
$affCloseStart=$script:checks
$affCloseUsers=@();$affCloseTokens=@();$affCloseUserIds=@();$affCloseIds=@()
$affCloseAdmin=(Json (Call GET '/api/Usuario/me' $null $at 200)).idUsuario
foreach($number in 1..4){
    $account=@{nombre='Afiliacion';apellido="Cierre $number";email="afiliacion-cierre$number@example.invalid";password=$password;fechaNacimiento='1990-01-01'}
    Call POST '/api/Usuario' $account '' 202 | Out-Null
    Call POST '/api/Usuario/email/confirmar' @{email=$account.email;password=$password;codigo=(CuentaCodigo $account.email 'email_verificacion')} '' 204 | Out-Null
    $token=(Json (Call POST '/api/Usuario/login' @{email=$account.email;password=$password} '' 200)).token
    $ownId=(Json (Call GET '/api/Usuario/me' $null $token 200)).idUsuario
    Call GET '/api/SolicitudAfiliacion/me' $null $token 404 | Out-Null
    $created=Call POST '/api/SolicitudAfiliacion' @{confirmacion=$true} $token 201
    Assert ($created.Headers.Location -match '/api/SolicitudAfiliacion/me$') 'Location de afiliación no permite recuperar el estado propio.'
    $affCloseUsers+=$account;$affCloseTokens+=$token;$affCloseUserIds+=$ownId;$affCloseIds+=(Json $created).id
}
Assert ($null -ne $swagger.paths.'/api/SolicitudAfiliacion/{id}/rechazar'.post) 'Swagger no documenta el rechazo.'
foreach($action in @('aprobar','rechazar')){
    $operation=$swagger.paths.("/api/SolicitudAfiliacion/{id}/$action").post
    Assert ($null -ne $operation.responses.'204' -and $null -ne $operation.responses.'404' -and $null -ne $operation.responses.'409') "Swagger no documenta los resultados reales de $action"
}
Assert ($null -ne $swagger.paths.'/api/SolicitudAfiliacion/me'.get.responses.'404') 'Swagger no documenta el estado sin solicitud propia.'
$affFilterParameter=@($swagger.paths.'/api/SolicitudAfiliacion/pendientes'.get.parameters | Where-Object name -eq 'estado')
Assert ($affFilterParameter.Count -eq 1) 'Swagger no documenta el estado de la bandeja.'
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[0])/rechazar" @{motivo='No reúne las condiciones de la solicitud.'} '' 401 | Out-Null
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[0])/rechazar" @{motivo='No reúne las condiciones de la solicitud.'} $affCloseTokens[0] 403 | Out-Null
Call POST "/api/SolicitudAfiliacion/$([Guid]::NewGuid())/rechazar" @{motivo='Una resolución administrativa de prueba.'} $at 404 | Out-Null
foreach($invalid in @(@{},@{motivo='corto'},@{motivo=(' '*20)},@{motivo=('x'*2001)},@{motivo=('Motivo de prueba con '+[char]0+' NUL')},@{motivo='Motivo de prueba válido.';rechazadaPor=$affCloseUserIds[0]},@{motivo='Motivo de prueba válido.';estado='aprobada'},@{motivo='Motivo de prueba válido.';observacion='Una nota interna no debe ir en el rechazo.'})){
    Call POST "/api/SolicitudAfiliacion/$($affCloseIds[0])/rechazar" $invalid $at 400 | Out-Null
}
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[0])/aprobar" @{observacion=('Observación con '+[char]0+' NUL')} $at 400 | Out-Null
foreach($state in @('invalid','aprobada','rechazada')){
    Call GET "/api/SolicitudAfiliacion/pendientes?estado=$state" $null $at 400 | Out-Null
}
Call GET '/api/SolicitudAfiliacion/pendientes?estado=en_revision' $null '' 401 | Out-Null
Call GET '/api/SolicitudAfiliacion/pendientes?estado=en_revision' $null $affCloseTokens[0] 403 | Out-Null
Sql "UPDATE public.solicitudes_afiliacion SET observacion='Nota interna que no debe salir al solicitante.' WHERE id='$($affCloseIds[0])'; UPDATE public.solicitudes_afiliacion SET estado='en_revision' WHERE id='$($affCloseIds[1])';" | Out-Null
$affPending=Json (Call GET '/api/SolicitudAfiliacion/pendientes' $null $at 200)
$affReview=Json (Call GET '/api/SolicitudAfiliacion/pendientes?estado=en_revision&limite=1' $null $at 200)
Assert ($affPending.items.id -contains $affCloseIds[0] -and $affPending.items.id -notcontains $affCloseIds[1]) 'La bandeja por defecto mezcla estados.'
Assert ($affReview.items.Count -eq 1 -and $affReview.items[0].id -eq $affCloseIds[1] -and $null -eq $affReview.siguienteCursor) 'Una solicitud en revisión queda inaccesible o mal paginada.'
Assert ($null -eq $affReview.items[0].email -and $null -eq $affReview.items[0].telefono -and $null -eq $affReview.items[0].observacion) 'La bandeja expone datos innecesarios.'
Call PATCH '/api/Usuario/me' @{telefono='+5493411234567'} $affCloseTokens[0] 204 | Out-Null
$affContact=Json (Call GET "/api/SolicitudAfiliacion/$($affCloseIds[0])" $null $at 200)
Assert ($affContact.telefono -eq '+5493411234567' -and $affContact.emailVerificado -and $affContact.requiereVerificarEmail) 'El contacto no refleja el perfil o la verificación real.'
Assert ($affContact.observacion -eq 'Nota interna que no debe salir al solicitante.' -and $null -eq $affContact.passwordHash -and $null -eq $affContact.versionAcceso) 'Detalle administrativo incorrecto.'
$affOwn=Json (Call GET "/api/SolicitudAfiliacion/me?usuarioId=$idb&id=$affB" $null $affCloseTokens[0] 200)
Assert ($affOwn.id -eq $affCloseIds[0] -and $affOwn.estado -eq 'pendiente') 'La consulta propia permite seleccionar otra persona.'
Assert ($null -eq $affOwn.observacion -and $null -eq $affOwn.rechazadaPor -and $null -eq $affOwn.email) 'La consulta propia expone notas internas o contactos.'

$affReason='La solicitud no cumple las condiciones informadas para este trámite.'
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[0])/rechazar" @{motivo=('  '+$affReason+'  ')} $at 204 | Out-Null
$affRejected=Json (Call GET "/api/SolicitudAfiliacion/$($affCloseIds[0])" $null $at 200)
Assert ($affRejected.estado -eq 'rechazada' -and $affRejected.motivoRechazo -eq $affReason -and $affRejected.rechazadaPor -eq $affCloseAdmin -and $affRejected.rechazadaAt) 'El rechazo no guarda motivo, autor y fecha.'
Assert ($null -eq $affRejected.aprobadaAt -and $null -eq $affRejected.aprobadaPor -and $affRejected.observacion -eq $affContact.observacion) 'Rechazo mezclado con aprobación o notas internas.'
$affOwn=Json (Call GET '/api/SolicitudAfiliacion/me' $null $affCloseTokens[0] 200)
Assert ($affOwn.estado -eq 'rechazada' -and $affOwn.motivoRechazo -eq $affReason -and $affOwn.rechazadaAt -and $null -eq $affOwn.observacion -and $null -eq $affOwn.rechazadaPor) 'El usuario no recibe el motivo público o recibe auditoría interna.'
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[0])/rechazar" @{motivo='Este texto no debe reemplazar el motivo original.'} $at 409 | Out-Null
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[0])/aprobar" @{} $at 409 | Out-Null
Call POST '/api/SolicitudAfiliacion' @{confirmacion=$true} $affCloseTokens[0] 409 | Out-Null
Call POST '/api/Usuario/me/afiliacion' @{confirmacion=$true} $affCloseTokens[0] 409 | Out-Null
$affUnchanged=Json (Call GET "/api/SolicitudAfiliacion/$($affCloseIds[0])" $null $at 200)
Assert ($affUnchanged.rechazadaAt -eq $affRejected.rechazadaAt -and $affUnchanged.motivoRechazo -eq $affReason) 'Un reintento cambió la resolución definitiva.'
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='afiliacion_aprobada' AND referencia_id='$($affCloseIds[0])'") -eq '0') 'Un rechazo genera correo de aprobación.'

# Cambiar el email invalida la verificación y bloquea aprobación hasta confirmarlo.
Sql "UPDATE public.usuarios SET email='afiliacion-nuevo-email@example.invalid' WHERE id='$($affCloseUserIds[1])'" | Out-Null
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[1])/aprobar" @{} $at 409 | Out-Null
$affUnverified=Json (Call GET "/api/SolicitudAfiliacion/$($affCloseIds[1])" $null $at 200)
Assert (-not $affUnverified.emailVerificado -and $affUnverified.requiereVerificarEmail -and $affUnverified.estado -eq 'en_revision') 'Se aprobó una cuenta con verificación obligatoria pendiente.'
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='afiliacion_aprobada' AND referencia_id='$($affCloseIds[1])'") -eq '0') 'Se encoló aprobación para un email aún no confirmado.'
Sql "UPDATE public.usuarios SET activo=false WHERE id='$($affCloseUserIds[1])'" | Out-Null
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[1])/aprobar" @{} $at 409 | Out-Null
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[1])/rechazar" @{motivo='Se cierra la solicitud pendiente de una cuenta inactiva.'} $at 204 | Out-Null
Call GET '/api/SolicitudAfiliacion/me' $null $affCloseTokens[1] 401 | Out-Null

# Dos decisiones distintas simultáneas: una resolución y un solo efecto en la cola.
$affBeforeRace=Json (Call GET '/api/SolicitudAfiliacion/totales' $null $at 200)
$client=[Net.Http.HttpClient]::new()
try {
    $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$at)
    $one=[Net.Http.StringContent]::new('{"observacion":"Revisión administrativa completada."}',[Text.Encoding]::UTF8,'application/json')
    $two=[Net.Http.StringContent]::new('{"motivo":"La resolución administrativa de esta prueba es negativa."}',[Text.Encoding]::UTF8,'application/json')
    $tasks=@($client.PostAsync("$base/api/SolicitudAfiliacion/$($affCloseIds[2])/aprobar",$one),$client.PostAsync("$base/api/SolicitudAfiliacion/$($affCloseIds[2])/rechazar",$two))
    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks)
    Assert ((@($tasks | ForEach-Object {[int]$_.Result.StatusCode} | Sort-Object) -join ',') -eq '204,409') 'Aprobar y rechazar simultáneamente produce más de una resolución.'
    foreach($t in $tasks){$t.Result.Dispose()};$one.Dispose();$two.Dispose()
} finally {$client.Dispose()}
$affWinner=Json (Call GET "/api/SolicitudAfiliacion/$($affCloseIds[2])" $null $at 200)
$affWinnerApproved=$affWinner.estado -eq 'aprobada'
Assert ($affWinner.estado -in @('aprobada','rechazada')) 'La carrera dejó la solicitud sin resolución.'
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='afiliacion_aprobada' AND referencia_id='$($affCloseIds[2])'") -eq ([int]$affWinnerApproved).ToString()) 'La carrera duplicó u omitió el correo de aprobación.'
$affAfterRace=Json (Call GET '/api/SolicitudAfiliacion/totales' $null $at 200)
Assert ($affAfterRace.totalAfiliados -eq ($affBeforeRace.totalAfiliados+[int]$affWinnerApproved) -and $affAfterRace.pendientes -eq ($affBeforeRace.pendientes-1) -and $affAfterRace.rechazadas -eq ($affBeforeRace.rechazadas+[int](-not $affWinnerApproved))) 'La carrera alteró más de un contador.'

# Si falla la cola, aprobación y auditoría retroceden en la misma transacción.
Sql @'
CREATE FUNCTION public.afiliacion_test_correo_fail() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.tipo='afiliacion_aprobada' AND NEW.usuario_id=(SELECT id FROM public.usuarios WHERE email='afiliacion-cierre4@example.invalid') THEN
  RAISE EXCEPTION 'Simulated outbox failure' USING ERRCODE='P0001';
 END IF;
 RETURN NEW;
END; $$;
CREATE TRIGGER aff_test_correo_fail BEFORE INSERT ON public.correos_transaccionales
 FOR EACH ROW EXECUTE FUNCTION public.afiliacion_test_correo_fail();
'@ | Out-Null
try {
    Call POST "/api/SolicitudAfiliacion/$($affCloseIds[3])/aprobar" @{observacion='Esta aprobación debe revertirse por fallo de cola.'} $at 503 | Out-Null
    $affRollback=Json (Call GET "/api/SolicitudAfiliacion/$($affCloseIds[3])" $null $at 200)
    Assert ($affRollback.estado -eq 'pendiente' -and $null -eq $affRollback.aprobadaAt -and $null -eq $affRollback.aprobadaPor -and $null -eq $affRollback.observacion) 'La aprobación o auditoría escaparon del rollback de correo.'
    Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='afiliacion_aprobada' AND referencia_id='$($affCloseIds[3])'") -eq '0') 'La cola guardó un resultado parcial.'
} finally {
    Sql 'DROP TRIGGER aff_test_correo_fail ON public.correos_transaccionales; DROP FUNCTION public.afiliacion_test_correo_fail();' | Out-Null
}
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[3])/aprobar" @{observacion='Aprobación definitiva tras verificar los datos.'} $at 204 | Out-Null
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[3])/aprobar" @{} $at 409 | Out-Null
Call POST "/api/SolicitudAfiliacion/$($affCloseIds[3])/rechazar" @{motivo='Este intento no debe revertir una aprobación.'} $at 409 | Out-Null
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='afiliacion_aprobada' AND referencia_id='$($affCloseIds[3])'") -eq '1') 'Reintentar aprobación duplica el correo.'
$affApprovedOwn=Json (Call GET '/api/SolicitudAfiliacion/me' $null $affCloseTokens[3] 200)
Assert ($affApprovedOwn.estado -eq 'aprobada' -and $affApprovedOwn.aprobadaAt -and $null -eq $affApprovedOwn.rechazadaAt -and $null -eq $affApprovedOwn.motivoRechazo -and $null -eq $affApprovedOwn.observacion) 'La aprobación propia expone observaciones o datos de rechazo.'
foreach($state in @('pendiente','en_revision')){
    $empty=Json (Call GET "/api/SolicitudAfiliacion/pendientes?estado=$state" $null $at 200)
    Assert ($empty.items.Count -eq 0 -and $null -eq $empty.siguienteCursor) 'Una resolución sigue en la bandeja de trabajo.'
}
$affTotals=Json (Call GET '/api/SolicitudAfiliacion/totales' $null $at 200)
Assert ($affTotals.totalSolicitudes -eq $affTotals.totalAfiliados+$affTotals.pendientes+$affTotals.enRevision+$affTotals.rechazadas) 'Los totales no coinciden con la distribución de estados.'
Assert ((Sql "SELECT count(*) FROM public.solicitudes_afiliacion WHERE usuario_id='$($affCloseUserIds[0])'") -eq '1') 'Resolver y reintentar permitió una segunda solicitud.'
Write-Output "OK: $($script:checks-$affCloseStart) comprobaciones del cierre de Afiliación."
