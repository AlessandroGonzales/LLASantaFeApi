# Contrato v1, comprobante propio y carreras de cuenta. Solo en la base temporal del runner.
$encCloseStart=$script:checks
$encPayload=Get-Content (Join-Path $root 'docs/ejemplos/encuesta-v1.json') -Raw | ConvertFrom-Json -AsHashtable
$encCreated=Json (Call POST '/api/Encuesta' $encPayload $at 201)
$encId=$encCreated.id
$encOwnPath="/api/Usuario/me/encuestas/$encId/respuesta"
Call GET $encOwnPath $null '' 401 | Out-Null
Call GET $encOwnPath $null $ta 404 | Out-Null
Call GET "/api/Usuario/me/encuestas/$([Guid]::NewGuid())/respuesta" $null $ta 404 | Out-Null
Call POST "/api/Encuesta/$encId/publicar" @{revision=1} $at 204 | Out-Null
$encDetail=Json (Call GET "/api/Encuesta/$encId" $null $ta 200)
Assert (($encDetail.configuracion.preguntas.tipo -join ',') -eq 'numero,opcion_unica,opcion_multiple,texto') 'Se alteraron los tipos o el orden v1.'
foreach($encAnswer in @(
    @{facilidad='3';seccion='noticias'},
    @{facilidad=$true;seccion='noticias'},
    @{facilidad=0;seccion='noticias'},
    @{facilidad=3;seccion='Noticias'},
    @{facilidad=3;seccion=@('noticias')},
    @{facilidad=3;seccion='noticias';dispositivos=@()},
    @{facilidad=3;seccion='noticias';dispositivos=@('movil','movil')},
    @{facilidad=3;seccion='noticias';dispositivos=@('inexistente')},
    @{facilidad=3;seccion='noticias';comentario=$null},
    @{facilidad=3;seccion='noticias';comentario='   '},
    @{facilidad=3;seccion='noticias';comentario=('x'*501)},
    @{facilidad=3;seccion='noticias';comentario="Texto`0inválido"},
    @{facilidad=3;seccion='noticias';inexistente='x'}
)){
    Call POST $encOwnPath @{respuestas=$encAnswer} $ta 400 | Out-Null
}
Assert ((Sql "SELECT count(*) FROM public.encuesta_respuestas WHERE encuesta_id='$encId'") -eq '0') 'Una respuesta inválida se almacenó.'
Call POST $encOwnPath '{"respuestas":{"facilidad":3,"seccion":"noticias","comentario":"\uD800"}}' $ta 400 | Out-Null
Call POST $encOwnPath '{"respuestas":{"facilidad":3,"seccion":"\uD800"}}' $ta 400 | Out-Null
Call POST $encOwnPath '{"respuestas":{"facilidad":3,"seccion":"noticias","dispositivos":["\uD800"]}}' $ta 400 | Out-Null
# Los cuatro tipos se guardan y se leen conservando tipos JSON, IDs y Unicode.
$encAnswer=@{respuestas=@{facilidad=3.5;seccion='noticias';dispositivos=@('movil','computadora');comentario='Más claridad en la búsqueda. 🧉'}}
$encPosted=Call POST $encOwnPath $encAnswer $ta 201
$encReceiptId=(Json $encPosted).id
Assert ($encPosted.Headers.Location[0].EndsWith($encOwnPath)) '201 no enlaza el comprobante propio.'
$encReceiptResponse=Call GET $encOwnPath $null $ta 200
$encReceipt=Json $encReceiptResponse
Assert ($encReceipt.id -eq $encReceiptId -and $encReceipt.createdAt) 'No se puede confirmar el envío propio.'
Assert (@($encReceipt.PSObject.Properties).Count -eq 2 -and $null -eq $encReceipt.respuestas -and $null -eq $encReceipt.usuarioId) 'El comprobante carga respuestas o identidades innecesarias.'
Assert ($encReceiptResponse.Headers['Cache-Control'] -match 'no-store') 'El comprobante permite caché compartida.'
Call GET $encOwnPath $null $tb 404 | Out-Null
Call GET $encOwnPath $null $at 404 | Out-Null
Call POST $encOwnPath $encAnswer $ta 409 | Out-Null
$encSaved=(Json (Call GET "/api/Encuesta/$encId/respuestas" $null $at 200)).items[0].respuestas
Assert ($encSaved.facilidad -eq 3.5 -and $encSaved.seccion -eq 'noticias' -and
    ($encSaved.dispositivos -join ',') -eq 'movil,computadora' -and $encSaved.comentario -eq 'Más claridad en la búsqueda. 🧉') 'Los cuatro tipos no completan el recorrido API → jsonb → API.'
