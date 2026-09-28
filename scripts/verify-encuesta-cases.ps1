# Casos de Encuesta; ejecutado por verify-usuario.ps1 dentro de su base temporal.
$draft=@{
    titulo='Encuesta de prueba';tipo='general';descripcion='Contrato inicial'
    configuracion=@{version=1;preguntas=@(
        @{id='satisfaccion';texto='Valor del servicio';tipo='numero';obligatoria=$true;min=1;max=5},
        @{id='opcion';texto='Elegir';tipo='opcion_unica';opciones=@(@{id='a';texto='A'},@{id='b';texto='B'})}
    )}
}
Call GET '/api/Encuesta' $null '' 401|Out-Null
Call POST '/api/Encuesta' $draft $ta 403|Out-Null
Call GET '/api/Encuesta/administracion' $null $ta 403|Out-Null
Call GET '/api/Encuesta?limite=51' $null $ta 400|Out-Null
Call GET '/api/Encuesta?limite=0' $null $ta 400|Out-Null
Call GET '/api/Encuesta?cursor=no-guid' $null $ta 400|Out-Null
foreach($configuration in @(@{version=2;preguntas=@()},@{version=1;preguntas=@($null)},@{version=1;preguntas=@(@{id='x';texto='X';tipo='texto';min=1})})) {
    $badDraft=$draft.Clone();$badDraft.configuracion=$configuration
    Call POST '/api/Encuesta' $badDraft $at 400|Out-Null
}
$badDraft=$draft.Clone();$badDraft.activa=$true
Call POST '/api/Encuesta' $badDraft $at 400|Out-Null
$badDraft=$draft.Clone();$badDraft.fechaInicio='2030-01-02';$badDraft.fechaFin='2030-01-01'
Call POST '/api/Encuesta' $badDraft $at 400|Out-Null
$badDraft=$draft.Clone();$badDraft.ciudadId=[Guid]::NewGuid().ToString()
Call POST '/api/Encuesta' $badDraft $at 400|Out-Null
$created=Json (Call POST '/api/Encuesta' $draft $at 201)
$surveyDraft=$created.id
Assert ($created.revision -eq 1) 'Revisión inicial incorrecta.'
Call GET "/api/Encuesta/$surveyDraft" $null $ta 404|Out-Null
Call GET "/api/Encuesta/administracion/$surveyDraft" $null $ta 403|Out-Null
$detail=Json (Call GET "/api/Encuesta/administracion/$surveyDraft" $null $at 200)
Assert (-not $detail.activa -and $null -eq $detail.publicadaAt) 'Borrador publicado accidentalmente.'
Call POST "/api/Usuario/me/encuestas/$surveyDraft/respuesta" @{respuestas=@{satisfaccion=3}} $ta 409|Out-Null
$edit=$draft.Clone();$edit.revision=1;$edit.titulo='Encuesta editada'
Call PUT "/api/Encuesta/$surveyDraft/borrador" $edit $at 204|Out-Null
Call PUT "/api/Encuesta/$surveyDraft/borrador" $edit $at 409|Out-Null
Call POST "/api/Encuesta/$surveyDraft/publicar" @{revision=1} $at 409|Out-Null
Call POST "/api/Encuesta/$surveyDraft/publicar" @{revision=2} $at 204|Out-Null
$detail=Json (Call GET "/api/Encuesta/$surveyDraft" $null $ta 200)
Assert ($detail.revision -eq 3 -and $detail.configuracion.preguntas.Count -eq 2) 'Publicación/configuración incorrecta.'
$edit.revision=3
Call PUT "/api/Encuesta/$surveyDraft/borrador" $edit $at 409|Out-Null
Call POST "/api/Encuesta/$surveyDraft/publicar" @{revision=3} $at 409|Out-Null
Call GET "/api/Encuesta/$surveyDraft/respuestas" $null $ta 403|Out-Null
Call POST "/api/Usuario/me/encuestas/$surveyDraft/respuesta" @{respuestas=@{satisfaccion=6}} $ta 400|Out-Null
Call POST "/api/Usuario/me/encuestas/$surveyDraft/respuesta" @{respuestas=@{satisfaccion=3;opcion='a'}} $ta 201|Out-Null
Call POST "/api/Usuario/me/encuestas/$surveyDraft/respuesta" @{respuestas=@{satisfaccion=3}} $ta 409|Out-Null
Call POST "/api/Usuario/me/encuestas/$surveyDraft/respuesta" @{respuestas=@{satisfaccion=4}} $tb 201|Out-Null
$list=Json (Call GET '/api/Encuesta' $null $ta 200)
Assert (($list.items|Where-Object id -eq $surveyDraft).respondida) 'No se indica respuesta propia.'
Assert ($null -eq $list.items[0].configuracion) 'Listado carga configuraciones completas.'
$page=Json (Call GET "/api/Encuesta/$surveyDraft/respuestas?limite=1" $null $at 200)
Assert ($page.items.Count -eq 1 -and $page.siguienteCursor) 'Paginación de respuestas incorrecta.'
Assert ($null -eq $page.items[0].usuarioId -and $null -eq $page.items[0].ipAddress) 'Se expone identidad innecesaria.'
$next=Json (Call GET "/api/Encuesta/$surveyDraft/respuestas?limite=1&cursor=$($page.siguienteCursor)" $null $at 200)
Assert ($next.items.Count -eq 1 -and $next.items[0].id -ne $page.items[0].id -and $null -eq $next.siguienteCursor) 'Cursor repite respuestas.'
Call GET "/api/Encuesta/$surveyDraft/respuestas?limite=21" $null $at 400|Out-Null
Call POST "/api/Encuesta/$surveyDraft/cerrar" @{revision=2} $at 409|Out-Null
Call POST "/api/Encuesta/$surveyDraft/cerrar" @{revision=3} $at 204|Out-Null
Call GET "/api/Encuesta/$surveyDraft" $null $ta 404|Out-Null
Call POST "/api/Usuario/me/encuestas/$surveyDraft/respuesta" @{respuestas=@{satisfaccion=3}} $at 409|Out-Null
Call GET "/api/Encuesta/$surveyDraft/respuestas" $null $at 200|Out-Null
Call POST "/api/Encuesta/$surveyDraft/publicar" @{revision=4} $at 409|Out-Null
$second=Json (Call POST '/api/Encuesta' $draft $at 201)
$page=Json (Call GET '/api/Encuesta/administracion?limite=1' $null $at 200)
$next=Json (Call GET "/api/Encuesta/administracion?limite=1&cursor=$($page.siguienteCursor)" $null $at 200)
Assert ($page.items[0].id -ne $next.items[0].id) 'Cursor repite encuestas.'
# El trigger también protege el contrato frente a cambios directos en SQL.
Sql "DO `$$ BEGIN BEGIN UPDATE public.encuestas SET configuracion='{}'::jsonb WHERE id='$surveyDraft'; RAISE EXCEPTION 'Published survey was changed'; EXCEPTION WHEN check_violation THEN NULL; END; END; `$$;"|Out-Null
$script:checks++
# Dos administradores guardan la misma revisión: solo uno puede ganar.
$edit2=$draft.Clone();$edit2.revision=1
$client=[Net.Http.HttpClient]::new()
try {
    $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$at)
    $payload=$edit2|ConvertTo-Json -Depth 20 -Compress
    $one=[Net.Http.StringContent]::new($payload,[Text.Encoding]::UTF8,'application/json')
    $two=[Net.Http.StringContent]::new($payload,[Text.Encoding]::UTF8,'application/json')
    $tasks=@($client.PutAsync("$base/api/Encuesta/$($second.id)/borrador",$one),$client.PutAsync("$base/api/Encuesta/$($second.id)/borrador",$two))
    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks)
    $codes=@($tasks|ForEach-Object{[int]$_.Result.StatusCode}|Sort-Object)
    Assert (($codes -join ',') -eq '204,409') 'Ediciones simultáneas sobrescriben la revisión.'
    foreach($t in $tasks){$t.Result.Dispose()};$one.Dispose();$two.Dispose()
} finally {$client.Dispose()}
# Una respuesta espera mientras otro proceso cierra la encuesta y confirma el cierre.
Call POST "/api/Encuesta/$($second.id)/publicar" @{revision=2} $at 204|Out-Null
$closeSql=Join-Path $root '.artifacts/encuesta-close-test.sql'
@"
BEGIN;
SET application_name = 'encuesta_close_test';
UPDATE public.encuestas SET activa=false WHERE id='$($second.id)';
SELECT pg_sleep(3);
COMMIT;
"@ | Set-Content $closeSql -Encoding utf8
$closer=Start-Process -FilePath $psql -ArgumentList @('-h','127.0.0.1','-p','55432','-U','migration_test','-d',$db,'-v','ON_ERROR_STOP=1','-f',$closeSql) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root '.artifacts/encuesta-close.stdout.log') -RedirectStandardError (Join-Path $root '.artifacts/encuesta-close.stderr.log')
try {
    $locked=$false
    for($i=0;$i -lt 30;$i++) {
        if((Sql "SELECT count(*) FROM pg_stat_activity WHERE application_name='encuesta_close_test' AND wait_event='PgSleep'") -eq '1'){$locked=$true;break}
        Start-Sleep -Milliseconds 50
    }
    Assert $locked 'No se consiguió preparar el cierre concurrente.'
    Call POST "/api/Usuario/me/encuestas/$($second.id)/respuesta" @{respuestas=@{satisfaccion=3}} $ta 409|Out-Null
    $closer.WaitForExit()
    Assert ($closer.ExitCode -eq 0) 'Falló el cierre concurrente.'
    Assert ((Sql "SELECT count(*) FROM public.encuesta_respuestas WHERE encuesta_id='$($second.id)'") -eq '0') 'Se aceptó una respuesta después del cierre.'
} finally {if(-not $closer.HasExited){Stop-Process -Id $closer.Id}}

