# Integración con PostgreSQL temporal. Nunca activa Gmail ni envía correos reales.
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='bienvenida'") -eq (Sql 'SELECT count(*) FROM public.usuarios')) 'Faltan bienvenidas o se duplicaron.'
Assert ((Sql "SELECT count(*) FROM public.correos_transaccionales WHERE tipo='afiliacion_aprobada'") -eq '2') 'Aprobación concurrente duplicó u omitió correos.'
$payload=@{tipo='bienvenida';referenciaId=$ida}
Call POST '/api/Notificacion' $payload '' 401|Out-Null
Call POST '/api/Notificacion' $payload $tb 403|Out-Null
$n1=Json (Call POST '/api/Notificacion' $payload $at 202)
$n2=Json (Call POST '/api/Notificacion' $payload $at 202)
Assert ($n1.id -eq $n2.id -and $n1.estado -eq 'pendiente') 'Encolado no idempotente.'
foreach($bad in @(@{tipo='noticia';referenciaId=$ida},@{tipo='bienvenida'},@{tipo='bienvenida';referenciaId=$ida;email='otro@example.invalid'},@{tipo='bienvenida';referenciaId=$ida;mensaje='Texto arbitrario'})) {
 Call POST '/api/Notificacion' $bad $at 400|Out-Null
}
Call POST '/api/Notificacion' @{tipo='bienvenida';referenciaId=[Guid]::NewGuid()} $at 404|Out-Null
Call POST '/api/Notificacion' @{tipo='afiliacion_aprobada';referenciaId=$affA} $at 404|Out-Null
$approved=Json (Call POST '/api/Notificacion' @{tipo='afiliacion_aprobada';referenciaId=$affB} $at 202)
Assert ($approved.estado -eq 'pendiente') 'No recupera aviso de aprobación.'
$before=Sql 'SELECT count(*) FROM public.correos_transaccionales'
Sql "BEGIN; INSERT INTO public.usuarios(nombre,apellido,email,password_hash) VALUES('Rollback','Test','rollback@example.invalid','unused'); ROLLBACK;"|Out-Null
Assert ((Sql 'SELECT count(*) FROM public.correos_transaccionales') -eq $before) 'La cola escapó al rollback del registro.'
# Aísla un aviso para verificar reclamación, cuota y recuperación sin llamar a Gmail.
Sql "UPDATE public.correos_transaccionales SET disponible_at=now()+interval '1 day'; UPDATE public.correos_transaccionales SET disponible_at=now() WHERE id='$($n1.id)';"|Out-Null
$claimed=Sql 'SELECT id FROM public.reservar_correo(1)'
Assert ($claimed -eq $n1.id) 'No reserva el correo disponible.'
Assert ((Sql 'SELECT count(*) FROM public.reservar_correo(1)') -eq '0') 'El correo se reservó dos veces.'
Sql "UPDATE public.correos_transaccionales SET disponible_at=now() WHERE id='$($approved.id)'; UPDATE public.correo_presupuesto SET siguiente_at=now()-interval '1 second';"|Out-Null
Assert ((Sql 'SELECT count(*) FROM public.reservar_correo(1)') -eq '0') 'No respeta cuota diaria.'
Sql "UPDATE public.correos_transaccionales SET reservado_at=now()-interval '6 minutes' WHERE id='$($n1.id)';"|Out-Null
Sql 'SELECT * FROM public.reservar_correo(1)'|Out-Null
Assert ((Sql "SELECT estado FROM public.correos_transaccionales WHERE id='$($n1.id)'") -eq 'incierto') 'Reenvía una reserva vencida.'
Sql "UPDATE public.correo_presupuesto SET dia=(now() AT TIME ZONE 'UTC')::date-1; UPDATE public.usuarios SET activo=false WHERE id='$idb';"|Out-Null
Assert ((Sql 'SELECT count(*) FROM public.reservar_correo(1)') -eq '0') 'Reservó correo de cuenta inactiva.'
Assert ((Sql "SELECT estado FROM public.correos_transaccionales WHERE id='$($approved.id)'") -eq 'cancelado') 'No cancela aviso de cuenta inactiva.'
Sql "UPDATE public.usuarios SET activo=true WHERE id='$idb'; UPDATE public.correos_transaccionales SET disponible_at=now(),created_at=now()-interval '15 days' WHERE tipo='bienvenida' AND usuario_id='$idb';"|Out-Null
Sql 'SELECT * FROM public.reservar_correo(1)'|Out-Null
Assert ((Sql "SELECT estado FROM public.correos_transaccionales WHERE tipo='bienvenida' AND usuario_id='$idb'") -eq 'cancelado') 'No cancela avisos vencidos.'
