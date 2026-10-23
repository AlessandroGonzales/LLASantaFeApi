# Ejecutado por verify-usuario.ps1 únicamente en su base temporal, con datos sintéticos.
$geoStartChecks=$script:checks
$geoDepartments=Call GET '/api/Departamento' $null '' 200
$geoRows=@(Json $geoDepartments)
Assert ($geoRows.Count -gt 0) 'No se devolvieron los departamentos de prueba.'
Assert ($geoDepartments.Headers['Cache-Control'] -match 'public') 'Departamentos anónimos sin caché pública.'
Assert ($geoDepartments.Headers['Vary'] -match 'Origin') 'Departamentos no separan orígenes HTTP.'
Assert ((($geoRows[0].PSObject.Properties.Name | Sort-Object) -join ',') -eq 'idDepartamento,nombre') 'Contrato de departamento expone campos extra.'
Assert (($geoRows.idDepartamento -join ',') -eq ((Sql 'SELECT id FROM public.departamentos ORDER BY nombre,id') -join ',')) 'Departamentos desordenados o IDs distintos a DB.'
$geoRepeated=Call GET '/api/Departamento' $null '' 200
Assert ($geoRepeated.Content -eq $geoDepartments.Content) 'IDs de departamentos inestables entre consultas.'
$geoAuthenticated=Call GET '/api/Departamento' $null $ta 200
Assert ($geoAuthenticated.Headers['Cache-Control'] -match 'no-store') 'Departamentos con JWT entraron en caché compartida.'
$geoExtra=Call GET '/api/Departamento?extra=1' $null '' 200
Assert ($geoExtra.Headers['Cache-Control'] -match 'no-store') 'Query extra de departamentos habilita caché.'
Assert ($swagger.paths.'/api/Departamento'.get.security.Count -eq 0) 'Swagger exige token para departamentos.'
Assert ($null -ne $swagger.components.schemas.DepartamentoResponse.properties.idDepartamento) 'Swagger no documenta el ID del departamento.'
Assert ($null -ne $swagger.components.schemas.CiudadResponse.properties.idCiudad) 'Swagger no documenta el ID de ciudad.'

Call POST '/api/Departamento' '"Geo Zulu"' '' 401 | Out-Null
Call POST '/api/Departamento' '"Geo Zulu"' $ta 403 | Out-Null
foreach($geoName in @('""','"   "',('x'*101 | ConvertTo-Json -Compress),('Geo'+[char]10+'Test' | ConvertTo-Json -Compress))) {
    $geoInvalid=Call POST '/api/Departamento' $geoName $at 400
    Assert ($geoInvalid.Headers['Cache-Control'] -match 'no-store') 'Error de creación cacheable.'
}
Call POST '/api/Departamento' '"  Geo Zulu  "' $at 200 | Out-Null
Call POST '/api/Departamento' '"Geo Alfa"' $at 200 | Out-Null
$geoAfter=@(Json (Call GET '/api/Departamento' $null '' 200))
$geoZulu=($geoAfter | Where-Object nombre -eq 'Geo Zulu').idDepartamento
$geoAlfa=($geoAfter | Where-Object nombre -eq 'Geo Alfa').idDepartamento
Assert ($geoZulu -and $geoAlfa) 'Alta no normaliza el nombre o no invalida la caché de departamentos.'
Assert (($geoAfter.idDepartamento -join ',') -eq ((Sql 'SELECT id FROM public.departamentos ORDER BY nombre,id') -join ',')) 'Orden de departamentos incorrecto después del alta.'
Call POST '/api/Departamento' '" Geo Zulu "' $at 409 | Out-Null
Assert ((Sql "SELECT count(*) FROM public.departamentos WHERE nombre='Geo Zulu'") -eq '1') 'Duplicado de departamento persistido.'
Assert (@(Json (Call GET "/api/Ciudad?departamentoId=$geoZulu" $null '' 200)).Count -eq 0) 'Departamento sin ciudades no devuelve lista vacía.'
$geoUnknown=[Guid]::NewGuid().ToString()
Assert (@(Json (Call GET "/api/Ciudad?departamentoId=$geoUnknown" $null '' 200)).Count -eq 0) 'Filtro sin coincidencias no devuelve lista vacía.'
foreach($geoBadPath in @('/api/Ciudad','/api/Ciudad?departamentoId=','/api/Ciudad?departamentoId=invalid','/api/Ciudad?departamentoId=00000000-0000-0000-0000-000000000000')) {
    $geoBad=Call GET $geoBadPath $null '' 400
    Assert ($geoBad.Headers['Cache-Control'] -match 'no-store') 'Filtro de ciudades inválido cacheado.'
}

