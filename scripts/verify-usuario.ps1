param([Parameter(Mandatory)][string]$SchemaPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$psql='C:/Program Files/PostgreSQL/16/bin/psql.exe'
$db='net10_usuario_'+[Guid]::NewGuid().ToString('N')
$base='http://127.0.0.1:5191'
$script:checks=0
$api=$null
$old=@{}
foreach($key in @('ConnectionStrings__DefaultConnection','JwtSettings__Key','ASPNETCORE_ENVIRONMENT','RateLimits__General','RateLimits__Login','RateLimits__Registro','RateLimits__Escritura','PdfAntivirus__Host','PdfAntivirus__Port','Gmail__Enabled')){$old[$key]=[Environment]::GetEnvironmentVariable($key)}
function Assert($condition,$message){if(-not $condition){throw $message};$script:checks++}
function Sql($query){
    $result=& $psql -h 127.0.0.1 -p 55432 -U migration_test -d $db -At -v ON_ERROR_STOP=1 -c $query
    if($LASTEXITCODE -ne 0){throw 'Falló SQL de prueba.'}
    return $result
}
function Call($method,$path,$body,$token,$status){
    $args=@{Uri="$base$path";Method=$method;SkipHttpErrorCheck=$true}
    if($null -ne $body){$args.ContentType='application/json';$args.Body=if($body -is [string]){$body}else{$body|ConvertTo-Json -Depth 20 -Compress}}
    if($token){$args.Headers=@{Authorization="Bearer $token"}}
    $r=Invoke-WebRequest @args
    Assert ($r.StatusCode -eq $status) "$method $path esperaba $status; recibió $($r.StatusCode): $($r.Content)"
    return $r
}
function Json($response){return $response.Content|ConvertFrom-Json}
function StopApi{if($script:api -and -not $script:api.HasExited){Stop-Process -Id $script:api.Id;$script:api.WaitForExit()}}
function StartApi{
    $script:api=Start-Process -FilePath (Join-Path $root '.tools/dotnet/dotnet.exe') -ArgumentList @('bin/Release/net10.0/Presentation.dll','--urls',$base) -WorkingDirectory (Join-Path $root 'Presentation') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root '.artifacts/usuario-test.stdout.log') -RedirectStandardError (Join-Path $root '.artifacts/usuario-test.stderr.log')
    for($i=0;$i -lt 40;$i++){
        if($script:api.HasExited){throw 'La API no inició; revisar .artifacts/usuario-test.*.log.'}
        try{$r=Invoke-WebRequest "$base/swagger/v1/swagger.json" -SkipHttpErrorCheck;if($r.StatusCode -eq 200){return}}catch{}
        Start-Sleep -Milliseconds 250
    }
    throw 'La API no respondió.'
}
function ConcurrentPost($path,$body,$token){
    $client=[Net.Http.HttpClient]::new()
    try{
        $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token)
        $json=$body|ConvertTo-Json -Depth 20 -Compress
        $one=[Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json')
        $two=[Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json')
        $tasks=@($client.PostAsync("$base$path",$one),$client.PostAsync("$base$path",$two))
        [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks)
        $codes=@($tasks|ForEach-Object{[int]$_.Result.StatusCode}|Sort-Object)
        Assert (($codes -join ',') -eq '201,409') "Concurrencia incorrecta: $codes"
        foreach($t in $tasks){$t.Result.Dispose()}
        $one.Dispose();$two.Dispose()
    }finally{$client.Dispose()}
}
function Encode([byte[]]$bytes){return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_')}
function Decode($value){$v=$value.Replace('-','+').Replace('_','/');$v=$v.PadRight($v.Length+((4-$v.Length%4)%4),'=');return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($v))}
function Sign($claims){
    $h=Encode ([Text.Encoding]::UTF8.GetBytes('{"alg":"HS256","typ":"JWT"}'))
    $p=Encode ([Text.Encoding]::UTF8.GetBytes(($claims|ConvertTo-Json -Compress)))
    $mac=[Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($env:JwtSettings__Key))
    try{return "$h.$p.$(Encode ($mac.ComputeHash([Text.Encoding]::UTF8.GetBytes("$h.$p"))))"}finally{$mac.Dispose()}
}
try{
    $cluster=& $psql -h 127.0.0.1 -p 55432 -U migration_test -d postgres -At -c 'SHOW data_directory'
    Assert ($LASTEXITCODE -eq 0 -and [IO.Path]::GetFullPath($cluster) -eq [IO.Path]::GetFullPath((Join-Path $root '.artifacts/postgres-net10'))) 'No es el cluster temporal esperado.'
    & $psql -h 127.0.0.1 -p 55432 -U migration_test -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $db;"|Out-Null
    if($LASTEXITCODE -ne 0){throw 'Falló creación de base temporal.'}
    foreach($file in @($SchemaPath,(Join-Path $root 'database/001_integridad_base.sql'),(Join-Path $root 'database/002_usuario_seguridad.sql'),(Join-Path $root 'database/003_encuestas.sql'),(Join-Path $root 'database/004_solicitudes_ciudadanas.sql'),(Join-Path $root 'database/005_afiliaciones.sql'),(Join-Path $root 'database/006_correos_transaccionales.sql'))){
        & $psql -h 127.0.0.1 -p 55432 -U migration_test -d $db -v ON_ERROR_STOP=1 -f $file > (Join-Path $root '.artifacts/usuario-schema.log')
        if($LASTEXITCODE -ne 0){throw "Falló $file"}
    }
    $city=[Guid]::NewGuid().ToString()
    Sql "INSERT INTO public.departamentos(nombre) VALUES('Test'); INSERT INTO public.ciudades(id,nombre,departamento_id) SELECT '$city','Test',id FROM public.departamentos WHERE nombre='Test'; INSERT INTO public.roles(nombre) VALUES('Admin');"|Out-Null
    $env:ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=55432;Database=$db;Username=migration_test"
    $env:JwtSettings__Key=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    $env:ASPNETCORE_ENVIRONMENT='Development'
    $env:PdfAntivirus__Host='127.0.0.1';$env:PdfAntivirus__Port='13310'
    $env:RateLimits__General='1000';$env:RateLimits__Login='100';$env:RateLimits__Registro='100';$env:RateLimits__Escritura='1000'
    $env:Gmail__Enabled='false'
    StartApi
    $password='Synthetic-Only-Password!2026'
    $swagger=Invoke-RestMethod "$base/swagger/v1/swagger.json"
    $loginOperation=$swagger.paths.'/api/Usuario/login'.post
    Assert (($loginOperation.PSObject.Properties.Name -contains 'security') -and $loginOperation.security.Count -eq 0) 'Swagger marca login como protegido.'
    Assert ($null -ne $swagger.components.schemas.LoginResponse.properties.token) 'Swagger no documenta el token de login.'
    $a=@{nombre='Ana';apellido='Prueba';email='Ana.Test@example.invalid';password=$password;fechaNacimiento='1990-01-01';ciudadId=$city;telefono='+5493411234567'}
    $b=@{nombre='Beto';apellido='Prueba';email='Beto.Test@example.invalid';password=$password;fechaNacimiento='1991-02-03'}
    $admin=@{nombre='Admin';apellido='Prueba';email='Admin.Test@example.invalid';password=$password;fechaNacimiento='1990-01-01'}
    foreach($account in @($a,$b,$admin)){Call POST '/api/Usuario' $account '' 202|Out-Null}
    $duplicate=$a.Clone();$duplicate.email=$a.email.ToLowerInvariant()
    Call POST '/api/Usuario' $duplicate '' 202|Out-Null
    Assert ((Sql 'SELECT count(*) FROM public.usuarios') -eq '3') 'Se duplicó una cuenta.'
    $bad=$a.Clone();$bad.rolId=[Guid]::NewGuid().ToString()
    Call POST '/api/Usuario' $bad '' 400|Out-Null
    foreach($badPassword in @('short',('é'*40))){$bad=$a.Clone();$bad.password=$badPassword;Call POST '/api/Usuario' $bad '' 400|Out-Null}
    $bad=$a.Clone();$bad.fechaNacimiento='2999-01-01';Call POST '/api/Usuario' $bad '' 400|Out-Null
    $bad=$a.Clone();$bad.ciudadId=[Guid]::NewGuid().ToString();Call POST '/api/Usuario' $bad '' 400|Out-Null
    Call POST '/api/Usuario/login' @{email='absent@example.invalid';password=$password} '' 401|Out-Null
    $ta=(Json (Call POST '/api/Usuario/login' @{email=' ANA.TEST@EXAMPLE.INVALID ';password=$password} '' 200)).token
    $tb=(Json (Call POST '/api/Usuario/login' @{email=$b.email;password=$password} '' 200)).token
    $profile=Json (Call GET '/api/Usuario/me' $null $ta 200)
    $ida=$profile.idUsuario;$idb=(Json (Call GET '/api/Usuario/me' $null $tb 200)).idUsuario
    Assert ($profile.ciudadId -eq $city -and $profile.roleNombre -eq 'Usuario' -and $profile.fechaNacimiento -eq '1990-01-01') 'Mapeo de perfil incompleto.'
    Assert (-not ($profile.PSObject.Properties.Name -contains 'passwordHash') -and -not ($profile.PSObject.Properties.Name -contains 'versionAcceso')) 'Se filtraron credenciales.'
    $claims=Decode ($ta.Split('.')[1])|ConvertFrom-Json -AsHashtable
    Assert (-not $claims.ContainsKey('email') -and -not $claims.ContainsKey('name') -and -not $claims.ContainsKey('imageUrl')) 'JWT expone información innecesaria.'
    Call GET '/api/Usuario/me' $null '' 401|Out-Null
    Call GET '/api/Usuario/me' $null ($ta+'invalid') 401|Out-Null
    $c=$claims.Clone();$c.exp=1;Call GET '/api/Usuario/me' $null (Sign $c) 401|Out-Null
    $c=$claims.Clone();$c.aud='wrong';Call GET '/api/Usuario/me' $null (Sign $c) 401|Out-Null
    $c=$claims.Clone();$c.Remove('ver');Call GET '/api/Usuario/me' $null (Sign $c) 401|Out-Null
    $c=$claims.Clone();$c.role='Admin';Call GET '/api/Usuario/me' $null (Sign $c) 401|Out-Null
    $c=$claims.Clone();$c.sub=$idb;Call GET '/api/Usuario/me' $null (Sign $c) 401|Out-Null
    $c=$claims.Clone();$c.sub='not-a-guid';Call GET '/api/Usuario/me' $null (Sign $c) 401|Out-Null
    Assert ((Json (Call GET "/api/Usuario/me?id=$idb" $null $ta 200)).idUsuario -eq $ida) 'IDOR.'
    Call GET "/api/Usuario/$idb" $null $ta 404|Out-Null
    Call PATCH '/api/Usuario/me' @{nombre='Ana editada'} $ta 204|Out-Null
    $p=Json (Call GET '/api/Usuario/me' $null $ta 200)
    Assert ($p.nombre -eq 'Ana editada' -and $p.apellido -eq 'Prueba' -and $p.telefono -eq $a.telefono) 'PATCH modificó campos omitidos.'
    Call PATCH '/api/Usuario/me' @{telefono=$null;ciudadId=$null;fotoPerfilUrl=$null} $ta 204|Out-Null
    $p=Json (Call GET '/api/Usuario/me' $null $ta 200)
    Assert ($null -eq $p.telefono -and $null -eq $p.ciudadId) 'PATCH no aplicó null.'
    foreach($body in @('{}','{"nombre":null}','{"email":"other@example.invalid"}','{"rolId":null}','{"activo":false}','{"nombre":"A","nombre":"B"}','{"fotoPerfilUrl":"javascript:alert(1)"}')){
        Call PATCH '/api/Usuario/me' $body $ta 400|Out-Null
    }
    Call PATCH '/api/Usuario/me' @{usuarioId=$idb;nombre='Intruso'} $ta 400|Out-Null
    Call PATCH '/api/Usuario/me' @{ciudadId=[Guid]::NewGuid().ToString()} $ta 400|Out-Null
    Assert ((Json (Call GET '/api/Usuario/me' $null $tb 200)).nombre -eq 'Beto') 'Se modificó otro usuario.'
    Call PATCH '/api/Usuario/me' ('{"nombre":"'+('x'*70000)+'"}') $ta 413|Out-Null
    foreach($path in @('/api/Ciudad?nombre=Test','/api/Departamento','/api/Sede')){
        Call POST $path @{} '' 401|Out-Null;Call POST $path @{} $ta 403|Out-Null
    }
    foreach($verb in @('PATCH','DELETE')){
        Call $verb "/api/Sede/$([Guid]::NewGuid())" @{} '' 401|Out-Null
        Call $verb "/api/Sede/$([Guid]::NewGuid())" @{} $ta 403|Out-Null
    }
    Call GET '/api/Sede' $null '' 200|Out-Null
    Sql "UPDATE public.usuarios SET rol_id=(SELECT id FROM public.roles WHERE nombre='Admin') WHERE email='Admin.Test@example.invalid';"|Out-Null
    $at=(Json (Call POST '/api/Usuario/login' @{email=$admin.email;password=$password} '' 200)).token
    Call POST '/api/Departamento' '"Departamento admin"' $at 200|Out-Null
    . (Join-Path $PSScriptRoot 'verify-encuesta-cases.ps1')
    $emptyAffiliations=Json (Call GET '/api/SolicitudAfiliacion/totales' $null $at 200)
    Assert ($emptyAffiliations.totalAfiliados -eq 0 -and $emptyAffiliations.totalSolicitudes -eq 0) 'Los totales vacíos no son cero.'
    Call POST '/api/Sede' @{nombre='Sede test';direccion='Direccion original'} $at 200|Out-Null
    $sedeId=Sql "SELECT id FROM public.sedes WHERE nombre='Sede test'"
    Call PATCH "/api/Sede/$sedeId" @{direccion='Direccion nueva'} $at 200|Out-Null
    Assert ((Sql "SELECT direccion FROM public.sedes WHERE id='$sedeId'") -eq 'Direccion nueva') 'PATCH de sede no persistido.'
    Call PATCH "/api/Sede/$([Guid]::NewGuid())" @{direccion='Otra'} $at 404|Out-Null
    Call DELETE "/api/Sede/$sedeId" $null $at 200|Out-Null
    Call DELETE "/api/Sede/$sedeId" $null $at 404|Out-Null
    Sql "UPDATE public.usuarios SET rol_id=(SELECT id FROM public.roles WHERE nombre='Usuario') WHERE email='Admin.Test@example.invalid';"|Out-Null
    Call POST '/api/Departamento' '"No permitido"' $at 401|Out-Null
    $survey=[Guid]::NewGuid().ToString();$race=[Guid]::NewGuid().ToString();$closed=[Guid]::NewGuid().ToString();$future=[Guid]::NewGuid().ToString()
    $config='{"version":1,"preguntas":[{"id":"p1","texto":"Elegi","tipo":"opcion_unica","obligatoria":true,"opciones":[{"id":"a","texto":"A"},{"id":"b","texto":"B"}]},{"id":"p2","texto":"Varias","tipo":"opcion_multiple","opciones":[{"id":"a","texto":"A"},{"id":"b","texto":"B"}]},{"id":"p3","texto":"Texto","tipo":"texto","maxLongitud":20},{"id":"p4","texto":"Numero","tipo":"numero","min":1,"max":5}]}'
    foreach($id in @($survey,$race,$closed,$future)){Sql "INSERT INTO public.encuestas(id,titulo,tipo,configuracion,publicada_at,activa,fecha_inicio) VALUES('$id','Test','general','$config',now(),'$($id -ne $closed)',CASE WHEN '$id'='$future' THEN CURRENT_DATE+10 ELSE NULL END);"|Out-Null}

    $answer=@{respuestas=@{p1='a';p2=@('a','b');p3='Texto';p4=3}}
    Call POST "/api/Usuario/me/encuestas/$survey/respuesta" $answer '' 401|Out-Null
    Call POST "/api/Usuario/me/encuestas/$closed/respuesta" $answer $ta 409|Out-Null
    Call POST "/api/Usuario/me/encuestas/$future/respuesta" $answer $ta 409|Out-Null
    Call POST "/api/Usuario/me/encuestas/$([Guid]::NewGuid())/respuesta" $answer $ta 404|Out-Null
    foreach($ans in @(@{p1='unknown'},@{p2=@('a')},@{p1='a';p4=9},@{p1='a';p2=@('a','a')},@{p1='a';extra='x'},@{p1='a';p3=('x'*21)})){
        Call POST "/api/Usuario/me/encuestas/$survey/respuesta" @{respuestas=$ans} $ta 400|Out-Null
    }
    Call POST "/api/Usuario/me/encuestas/$survey/respuesta" @{respuestas=@{p1='a'};usuarioId=$idb} $ta 400|Out-Null
    foreach($body in @('null','{"respuestas":null}','{"respuestas":{"p1":null}}','{"respuestas":{"p1":"a","p1":"b"}}')){
        Call POST "/api/Usuario/me/encuestas/$survey/respuesta" $body $ta 400|Out-Null
    }
    $malformed=[Guid]::NewGuid().ToString()
    $invalidConfig='{"version":1,"preguntas":[null]}'
    Sql "INSERT INTO public.encuestas(id,titulo,tipo,configuracion,publicada_at,activa) VALUES('$malformed','Test','general','$invalidConfig',now(),true);"|Out-Null
    Call POST "/api/Usuario/me/encuestas/$malformed/respuesta" $answer $ta 409|Out-Null
    Call POST "/api/Usuario/me/encuestas/$survey/respuesta" $answer $ta 201|Out-Null
    Call POST "/api/Usuario/me/encuestas/$survey/respuesta" $answer $ta 409|Out-Null
    Call POST "/api/Usuario/me/encuestas/$survey/respuesta" $answer $tb 201|Out-Null
    ConcurrentPost "/api/Usuario/me/encuestas/$race/respuesta" $answer $ta
    Assert ((Sql "SELECT count(*) FROM public.encuesta_respuestas WHERE encuesta_id='$race' AND usuario_id='$ida'") -eq '1') 'Respuesta duplicada.'
    Call POST '/api/Usuario/me/afiliacion' @{confirmacion=$false} $ta 400|Out-Null
    Call POST '/api/Usuario/me/afiliacion' @{confirmacion=$true;estado='aprobada'} $ta 400|Out-Null
    ConcurrentPost '/api/Usuario/me/afiliacion' @{confirmacion=$true} $ta
    Assert ((Sql "SELECT count(*) FROM public.solicitudes_afiliacion WHERE usuario_id='$ida'") -eq '1') 'Afiliación duplicada.'
    Sql "UPDATE public.solicitudes_afiliacion SET estado='rechazada' WHERE usuario_id='$ida';"|Out-Null
    Call POST '/api/Usuario/me/afiliacion' @{confirmacion=$true} $ta 409|Out-Null
    Call POST '/api/Usuario/me/afiliacion' @{confirmacion=$true} $tb 201|Out-Null
    Call POST '/api/Usuario/me/solicitudes' @{motivo='duda';mensaje='Una duda de prueba para el partido.'} $ta 201|Out-Null
    Call POST '/api/Usuario/me/solicitudes' @{motivo='invalid';mensaje='Texto suficiente para validar.'} $ta 400|Out-Null
    Call POST '/api/Usuario/me/solicitudes' @{motivo='proyecto';mensaje='Texto suficiente';usuarioId=$idb} $ta 400|Out-Null
    Call POST '/api/Usuario/me/solicitudes' @{motivo='sugerencia';mensaje="Texto '; DROP TABLE usuarios; --"} $ta 201|Out-Null
    Assert ((Sql 'SELECT count(*) FROM public.usuarios') -eq '3') 'Inyección en mensaje.'
    . (Join-Path $PSScriptRoot 'verify-solicitud-cases.ps1')
    . (Join-Path $PSScriptRoot 'verify-afiliacion-cases.ps1')
    . (Join-Path $PSScriptRoot 'verify-noticia-cases.ps1')
    . (Join-Path $PSScriptRoot 'verify-notificacion-cases.ps1')
    for($i=0;$i -lt 5;$i++){Call POST '/api/Usuario/login' @{email=$b.email;password='Wrong-password-123'} '' 401|Out-Null}
    Call POST '/api/Usuario/login' @{email=$b.email;password=$password} '' 401|Out-Null
    Assert ((Sql "SELECT intentos_fallidos FROM public.usuarios WHERE id='$idb'") -eq '5') 'Contador incorrecto.'
    Sql "UPDATE public.usuarios SET bloqueado_hasta=now()-interval '1 minute' WHERE id='$idb';"|Out-Null
    $tb=(Json (Call POST '/api/Usuario/login' @{email=$b.email;password=$password} '' 200)).token
    Assert ((Sql "SELECT intentos_fallidos FROM public.usuarios WHERE id='$idb'") -eq '0') 'No se reiniciaron los fallos.'
    Call DELETE '/api/Usuario/me' $null $ta 204|Out-Null
    Call GET '/api/Usuario/me' $null $ta 401|Out-Null
    Call POST '/api/Usuario/me/solicitudes' @{motivo='duda';mensaje='No debe enviarse.'} $ta 401|Out-Null
    Call POST '/api/Usuario/login' @{email=$a.email;password=$password} '' 401|Out-Null
    Call POST '/api/Usuario' $a '' 202|Out-Null
    Assert ((Sql "SELECT activo FROM public.usuarios WHERE id='$ida'") -eq 'f') 'Cuenta reactivada indebidamente.'
    Sql "UPDATE public.usuarios SET activo=true,deleted_at=null WHERE id='$ida';"|Out-Null
    Call GET '/api/Usuario/me' $null $ta 401|Out-Null
    StopApi
    $env:RateLimits__Login='2';$env:RateLimits__Registro='2';$env:RateLimits__Escritura='2'
    $env:Gmail__Enabled='false'
    StartApi
    1..2|ForEach-Object{Call POST '/api/Usuario/login' @{email='none@example.invalid';password=$password} '' 401|Out-Null}
    $limited=Call POST '/api/Usuario/login' @{email='none@example.invalid';password=$password} '' 429
    Assert ($limited.Headers.ContainsKey('Retry-After')) 'Falta Retry-After.'
    1..2|ForEach-Object{Call POST '/api/Usuario' @{} '' 400|Out-Null}
    Call POST '/api/Usuario' @{} '' 429|Out-Null
    1..2|ForEach-Object{Call PATCH '/api/Usuario/me' @{nombre='Beto'} $tb 204|Out-Null}
    Call PATCH '/api/Usuario/me' @{nombre='Beto'} $tb 429|Out-Null
    Write-Output "OK: $script:checks comprobaciones de Usuario, Encuesta, Solicitud ciudadana, Afiliacion, Noticias y Notificaciones. Base temporal: $db"
}finally{
    StopApi
    foreach($key in $old.Keys){[Environment]::SetEnvironmentVariable($key,$old[$key])}
}