# Optional no respondida se omite; min/max son inclusivos.
Call POST $encOwnPath @{respuestas=@{facilidad=1;seccion='sedes'}} $tb 201 | Out-Null
Call POST "/api/Encuesta/$encId/cerrar" @{revision=2} $at 204 | Out-Null
Call GET "/api/Encuesta/$encId" $null $ta 404 | Out-Null
Assert ((Json (Call GET $encOwnPath $null $ta 200)).id -eq $encReceiptId) 'Cerrar la encuesta impide recuperar un envío confirmado.'
Assert ((Sql "SELECT count(*) FROM public.encuesta_respuestas WHERE encuesta_id='$encId' AND usuario_id='$ida'") -eq '1') 'Un reintento duplicó la respuesta.'
# NUL es JSON válido, pero PostgreSQL jsonb/text no lo almacena: debe ser 400, nunca 500/503.
foreach($encField in @('titulo','descripcion')){
    $encBad=$encPayload.Clone();$encBad[$encField]="Texto`0inválido"
    Call POST '/api/Encuesta' $encBad $at 400 | Out-Null
}
foreach($encField in @('id','texto')){
    $encBad=$encPayload | ConvertTo-Json -Depth 20 | ConvertFrom-Json -AsHashtable
    $encBad.configuracion.preguntas[0][$encField]="Texto`0inválido"
    Call POST '/api/Encuesta' $encBad $at 400 | Out-Null
    $encBad=$encPayload | ConvertTo-Json -Depth 20 | ConvertFrom-Json -AsHashtable
    $encBad.configuracion.preguntas[1].opciones[0][$encField]="Texto`0inválido"
    Call POST '/api/Encuesta' $encBad $at 400 | Out-Null
}
$encBadJson=(Get-Content (Join-Path $root 'docs/ejemplos/encuesta-v1.json') -Raw).Replace('"id": "facilidad"','"id": "\uD800"')
Call POST '/api/Encuesta' $encBadJson $at 400 | Out-Null
$encMissing=[Guid]::NewGuid().ToString()
Call PUT "/api/Encuesta/$encMissing/borrador" ($encPayload+@{revision=1}) $at 404 | Out-Null
Call POST "/api/Encuesta/$encMissing/publicar" @{revision=1} $at 404 | Out-Null
Call POST "/api/Encuesta/$encMissing/cerrar" @{revision=1} $at 404 | Out-Null
Call GET "/api/Encuesta/$encMissing/respuestas" $null $at 404 | Out-Null
# Fechas inclusivas según Argentina; no depende de la zona horaria del proceso de pruebas.
$encToday=Sql "SELECT (statement_timestamp() AT TIME ZONE 'America/Argentina/Buenos_Aires')::date"
$encTimed=$encPayload.Clone();$encTimed.fechaInicio=$encToday;$encTimed.fechaFin=$encToday
$encTimedId=(Json (Call POST '/api/Encuesta' $encTimed $at 201)).id
Call POST "/api/Encuesta/$encTimedId/publicar" @{revision=1} $at 204 | Out-Null
Call GET "/api/Encuesta/$encTimedId" $null $ta 200 | Out-Null
Call POST "/api/Usuario/me/encuestas/$encTimedId/respuesta" @{respuestas=@{facilidad=5;seccion='sedes'}} $ta 201 | Out-Null
$encTomorrow=Sql "SELECT (statement_timestamp() AT TIME ZONE 'America/Argentina/Buenos_Aires')::date+1"
$encTimed.fechaInicio=$encTomorrow;$encTimed.fechaFin=$null
$encFutureId=(Json (Call POST '/api/Encuesta' $encTimed $at 201)).id
Call POST "/api/Encuesta/$encFutureId/publicar" @{revision=1} $at 204 | Out-Null
Call GET "/api/Encuesta/$encFutureId" $null $ta 404 | Out-Null
Call POST "/api/Usuario/me/encuestas/$encFutureId/respuesta" @{respuestas=@{facilidad=5;seccion='sedes'}} $ta 409 | Out-Null
$encYesterday=Sql "SELECT (statement_timestamp() AT TIME ZONE 'America/Argentina/Buenos_Aires')::date-1"
$encTimed.fechaInicio=$null;$encTimed.fechaFin=$encYesterday
$encExpiredId=(Json (Call POST '/api/Encuesta' $encTimed $at 201)).id
Call POST "/api/Encuesta/$encExpiredId/publicar" @{revision=1} $at 409 | Out-Null
# Límite de respuesta serializada: Unicode puede ocupar más bytes con el encoder JSON.
$encLargeQuestions=@();$encLargeAnswers=@{}
for($encN=0;$encN -lt 9;$encN++){
    $encLargeQuestions+=@{id="p$encN";texto='Comentario';tipo='texto';maxLongitud=2000}
    $encLargeAnswers["p$encN"]='é'*1600
}
$encLarge=$encPayload.Clone();$encLarge.configuracion=@{version=1;preguntas=$encLargeQuestions}
$encLargeId=(Json (Call POST '/api/Encuesta' $encLarge $at 201)).id
Call POST "/api/Encuesta/$encLargeId/publicar" @{revision=1} $at 204 | Out-Null
Call POST "/api/Usuario/me/encuestas/$encLargeId/respuesta" @{respuestas=$encLargeAnswers} $ta 400 | Out-Null
Assert ((Sql "SELECT count(*) FROM public.encuesta_respuestas WHERE encuesta_id='$encLargeId'") -eq '0') 'Se guardó una respuesta fuera del límite serializado.'
# La cuenta se desactiva después de validar JWT, antes de que el INSERT adquiera el bloqueo.
$encAccountId=(Json (Call POST '/api/Encuesta' $encPayload $at 201)).id
Call POST "/api/Encuesta/$encAccountId/publicar" @{revision=1} $at 204 | Out-Null
$encAccountSql=Join-Path $root '.artifacts/encuesta-account-test.sql'
@"
BEGIN;
SET application_name='encuesta_account_test';
UPDATE public.usuarios SET activo=false WHERE id='$idb';
SELECT pg_sleep(3);
COMMIT;
"@ | Set-Content $encAccountSql -Encoding utf8
$encAccountProcess=Start-Process -FilePath $psql -ArgumentList @('-h','127.0.0.1','-p','55432','-U','migration_test','-d',$db,'-v','ON_ERROR_STOP=1','-f',$encAccountSql) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root '.artifacts/encuesta-account.stdout.log') -RedirectStandardError (Join-Path $root '.artifacts/encuesta-account.stderr.log')
try {
    $encAccountLocked=$false
    for($encN=0;$encN -lt 30;$encN++){
        if((Sql "SELECT count(*) FROM pg_stat_activity WHERE application_name='encuesta_account_test' AND wait_event='PgSleep'") -eq '1'){$encAccountLocked=$true;break}
        Start-Sleep -Milliseconds 50
    }
    Assert $encAccountLocked 'No se pudo coordinar la baja concurrente.'
    Call POST "/api/Usuario/me/encuestas/$encAccountId/respuesta" @{respuestas=@{facilidad=3;seccion='sedes'}} $tb 409 | Out-Null
    $encAccountProcess.WaitForExit()
    Assert ($encAccountProcess.ExitCode -eq 0) 'Falló la baja concurrente.'
    Assert ((Sql "SELECT count(*) FROM public.encuesta_respuestas WHERE encuesta_id='$encAccountId'") -eq '0') 'Se guardó la respuesta después de desactivar la cuenta.'
    Call GET $encOwnPath $null $tb 401 | Out-Null
} finally {
    if(-not $encAccountProcess.HasExited){Stop-Process -Id $encAccountProcess.Id;$encAccountProcess.WaitForExit()}
    Sql "UPDATE public.usuarios SET activo=true WHERE id='$idb'" | Out-Null
}
$tb=(Json (Call POST '/api/Usuario/login' @{email=$b.email;password=$password} '' 200)).token
$encSwagger=Json (Call GET '/swagger/v1/swagger.json' $null '' 200)
Assert ($encSwagger.paths.'/api/Usuario/me/encuestas/{encuestaId}/respuesta'.get.responses.'200' -and
    $encSwagger.paths.'/api/Usuario/me/encuestas/{encuestaId}/respuesta'.post.responses.'201' -and
    $encSwagger.paths.'/api/Usuario/me/encuestas/{encuestaId}/respuesta'.post.responses.'409') 'Swagger no documenta envío, comprobante o conflicto.'
Write-Output "OK: $($script:checks-$encCloseStart) comprobaciones del cierre de Encuesta."
