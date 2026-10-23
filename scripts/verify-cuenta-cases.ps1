# Casos de identidad contra la DB desechable de verify-usuario.ps1; Gmail deshabilitado.
$accountRequest=@{nombre='Cuenta';apellido='Prueba';email='cuenta@example.invalid';password=$password;fechaNacimiento='1990-01-01'}
Call POST '/api/Usuario' $accountRequest '' 202 | Out-Null
$accountId=Sql "SELECT id FROM public.usuarios WHERE email='cuenta@example.invalid'"
$verifyCode=CuentaCodigo $accountRequest.email 'email_verificacion'
$verifyId=$verifyCode.Split('.')[0]
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='bienvenida' AND usuario_id='$accountId'") -eq '0') 'Bienvenida antes de probar email.'
Assert ((Sql "SELECT requiere_verificar_email FROM public.usuarios WHERE id='$accountId'") -eq 't') 'Cuenta nueva evita verificación.'
$notVerified=Call POST '/api/Usuario/login' @{email=$accountRequest.email;password=$password} '' 403
Assert ((Json $notVerified).code -eq 'email_not_verified') "No hay código de error para React: $($notVerified.Content)"
Call POST '/api/Usuario/login' @{email=$accountRequest.email;password='Incorrect-Synthetic-Password'} '' 401 | Out-Null
Call GET '/api/Usuario/email/confirmar' $null '' 401 | Out-Null
$confirm=@{email=$accountRequest.email;password=$password;codigo=$verifyCode}
foreach($invalid in @(@{email='otro@example.invalid';password=$password;codigo=$verifyCode},@{email=$accountRequest.email;password='Incorrect-Synthetic-Password';codigo=$verifyCode},@{email=$accountRequest.email;password=$password;codigo=('x'*80)},@{email=$accountRequest.email;password=$password;codigo=$verifyCode;emailVerificado=$true})){
    Call POST '/api/Usuario/email/confirmar' $invalid '' 400 | Out-Null
}
$beforeMail=Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='email_verificacion' AND usuario_id='$accountId'"
$known=Call POST '/api/Usuario/email/solicitar-verificacion' @{email=$accountRequest.email} '' 202
$unknown=Call POST '/api/Usuario/email/solicitar-verificacion' @{email='ausente@example.invalid'} '' 202
Assert ($known.Content -eq $unknown.Content) 'Verificación enumera cuentas.'
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='email_verificacion' AND usuario_id='$accountId'") -eq $beforeMail) 'Reenvío bombardea correo o invalida enlace vigente.'
Assert ((CuentaCodigo $accountRequest.email 'email_verificacion') -eq $verifyCode) 'Repetición sustituyó código válido.'
Sql "UPDATE public.usuario_acciones SET vence_at=now()-interval '1 second' WHERE id='$verifyId'" | Out-Null
Call POST '/api/Usuario/email/confirmar' $confirm '' 400 | Out-Null
Sql "UPDATE public.usuario_acciones SET vence_at=now()+interval '30 minutes' WHERE id='$verifyId'" | Out-Null
ConcurrentPost '/api/Usuario/email/confirmar' $confirm '' '204,400'
Call POST '/api/Usuario/email/confirmar' $confirm '' 400 | Out-Null
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='bienvenida' AND usuario_id='$accountId'") -eq '1') 'Confirmación duplicó/omitió bienvenida.'
$accountToken=(Json (Call POST '/api/Usuario/login' @{email=$accountRequest.email;password=$password} '' 200)).token
$accountToken2=(Json (Call POST '/api/Usuario/login' @{email=$accountRequest.email;password=$password} '' 200)).token
$accountProfile=Json (Call GET '/api/Usuario/me' $null $accountToken 200)
Assert ($accountProfile.emailVerificado -and $accountProfile.requiereVerificarEmail) 'Perfil no informa prueba de email.'
Call GET '/api/Encuesta/administracion' $null $accountToken 403 | Out-Null
$oldVersion=Sql "SELECT version_acceso FROM public.usuarios WHERE id='$accountId'"
$forgotKnown=Call POST '/api/Usuario/password/solicitar-recuperacion' @{email=$accountRequest.email} '' 202
$forgotUnknown=Call POST '/api/Usuario/password/solicitar-recuperacion' @{email='ausente@example.invalid'} '' 202
Assert ($forgotKnown.Content -eq $forgotUnknown.Content) 'Recuperación enumera cuentas.'
Assert ((Sql "SELECT version_acceso FROM public.usuarios WHERE id='$accountId'") -eq $oldVersion) 'Solicitar recuperación cerró sesiones antes de probar código.'
$resetCode=CuentaCodigo $accountRequest.email 'password_recuperacion'
$resetId=$resetCode.Split('.')[0]
Call POST '/api/Usuario/password/solicitar-recuperacion' @{email=$accountRequest.email} '' 202 | Out-Null
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='password_recuperacion' AND referencia_id='$resetId'") -eq '1') 'Recuperación repetida envía otro correo.'
$newPassword='New-Synthetic-Password!2026'
foreach($bad in @(@{codigo=$verifyCode;nuevaPassword=$newPassword},@{codigo=$resetCode;nuevaPassword='short'},@{codigo=$resetCode;nuevaPassword=('é'*40)},@{codigo=$resetCode;nuevaPassword=$newPassword;usuarioId=$ida},@{codigo=('x'*80);nuevaPassword=$newPassword})){
    Call POST '/api/Usuario/password/restablecer' $bad '' 400 | Out-Null
}
Sql "UPDATE public.usuario_acciones SET vence_at=now()-interval '1 second' WHERE id='$resetId'" | Out-Null
Call POST '/api/Usuario/password/restablecer' @{codigo=$resetCode;nuevaPassword=$newPassword} '' 400 | Out-Null
Sql "UPDATE public.usuario_acciones SET vence_at=now()+interval '30 minutes' WHERE id='$resetId'" | Out-Null
ConcurrentPost '/api/Usuario/password/restablecer' @{codigo=$resetCode;nuevaPassword=$newPassword} '' '204,400'
Call POST '/api/Usuario/password/restablecer' @{codigo=$resetCode;nuevaPassword=$newPassword} '' 400 | Out-Null
Call GET '/api/Usuario/me' $null $accountToken 401 | Out-Null
Call GET '/api/Usuario/me' $null $accountToken2 401 | Out-Null
Call POST '/api/Usuario/login' @{email=$accountRequest.email;password=$password} '' 401 | Out-Null
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='password_modificada' AND referencia_id='$resetId'") -eq '1') 'Falta aviso de contraseña actualizada.'
$freshToken=(Json (Call POST '/api/Usuario/login' @{email=$accountRequest.email;password=$newPassword} '' 200)).token
Call POST '/api/Usuario/me/cerrar-sesion' $null '' 401 | Out-Null
Call POST '/api/Usuario/password/solicitar-recuperacion' @{email=$accountRequest.email} '' 202 | Out-Null
# El cooldown sigue vigente: hacer avanzar únicamente las fechas de la acción sintética.
Sql "UPDATE public.usuario_acciones SET emitido_at=now()-interval '2 minutes' WHERE usuario_id='$accountId' AND tipo='password_recuperacion'" | Out-Null
Call POST '/api/Usuario/password/solicitar-recuperacion' @{email=$accountRequest.email} '' 202 | Out-Null
$beforeLogoutCode=CuentaCodigo $accountRequest.email 'password_recuperacion'
$otherSession=(Json (Call POST '/api/Usuario/login' @{email=$accountRequest.email;password=$newPassword} '' 200)).token
Call POST '/api/Usuario/me/cerrar-sesion' $null $freshToken 204 | Out-Null
Call GET '/api/Usuario/me' $null $freshToken 401 | Out-Null
Call GET '/api/Usuario/me' $null $otherSession 401 | Out-Null
Call POST '/api/Usuario/password/restablecer' @{codigo=$beforeLogoutCode;nuevaPassword=$newPassword} '' 400 | Out-Null
$freshToken=(Json (Call POST '/api/Usuario/login' @{email=$accountRequest.email;password=$newPassword} '' 200)).token
# Hasta tres emisiones por cuenta/tipo/día; no crear más filas por reintentos.
for($i=0;$i -lt 3;$i++){
    Sql "UPDATE public.usuario_acciones SET emitido_at=now()-interval '2 minutes',vence_at=now()-interval '1 second' WHERE usuario_id='$accountId' AND tipo='password_recuperacion'" | Out-Null
    Call POST '/api/Usuario/password/solicitar-recuperacion' @{email=$accountRequest.email} '' 202 | Out-Null
}
Assert ((Sql "SELECT emisiones FROM public.usuario_acciones WHERE usuario_id='$accountId' AND tipo='password_recuperacion'") -eq '3') 'No respeta cuota de emisión.'
Assert ((Sql "SELECT count(*) FROM public.usuario_acciones WHERE usuario_id='$accountId'") -eq '2') 'La tabla de acciones crece por cada intento.'
# Baja invalida acceso y no permite volver a emitir códigos.
Call DELETE '/api/Usuario/me' $null $freshToken 204 | Out-Null
$deadMailBefore=Sql "SELECT count(*) FROM public.correos_transaccionales WHERE usuario_id='$accountId'"
Call POST '/api/Usuario/password/solicitar-recuperacion' @{email=$accountRequest.email} '' 202 | Out-Null
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE usuario_id='$accountId'") -eq $deadMailBefore) 'Recuperación reactiva o envía a cuenta dada de baja.'

