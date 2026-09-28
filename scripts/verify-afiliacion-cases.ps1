# Ejecutado por verify-usuario.ps1 contra PostgreSQL temporal.
$affB=(Sql "SELECT id FROM public.solicitudes_afiliacion WHERE usuario_id='$idb'")
$affA=(Sql "SELECT id FROM public.solicitudes_afiliacion WHERE usuario_id='$ida'")
Call POST '/api/SolicitudAfiliacion' @{confirmacion=$true} '' 401|Out-Null
Call POST '/api/SolicitudAfiliacion' @{confirmacion=$false} $at 400|Out-Null
Call POST '/api/SolicitudAfiliacion' @{confirmacion=$true;usuarioId=$ida} $at 400|Out-Null
Call POST '/api/SolicitudAfiliacion' @{confirmacion=$true;estado='aprobada'} $at 400|Out-Null
Call GET '/api/SolicitudAfiliacion/me' $null $at 404|Out-Null
$before=Json (Call GET '/api/SolicitudAfiliacion/totales' $null $at 200)
Assert ($before.totalAfiliados -eq 0 -and $before.totalSolicitudes -eq 2 -and $before.pendientes -eq 1 -and $before.rechazadas -eq 1) 'Totales previos incorrectos.'
$created=Call POST '/api/SolicitudAfiliacion' @{confirmacion=$true} $at 201
$affAdmin=(Json $created).id
Assert ($created.Headers.Location -match '/api/SolicitudAfiliacion/me$') 'Location no apunta a solicitud propia.'
Call POST '/api/SolicitudAfiliacion' @{confirmacion=$true} $at 409|Out-Null
Call POST '/api/Usuario/me/afiliacion' @{confirmacion=$true} $at 409|Out-Null
foreach($path in @('/api/SolicitudAfiliacion/pendientes','/api/SolicitudAfiliacion/totales',"/api/SolicitudAfiliacion/$affB")) {
    Call GET $path $null '' 401|Out-Null
    Call GET $path $null $ta 403|Out-Null
}
Call POST "/api/SolicitudAfiliacion/$affB/aprobar" @{} '' 401|Out-Null
Call POST "/api/SolicitudAfiliacion/$affB/aprobar" @{} $tb 403|Out-Null
Call POST "/api/SolicitudAfiliacion/$([Guid]::NewGuid())/aprobar" @{} $at 404|Out-Null
Call POST "/api/SolicitudAfiliacion/$affB/aprobar" @{observacion=('x'*2001)} $at 400|Out-Null
Call POST "/api/SolicitudAfiliacion/$affB/aprobar" @{aprobadaPor=$ida} $at 400|Out-Null
Call GET '/api/SolicitudAfiliacion/pendientes?limite=51' $null $at 400|Out-Null
Call GET '/api/SolicitudAfiliacion/pendientes?limite=0' $null $at 400|Out-Null
Call GET '/api/SolicitudAfiliacion/pendientes?cursor=invalid' $null $at 400|Out-Null
$page=Json (Call GET '/api/SolicitudAfiliacion/pendientes?limite=1' $null $at 200)
$next=Json (Call GET "/api/SolicitudAfiliacion/pendientes?limite=1&cursor=$($page.siguienteCursor)" $null $at 200)
Assert ($page.items.Count -eq 1 -and $next.items.Count -eq 1 -and $page.items[0].id -ne $next.items[0].id -and $null -eq $next.siguienteCursor) 'Paginacion de pendientes incorrecta.'
Assert ($null -eq $page.items[0].email -and $null -eq $page.items[0].telefono) 'La bandeja carga contactos innecesariamente.'
$own=Json (Call GET '/api/SolicitudAfiliacion/me' $null $tb 200)
Assert ($own.id -eq $affB -and $own.estado -eq 'pendiente' -and $null -eq $own.observacion) 'La consulta propia devuelve datos incorrectos.'
Call PATCH '/api/Usuario/me' @{telefono=$a.telefono} $ta 204|Out-Null
$contact=Json (Call GET "/api/SolicitudAfiliacion/$affA" $null $at 200)
Assert ($contact.email -eq $a.email -and $contact.telefono -eq $a.telefono -and $null -eq $contact.passwordHash) 'Detalle de contacto incorrecto.'
Sql "UPDATE public.usuarios SET activo=false WHERE id='$idb';"|Out-Null
Call POST "/api/SolicitudAfiliacion/$affB/aprobar" @{} $at 409|Out-Null
Call POST '/api/SolicitudAfiliacion' @{confirmacion=$true} $tb 401|Out-Null
Sql "UPDATE public.usuarios SET activo=true WHERE id='$idb';"|Out-Null
$tb=(Json (Call POST '/api/Usuario/login' @{email=$b.email;password=$password} '' 200)).token
# Dos aprobaciones simultáneas: solo una cambia el estado y se cuenta una sola vez.
$client=[Net.Http.HttpClient]::new()
try {
    $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$at)
    $one=[Net.Http.StringContent]::new('{"observacion":"Datos revisados."}',[Text.Encoding]::UTF8,'application/json')
    $two=[Net.Http.StringContent]::new('{"observacion":"Datos revisados."}',[Text.Encoding]::UTF8,'application/json')
    $tasks=@($client.PostAsync("$base/api/SolicitudAfiliacion/$affB/aprobar",$one),$client.PostAsync("$base/api/SolicitudAfiliacion/$affB/aprobar",$two))
    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks)
    $codes=@($tasks|ForEach-Object{[int]$_.Result.StatusCode}|Sort-Object)
    Assert (($codes -join ',') -eq '204,409') 'Aprobacion concurrente incorrecta.'
    foreach($t in $tasks){$t.Result.Dispose()};$one.Dispose();$two.Dispose()
} finally {$client.Dispose()}
$contact=Json (Call GET "/api/SolicitudAfiliacion/$affB" $null $at 200)
$adminId=(Json (Call GET '/api/Usuario/me' $null $at 200)).idUsuario
Assert ($contact.estado -eq 'aprobada' -and $contact.aprobadaPor -eq $adminId -and $contact.aprobadaAt -and $contact.observacion -eq 'Datos revisados.') 'No se registra la aprobación.'
$own=Json (Call GET '/api/SolicitudAfiliacion/me' $null $tb 200)
Assert ($own.estado -eq 'aprobada' -and $own.aprobadaAt) 'El usuario no ve su aprobación.'
Call POST "/api/SolicitudAfiliacion/$affB/aprobar" @{} $at 409|Out-Null
Call POST "/api/SolicitudAfiliacion/$affA/aprobar" @{} $at 409|Out-Null
$total=Json (Call GET '/api/SolicitudAfiliacion/totales' $null $at 200)
Assert ($total.totalAfiliados -eq 1 -and $total.totalSolicitudes -eq 3 -and $total.pendientes -eq 1) 'Se contaron solicitudes como afiliaciones aprobadas.'
# Estado heredado en_revision también puede aprobarse.
Sql "UPDATE public.solicitudes_afiliacion SET estado='en_revision' WHERE id='$affAdmin';"|Out-Null
Call POST "/api/SolicitudAfiliacion/$affAdmin/aprobar" @{} $at 204|Out-Null
$page=Json (Call GET '/api/SolicitudAfiliacion/pendientes' $null $at 200)
Assert ($page.items.Count -eq 0 -and $null -eq $page.siguienteCursor) 'Una resuelta sigue en pendientes.'
$total=Json (Call GET '/api/SolicitudAfiliacion/totales' $null $at 200)
Assert ($total.totalAfiliados -eq 2 -and $total.totalSolicitudes -eq 3 -and $total.rechazadas -eq 1 -and $total.enRevision -eq 0) 'Total final incorrecto.'
# Una baja de la cuenta no modifica la afiliación aprobada registrada.
Sql "UPDATE public.usuarios SET activo=false WHERE id='$idb';"|Out-Null
$total=Json (Call GET '/api/SolicitudAfiliacion/totales' $null $at 200)
Assert ($total.totalAfiliados -eq 2) 'La baja de cuenta alteró la afiliación histórica.'
Sql "UPDATE public.usuarios SET activo=true WHERE id='$idb';"|Out-Null
$tb=(Json (Call POST '/api/Usuario/login' @{email=$b.email;password=$password} '' 200)).token
