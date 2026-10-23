# Ejecutado únicamente por verify-usuario.ps1 contra su base temporal.
$catalogDepartment=Sql "SELECT departamento_id FROM public.ciudades WHERE id='$city'"
$catalogPath="/api/Ciudad?departamentoId=$catalogDepartment"
$catalogFirst=Call GET $catalogPath $null '' 200
$catalogAgain=Call GET $catalogPath $null '' 200
Assert ($catalogAgain.Content -eq $catalogFirst.Content) 'El catálogo cacheado cambia sin modificación.'
Assert ($catalogAgain.Headers['Cache-Control'] -match 'public') 'El catálogo anónimo no es cacheable.'
$catalogAuth=Call GET $catalogPath $null $ta 200
Assert ($catalogAuth.Headers['Cache-Control'] -match 'no-store') 'El catálogo con JWT entró en caché compartida.'
$catalogExtra=Call GET ($catalogPath+'&extra=1') $null '' 200
Assert ($catalogExtra.Headers['Cache-Control'] -match 'no-store') 'Query arbitraria genera claves de caché.'
$catalogError=Call GET '/api/Ciudad?departamentoId=invalid' $null '' 400
Assert ($catalogError.Headers['Cache-Control'] -match 'no-store') 'Se permite cachear un error de validación.'
$newCityName='Cache-City-'+[Guid]::NewGuid().ToString('N')
Call POST "/api/Ciudad?nombre=$newCityName&departamentoId=$catalogDepartment" $null $at 200 | Out-Null
$catalogChanged=Json (Call GET $catalogPath $null '' 200)
Assert ($catalogChanged.nombre -contains $newCityName) 'Crear ciudad no invalida el catálogo.'
$sedeName='Cache-Sede-'+[Guid]::NewGuid().ToString('N')
$sedeRequest=@{nombre=$sedeName;direccion='Direccion de prueba';ciudadId=$city}
Call GET '/api/Sede' $null '' 200 | Out-Null
Call POST '/api/Sede' $sedeRequest $at 200 | Out-Null
$sedeId=Sql "SELECT id FROM public.sedes WHERE nombre='$sedeName'"
$sedeList=Call GET '/api/Sede' $null '' 200
Assert ((Json $sedeList).nombre -contains $sedeName) 'Crear sede no invalida el catálogo.'
Assert ($sedeList.Headers['Cache-Control'] -match 'public') 'Sedes anónimas no son cacheables.'
Call PATCH "/api/Sede/$sedeId" @{direccion='Direccion actualizada'} $at 200 | Out-Null
$sedeAfter=Json (Call GET '/api/Sede' $null '' 200)
Assert (($sedeAfter | Where-Object nombre -eq $sedeName).direccion -eq 'Direccion actualizada') 'PATCH sirve dirección obsoleta.'
Call DELETE "/api/Sede/$sedeId" $null $at 200 | Out-Null
Assert (-not ((Json (Call GET '/api/Sede' $null '' 200)).nombre -contains $sedeName)) 'DELETE conserva sede en caché.'
$privateProfile=Call GET '/api/Usuario/me' $null $ta 200
Assert ($privateProfile.Headers['Cache-Control'] -match 'no-store') 'Perfil privado cacheable.'