# Probar rollback de registro si no puede persistir acción+correo en la misma transacción.
Sql @'
CREATE FUNCTION public.test_cuenta_rollback() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF EXISTS(SELECT 1 FROM public.usuarios WHERE id=NEW.usuario_id AND email='rollback-cuenta@example.invalid') THEN
  RAISE EXCEPTION 'Fallo sintetico';
 END IF;
 RETURN NEW;
END; $$;
CREATE TRIGGER test_cuenta_rollback BEFORE INSERT ON public.usuario_acciones FOR EACH ROW EXECUTE FUNCTION public.test_cuenta_rollback();
'@ | Out-Null
try {
    $rollbackAccount=$accountRequest.Clone();$rollbackAccount.email='rollback-cuenta@example.invalid'
    Call POST '/api/Usuario' $rollbackAccount '' 503 | Out-Null
    Assert ((Sql "SELECT count(*) FROM public.usuarios WHERE email='rollback-cuenta@example.invalid'") -eq '0') 'Registro quedó sin código ante fallo de cola.'
}finally {
    Sql 'DROP TRIGGER test_cuenta_rollback ON public.usuario_acciones; DROP FUNCTION public.test_cuenta_rollback();' | Out-Null
}

# Recuperar una cuenta aún no verificada establece una contraseña propia y prueba el buzón.
$preAccount=$accountRequest.Clone();$preAccount.email='precuenta@example.invalid'
Call POST '/api/Usuario' $preAccount '' 202 | Out-Null
$preVerifyCode=CuentaCodigo $preAccount.email 'email_verificacion'
Call POST '/api/Usuario/password/solicitar-recuperacion' @{email=$preAccount.email} '' 202 | Out-Null
$preResetCode=CuentaCodigo $preAccount.email 'password_recuperacion'
Call POST '/api/Usuario/password/restablecer' @{codigo=$preResetCode;nuevaPassword=$newPassword} '' 204 | Out-Null
Call POST '/api/Usuario/email/confirmar' @{email=$preAccount.email;password=$password;codigo=$preVerifyCode} '' 400 | Out-Null
Call POST '/api/Usuario/login' @{email=$preAccount.email;password=$password} '' 401 | Out-Null
$preToken=(Json (Call POST '/api/Usuario/login' @{email=$preAccount.email;password=$newPassword} '' 200)).token
Assert ((Json (Call GET '/api/Usuario/me' $null $preToken 200)).emailVerificado) 'Recuperación no confirma propiedad del buzón.'
