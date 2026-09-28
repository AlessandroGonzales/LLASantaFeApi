# Se ejecuta dentro de verify-usuario.ps1 con usuarios sintéticos y DB temporal.
function PdfCall($id,$token,[byte[]]$bytes,$status,$filename='documento.pdf',$type='application/pdf',$extra=$false,$field='archivo') {
    $client=[Net.Http.HttpClient]::new()
    $multi=[Net.Http.MultipartFormDataContent]::new()
    try {
        if($token){$client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token)}
        $file=[Net.Http.ByteArrayContent]::new($bytes)
        $file.Headers.ContentType=[Net.Http.Headers.MediaTypeHeaderValue]::new($type)
        $multi.Add($file,$field,$filename)
        if($extra){$multi.Add([Net.Http.ByteArrayContent]::new($bytes),'archivo','otro.pdf')}
        $r=$client.PostAsync("$base/api/SolicitudCiudadana/$id/pdf",$multi).GetAwaiter().GetResult()
        $body=$r.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        Assert ([int]$r.StatusCode -eq $status) "PDF esperaba $status, obtuvo $($r.StatusCode): $body"
        $r.Dispose()
    } finally {$multi.Dispose();$client.Dispose()}
}
function TestPdf($marker) {
    $doc="%PDF-1.4`n%$marker`n";$offsets=@(0)
    $objects=@('<< /Type /Catalog /Pages 2 0 R >>','<< /Type /Pages /Kids [3 0 R] /Count 1 >>','<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << >> /Contents 4 0 R >>',"<< /Length 0 >>`nstream`nendstream")
    for($i=0;$i -lt $objects.Count;$i++){$offsets+=$doc.Length;$doc+="$($i+1) 0 obj`n$($objects[$i])`nendobj`n"}
    $start=$doc.Length;$doc+="xref`n0 5`n0000000000 65535 f `n"
    foreach($offset in $offsets[1..4]){$doc+=$offset.ToString('D10')+" 00000 n `n"}
    $doc+="trailer`n<< /Size 5 /Root 1 0 R >>`nstartxref`n$start`n%%EOF`n"
    return [Text.Encoding]::ASCII.GetBytes($doc)
}
$clean=TestPdf 'CLEAN';$badPdf=TestPdf 'SCANNER_FOUND'
$scanProcess=Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @('-NoProfile','-File',(Join-Path $PSScriptRoot 'test-clamd-protocol.ps1')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root '.artifacts/clamd-test.stdout.log') -RedirectStandardError (Join-Path $root '.artifacts/clamd-test.stderr.log')
try {
    Sql "UPDATE public.usuarios SET rol_id=(SELECT id FROM public.roles WHERE nombre='Admin') WHERE email='Admin.Test@example.invalid';"|Out-Null
    $at=(Json (Call POST '/api/Usuario/login' @{email=$admin.email;password=$password} '' 200)).token
    $request=@{motivo='proyecto';mensaje='Proyecto formal de prueba con adjunto opcional.'}
    Call POST '/api/SolicitudCiudadana' $request '' 401|Out-Null
    foreach($oldReason in @('propuesta','inquietud','otro','Tengo un Proyecto / Propuesta formal')) {
        $invalid=$request.Clone();$invalid.motivo=$oldReason
        Call POST '/api/SolicitudCiudadana' $invalid $ta 400|Out-Null
    }
    Call GET '/api/SolicitudCiudadana/administracion' $null $ta 403|Out-Null
    $sc=(Json (Call POST '/api/SolicitudCiudadana' $request $ta 201)).id
    Call POST '/api/SolicitudCiudadana' $request $ta 429|Out-Null
    Call POST '/api/Usuario/me/solicitudes' $request $ta 429|Out-Null
    $volunteer=$request.Clone();$volunteer.motivo='voluntariado'
    $sb=(Json (Call POST '/api/SolicitudCiudadana' $volunteer $tb 201)).id
    $complaint=$request.Clone();$complaint.motivo='denuncia'
    $sadmin=(Json (Call POST '/api/SolicitudCiudadana' $complaint $at 201)).id
    Assert ((Sql "SELECT count(DISTINCT motivo) FROM public.solicitudes_ciudadanas") -eq '5') 'No se guardaron los cinco motivos del frontend.'
    Call GET "/api/SolicitudCiudadana/$sc" $null $tb 404|Out-Null
    Call GET "/api/SolicitudCiudadana/$sc/pdf" $null $tb 404|Out-Null
    Call GET "/api/SolicitudCiudadana/$sc" $null $at 200|Out-Null
    PdfCall $sc '' $clean 401
    PdfCall $sc $tb $clean 404
    PdfCall $sc $ta $clean 400 'wrong.txt'
    PdfCall $sc $ta $badPdf 400
    PdfCall $sc $ta $clean 201 -field 'Archivo'
    PdfCall $sc $ta $clean 409
    $download=Call GET "/api/SolicitudCiudadana/$sc/pdf" $null $ta 200
    Assert ($download.Headers.'Content-Disposition' -match 'attachment') 'PDF se sirve inline.'
    Assert ($download.Headers.'X-Content-Type-Options' -eq 'nosniff') 'Falta nosniff.'
    Call GET "/api/SolicitudCiudadana/$sc/pdf" $null $tb 404|Out-Null
    Call GET "/api/SolicitudCiudadana/$sc/pdf" $null $at 200|Out-Null
    $detail=Json (Call GET "/api/SolicitudCiudadana/$sc" $null $ta 200)
    Assert ($detail.tienePdf -and $detail.revision -eq 1 -and $null -eq $detail.pdfUrl) 'Metadata PDF incorrecta.'
    Call PATCH "/api/SolicitudCiudadana/$sc/gestion" @{revision=1;estado='en_revision'} $ta 403|Out-Null
    Call PATCH "/api/SolicitudCiudadana/$sc/gestion" @{revision=1;estado='resuelta';respuesta='corta'} $at 400|Out-Null
    Call PATCH "/api/SolicitudCiudadana/$sc/gestion" @{revision=1;estado='en_revision'} $at 204|Out-Null
    Call PATCH "/api/SolicitudCiudadana/$sc/gestion" @{revision=1;estado='resuelta';respuesta='Respuesta formal de prueba.'} $at 409|Out-Null
    Call PATCH "/api/SolicitudCiudadana/$sc/gestion" @{revision=2;estado='resuelta';respuesta='Respuesta formal de prueba.'} $at 204|Out-Null
    Call PATCH "/api/SolicitudCiudadana/$sc/gestion" @{revision=3;estado='en_revision'} $at 409|Out-Null
    $detail=Json (Call GET "/api/SolicitudCiudadana/$sc" $null $ta 200)
    Assert ($detail.estado -eq 'resuelta' -and $detail.respuesta -eq 'Respuesta formal de prueba.') 'No se ve la respuesta administrativa.'
    Assert ((Sql "SELECT aceptacion FROM public.solicitudes_ciudadanas WHERE id='$sc'") -eq 't') 'Aceptacion legacy inconsistente.'
    $page=Json (Call GET '/api/SolicitudCiudadana?limite=1' $null $ta 200)
    $next=Json (Call GET "/api/SolicitudCiudadana?limite=1&cursor=$($page.siguienteCursor)" $null $ta 200)
    Assert ($page.items[0].id -ne $next.items[0].id -and $null -eq $page.items[0].mensaje) 'Paginación incorrecta.'
    Call GET '/api/SolicitudCiudadana?limite=51' $null $ta 400|Out-Null
    Call GET '/api/SolicitudCiudadana?estado=invalid' $null $ta 400|Out-Null
    Call GET '/api/SolicitudCiudadana/administracion?estado=resuelta' $null $at 200|Out-Null
    # Tres intentos incluso cuando el cuerpo es inválido; no llegan al antivirus.
    PdfCall $sb $tb ([Text.Encoding]::ASCII.GetBytes('invalid content longer than twenty bytes')) 400
    PdfCall $sb $tb $clean 400 'documento.pdf' 'application/pdf' $true
    PdfCall $sb $tb ([byte[]]::new(2097153)) 413
    PdfCall $sb $tb $clean 429
    # Tres altas concurrentes cuando queda cupo para dos: solo dos se confirman.
    $client=[Net.Http.HttpClient]::new()
    $contents=@()
    try {
        $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$tb)
        $json=$request|ConvertTo-Json -Compress
        $tasks=@(1..3|ForEach-Object {
            $c=[Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json');$contents+=$c
            $client.PostAsync("$base/api/SolicitudCiudadana",$c)
        })
        [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks)
        $codes=@($tasks|ForEach-Object{[int]$_.Result.StatusCode}|Sort-Object)
        Assert (($codes -join ',') -eq '201,201,429') 'La cuota de solicitudes falla con concurrencia.'
        foreach($t in $tasks){$t.Result.Dispose()}
    } finally {foreach($c in $contents){$c.Dispose()};$client.Dispose()}
    Assert ((Sql "SELECT count(*) FROM public.solicitudes_ciudadanas WHERE usuario_id='$idb'") -eq '3') 'Cupo persistido incorrecto.'
    # Las descargas se limitan por cuenta, además de la política por IP.
    $limited=$false
    for($i=0;$i -lt 21;$i++) {
        $r=Invoke-WebRequest "$base/api/SolicitudCiudadana/$sc/pdf" -Headers @{Authorization="Bearer $ta"} -SkipHttpErrorCheck
        if($r.StatusCode -eq 429){$limited=$true;break}
        Assert ($r.StatusCode -eq 200) 'Fallo inesperado al descargar.'
    }
    Assert $limited 'Falta cuota de descargas por cuenta.'
    Assert ($r.Headers.ContainsKey('Retry-After')) 'Falta Retry-After para transferencias.'
    # La indisponibilidad del antivirus nunca libera ni persiste archivos.
    PdfCall $sadmin $at $clean 201 -field 'archivo'
    $sadmin=(Json (Call POST '/api/SolicitudCiudadana' $complaint $at 201)).id
    Stop-Process -Id $scanProcess.Id;$scanProcess.WaitForExit()
    PdfCall $sadmin $at $clean 503
    Assert ((Sql "SELECT count(*) FROM public.solicitud_pdf WHERE solicitud_id='$sadmin'") -eq '0') 'Se guardó un archivo sin antivirus.'
    Call GET "/api/SolicitudCiudadana/$sadmin" $null $at 200|Out-Null
} finally {
    if(-not $scanProcess.HasExited){Stop-Process -Id $scanProcess.Id}
}



