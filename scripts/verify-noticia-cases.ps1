# Noticias: ejecutado por verify-usuario.ps1 con DB temporal y administrador autenticado.
$newsRequest=@{titulo='Noticia de prueba';resumen='Resumen inicial';contenido='Contenido completo de la noticia de prueba.';imagenPrincipalUrl='https://example.invalid/noticia.jpg';publicado=$true;destacada=$false;categoria='institucional';ciudadId=$city}
$emptyNews=Json (Call GET '/api/Noticia' $null '' 200)
Assert ($emptyNews.Count -eq 0) 'El feed vacío no es un array vacío.'
Call POST '/api/Noticia' $newsRequest '' 401|Out-Null
Call POST '/api/Noticia' $newsRequest $ta 403|Out-Null
$ids=@()
for($i=0;$i -lt 8;$i++) {
    $n=$newsRequest.Clone();$n.titulo="Noticia $i";$n.fechaPublicacion=[DateTimeOffset]::UtcNow.AddDays(-$i-1).ToString('o')
    $created=Call POST '/api/Noticia' $n $at 201
    $ids+=(Json $created).id
    Assert ($created.Headers.Location -match '/api/Noticia/administracion/') 'Location de creación incorrecta.'
}
$feed=Json (Call GET '/api/Noticia?limite=100' $null '' 200)
Assert ($feed.Count -eq 6 -and ($feed.id -join ',') -eq ($ids[0..5] -join ',')) 'No devuelve las seis últimas por fecha.'
Assert ($feed[0].contenido -eq $newsRequest.contenido -and $null -eq $feed[0].createdBy) 'Feed incompleto o expone auditoría interna.'
$n=$newsRequest.Clone();$n.publicado=$false;$n.titulo='Borrador'
$draftId=(Json (Call POST '/api/Noticia' $n $at 201)).id
$n=$newsRequest.Clone();$n.fechaPublicacion=[DateTimeOffset]::UtcNow.AddDays(5).ToString('o')
$futureId=(Json (Call POST '/api/Noticia' $n $at 201)).id
$n=$newsRequest.Clone();$n.fechaPublicacion=[DateTimeOffset]::UtcNow.ToString('o')
$deletedId=(Json (Call POST '/api/Noticia' $n $at 201)).id
Sql "UPDATE public.noticias SET deleted_at=now() WHERE id='$deletedId';"|Out-Null
$feed=Json (Call GET '/api/Noticia' $null '' 200)
Assert (($feed.id -join ',') -eq ($ids[0..5] -join ',')) 'El feed muestra borradores, futuras o eliminadas.'
Call GET "/api/Noticia/administracion/$draftId" $null '' 401|Out-Null
Call GET "/api/Noticia/administracion/$draftId" $null $ta 403|Out-Null
$draftDetail=Json (Call GET "/api/Noticia/administracion/$draftId" $null $at 200)
Assert (-not $draftDetail.publicado -and $null -eq $draftDetail.fechaPublicacion) 'Borrador publicado accidentalmente.'
Call PATCH "/api/Noticia/$($ids[0])" @{titulo='Título editado'} '' 401|Out-Null
Call PATCH "/api/Noticia/$($ids[0])" @{titulo='Título editado'} $ta 403|Out-Null
Call PATCH "/api/Noticia/$([Guid]::NewGuid())" @{titulo='Título editado'} $at 404|Out-Null
Call PATCH "/api/Noticia/$deletedId" @{titulo='Título editado'} $at 404|Out-Null
foreach($payload in @('{}','{"titulo":null}','{"contenido":"corto"}','{"publicado":null}','{"destacada":null}','{"cantidadVisualizaciones":99}','{"createdBy":"00000000-0000-0000-0000-000000000001"}','{"deletedAt":null}','{"imagenPrincipalUrl":"javascript:alert(1)"}','{"titulo":"Primero","titulo":"Segundo"}')) {
    Call PATCH "/api/Noticia/$($ids[0])" $payload $at 400|Out-Null
}
$invalid=$newsRequest.Clone();$invalid.ciudadId=[Guid]::NewGuid().ToString()
Call POST '/api/Noticia' $invalid $at 400|Out-Null
$invalid=$newsRequest.Clone();$invalid.cantidadVisualizaciones=1
Call POST '/api/Noticia' $invalid $at 400|Out-Null
$invalid=$newsRequest.Clone();$invalid.titulo='  '
Call POST '/api/Noticia' $invalid $at 400|Out-Null
$invalid=$newsRequest.Clone();$invalid.imagenPrincipalUrl='http://example.invalid/inseguro.jpg'
Call POST '/api/Noticia' $invalid $at 400|Out-Null
$before=Json (Call GET "/api/Noticia/administracion/$($ids[0])" $null $at 200)
Call PATCH "/api/Noticia/$($ids[0])" @{titulo='Título editado';resumen=$null;imagenPrincipalUrl=$null;categoria=$null;ciudadId=$null;destacada=$true} $at 204|Out-Null
$after=Json (Call GET "/api/Noticia/administracion/$($ids[0])" $null $at 200)
Assert ($after.titulo -eq 'Título editado' -and $null -eq $after.resumen -and $null -eq $after.imagenPrincipalUrl -and $null -eq $after.categoria -and $null -eq $after.ciudadId) 'PATCH no distingue null de omitido.'
Assert ($after.contenido -eq $before.contenido -and $after.fechaPublicacion -eq $before.fechaPublicacion -and $after.publicado -and $after.destacada -and $after.cantidadVisualizaciones -eq 0) 'PATCH modificó campos omitidos.'
Call PATCH "/api/Noticia/$($ids[0])" @{publicado=$false} $at 204|Out-Null
$feed=Json (Call GET '/api/Noticia' $null '' 200)
Assert (($feed.id -join ',') -eq ($ids[1..6] -join ',')) 'Despublicar no actualiza el feed.'
Call PATCH "/api/Noticia/$draftId" @{publicado=$true} $at 204|Out-Null
$feed=Json (Call GET '/api/Noticia' $null '' 200)
Assert ($feed[0].id -eq $draftId -and $null -ne $feed[0].fechaPublicacion) 'No se asigna fecha al publicar.'
# Ediciones en atributos distintos no pisan cambios ajenos.
$client=[Net.Http.HttpClient]::new()
try {
    $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$at)
    $one=[Net.Http.StringContent]::new('{"titulo":"Titulo concurrente"}',[Text.Encoding]::UTF8,'application/json')
    $two=[Net.Http.StringContent]::new('{"resumen":"Resumen concurrente"}',[Text.Encoding]::UTF8,'application/json')
    $tasks=@($client.PatchAsync("$base/api/Noticia/$draftId",$one),$client.PatchAsync("$base/api/Noticia/$draftId",$two))
    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks)
    Assert (($tasks|Where-Object{[int]$_.Result.StatusCode -ne 204}).Count -eq 0) 'Fallo en PATCH concurrente.'
    foreach($t in $tasks){$t.Result.Dispose()};$one.Dispose();$two.Dispose()
} finally {$client.Dispose()}
$after=Json (Call GET "/api/Noticia/administracion/$draftId" $null $at 200)
Assert ($after.titulo -eq 'Titulo concurrente' -and $after.resumen -eq 'Resumen concurrente') 'Se perdieron campos de un PATCH concurrente.'