Call POST "/api/Ciudad?nombre=Geo%20Sur&departamentoId=$geoZulu" $null '' 401 | Out-Null
Call POST "/api/Ciudad?nombre=Geo%20Sur&departamentoId=$geoZulu" $null $ta 403 | Out-Null
Call POST "/api/Ciudad?nombre=Geo%20Sur&departamentoId=$geoUnknown" $null $at 400 | Out-Null
Call POST '/api/Ciudad?nombre=Geo%20Sur&departamentoId=00000000-0000-0000-0000-000000000000' $null $at 400 | Out-Null
foreach($geoBadName in @('   ',('x'*101),('Geo'+[char]9+'Test'))) {
    $geoEncoded=[Uri]::EscapeDataString($geoBadName)
    Call POST "/api/Ciudad?nombre=$geoEncoded&departamentoId=$geoZulu" $null $at 400 | Out-Null
}
Call POST "/api/Ciudad?nombre=%20Geo%20Sur%20&departamentoId=$geoZulu" $null $at 200 | Out-Null
Call POST "/api/Ciudad?nombre=Geo%20Norte&departamentoId=$geoZulu" $null $at 200 | Out-Null
$geoCities=@(Json (Call GET "/api/Ciudad?departamentoId=$geoZulu" $null '' 200))
Assert ($geoCities.Count -eq 2) 'Alta no invalida lista vacía de ciudades o mezcla departamentos.'
Assert (($geoCities.nombre -join ',') -eq 'Geo Norte,Geo Sur') 'Ciudades sin orden estable por nombre.'
Assert ((($geoCities[0].PSObject.Properties.Name | Sort-Object) -join ',') -eq 'departamentoId,idCiudad,nombre') 'Contrato de ciudad expone datos extra.'
Assert (@($geoCities | Where-Object departamentoId -ne $geoZulu).Count -eq 0) 'Respuesta mezcla departamentos.'
Assert (($geoCities.idCiudad -join ',') -eq ((Sql "SELECT id FROM public.ciudades WHERE departamento_id='$geoZulu' ORDER BY nombre,id") -join ',')) 'IDs de ciudades no corresponden a DB.'
Call POST "/api/Ciudad?nombre=Geo%20Sur&departamentoId=$geoZulu" $null $at 409 | Out-Null
Call POST "/api/Ciudad?nombre=Geo%20Sur&departamentoId=$geoAlfa" $null $at 200 | Out-Null
$geoOther=@(Json (Call GET "/api/Ciudad?departamentoId=$geoAlfa" $null '' 200))
Assert ($geoOther.Count -eq 1 -and $geoOther[0].idCiudad -ne $geoCities[1].idCiudad) 'Nombres iguales en departamentos distintos comparten identidad.'
$geoAgain=@(Json (Call GET "/api/Ciudad?departamentoId=$geoZulu" $null '' 200))
Assert (($geoAgain.idCiudad -join ',') -eq ($geoCities.idCiudad -join ',')) 'Crear otra ciudad cambió IDs existentes.'

# Se usa un UUID obtenido del contrato público, sin inventar referencias en React.
$geoSelected=$geoCities[0].idCiudad
Call PATCH '/api/Usuario/me' @{ciudadId=$geoSelected} $ta 204 | Out-Null
Assert ((Json (Call GET '/api/Usuario/me' $null $ta 200)).ciudadId -eq $geoSelected) 'Perfil no acepta el ID público de ciudad.'
Call PATCH '/api/Usuario/me' @{ciudadId=[Guid]::NewGuid().ToString()} $ta 400 | Out-Null
Assert ((Json (Call GET '/api/Usuario/me' $null $ta 200)).ciudadId -eq $geoSelected) 'Una ciudad inexistente cambió el perfil.'
Call PATCH '/api/Usuario/me' @{ciudadId=$city} $ta 204 | Out-Null
Write-Output "OK: $($script:checks-$geoStartChecks) comprobaciones del catálogo geográfico."
