# Prueba paso a paso: solicitud y PDF

## 1. Preparar la base y la API

Si ya ejecutaste `database/004_solicitudes_ciudadanas.sql`, no lo repitas.
Si falta, detener la API y ejecutarlo completo en pgAdmin después de 001–003.
La corrección de motivos no requiere una nueva migración ni scaffolding.
Los registros históricos conservan su motivo; las altas nuevas aceptan exactamente
los cinco valores del frontend.

Compilar Presentation en Debug desde Visual Studio. Todavía no iniciar la API si
vas a cambiar la configuración local del antivirus en el siguiente paso.

## 2. Preparar ClamAV real en Windows

La carga exige antivirus activo; no basta con cambiar Host y Port. Esta preparación
usa el ZIP oficial, sin Docker ni instalar un servicio permanente de Windows.

1. Abrir [descargas oficiales](https://www.clamav.net/downloads) y elegir el ZIP
   estable de Windows x64. No descargar el código fuente ni el paquete Win32.
2. Extraerlo, por ejemplo dentro de `.tools/clamav` del repositorio (ignorado por
   Git). Identificar la carpeta concreta que contiene `clamd.exe` y `freshclam.exe`;
   el ZIP puede añadir un subdirectorio con su versión.
3. Abrir **PowerShell 7** en la raíz del proyecto y ejecutar, ajustando esa carpeta:

```powershell
cd C:\Users\jaman\LLASantaFeApi
.\scripts\start-antivirus-local.ps1 -ClamDirectory '.\.tools\clamav'
```

El script genera configuraciones locales, descarga/actualiza las firmas con
FreshClam e inicia clamd en `127.0.0.1:3310`. La primera actualización puede tardar.
Si FreshClam informa un error, resolverlo antes de continuar; el script no inicia
el motor con una actualización fallida. Dejar esa terminal abierta durante la
prueba. Ctrl+C detiene el proceso al terminar.

Desde otra terminal se puede comprobar que escucha:

```powershell
Test-NetConnection 127.0.0.1 -Port 3310
```

`TcpTestSucceeded` debe ser `True`; esto verifica el puerto, no sustituye una prueba
real de análisis. El script fue revisado sintácticamente; no se descargó ni ejecutó
el motor real como parte de las pruebas automatizadas del proyecto.

Referencia oficial: [configuración de ClamAV](https://docs.clamav.net/manual/Usage/Configuration.html)
y [gestión de firmas](https://docs.clamav.net/manual/Usage/SignatureManagement.html).

## 3. Conectar la API al antivirus

En `Presentation/appsettings.Local.json`, agregar esta propiedad al objeto raíz,
conservando las propiedades existentes de JWT y conexión:

```json
"PdfAntivirus": {
  "Host": "127.0.0.1",
  "Port": 3310
}
```

No reemplazar todo el archivo por ese fragmento. Separar con coma las propiedades
del objeto raíz. Reiniciar la API: la configuración local no se recarga en caliente.
Usar el puerto 3310 del motor real, **no** el 13310 del simulador automatizado.

Seleccionar **Presentation**, configuración **Debug**, perfil **http** y F5.
Abrir `http://localhost:5275/swagger`.

## 4. Obtener y cargar el token

En `POST /api/Usuario/login`, elegir Try it out y enviar los datos de una cuenta
registrada y activa:

```json
{
  "email": "tu-correo-registrado@example.com",
  "password": "tu-contraseña-real"
}
```

Esperar 200, copiar solo el valor `token`, abrir **Authorize**, pegarlo sin comillas
y sin escribir `Bearer`, confirmar y cerrar el diálogo. No hace falta ser Admin
para crear una solicitud o adjuntar su propio PDF. Si devuelve 401, no continuar
repitiendo intentos: comprobar credenciales/estado de cuenta. Cinco fallos bloquean
temporalmente el login.

## 5. Crear la solicitud

`POST /api/SolicitudCiudadana` → Try it out:

```json
{
  "motivo": "proyecto",
  "mensaje": "Presento un proyecto formal para mejorar los espacios públicos de mi localidad. Adjunto el documento con el detalle."
}
```

Estos son los valores que espera `motivo`:

| Texto visible en React | Valor enviado |
| --- | --- |
| Tengo un Proyecto / Propuesta formal | `proyecto` |
| Sugerencia para la provincia | `sugerencia` |
| Duda o Consulta general | `duda` |
| Quiero sumarme como voluntario/fiscal | `voluntariado` |
| Denuncia / Irregularidad | `denuncia` |

Se debe recibir **201 Created**, por ejemplo:

```json
{
  "id": "UUID-DEVUELTO-POR-LA-API",
  "estado": "recibida"
}
```

Copiar el UUID real. `recibida` es el acuse del envío; al consultar el expediente
su estado inicial es `pendiente`. El propietario lo obtiene del JWT: no enviar
usuarioId, pdfUrl, aceptacion ni estado. El mensaje debe tener 10–5000 caracteres.

## 6. Adjuntar el PDF a esa solicitud

En `POST /api/SolicitudCiudadana/{id}/pdf`:

1. Elegir Try it out.
2. Pegar en `id` el UUID recibido en el paso anterior.
3. En el campo **archivo**, seleccionar un PDF normal, sin contraseña, de hasta
   **2.097.152 bytes (2 MiB)**. Un documento breve exportado desde Word sirve para
   una primera prueba.
4. Usar multipart/form-data y ejecutar. No pegar JSON ni una ruta como texto.

Resultado esperado: **201 Created** con `{"estado":"disponible"}`.
El mismo JWT debe pertenecer al propietario. Hacer la carga antes de gestionar la
solicitud: debe seguir pendiente y tener menos de 24 horas. Se admite un único PDF
y no se puede reemplazar después de guardarlo.

## 7. Verificar el resultado

- `GET /api/SolicitudCiudadana/{id}` debe devolver **200** y `tienePdf: true`.
- `GET /api/SolicitudCiudadana/{id}/pdf` debe devolver **200** y permitir descargar
  el archivo. Requiere token; no es un enlace público.
- `GET /api/SolicitudCiudadana` muestra el listado propio.

No volver a crear una solicitud para consultar o descargar el mismo expediente.

## Si la prueba falla

| Código | Qué revisar |
| --- | --- |
| 400 al crear | `motivo` debe ser uno de los cinco values, no la etiqueta; revisar longitud del mensaje y campos extra |
| 400 al adjuntar | Un solo campo archivo, PDF válido, MIME application/pdf; posible rechazo del antivirus |
| 401 | Token ausente, vencido o revocado; volver a iniciar sesión y actualizar Authorize |
| 404 | ID incorrecto, solicitud ajena o todavía no tiene adjunto |
| 409 | Ya tiene PDF, dejó de estar pendiente, pasaron 24 h, archivo idéntico ya adjuntado por la cuenta o reserva vencida |
| 413 | Archivo/cuerpo excede el tamaño permitido |
| 429 | Cuota de solicitudes, intentos PDF o transferencias agotada |
| 503 | Antivirus no configurado, inaccesible o sin respuesta válida; también puede indicar capacidad global de adjuntos agotada; leer el mensaje |

Hay **3 solicitudes y 3 intentos de PDF por usuario cada 24 horas**. Los fallos de
carga también consumen intentos: preparar y comprobar el antivirus antes de subir.
No reiniciar la API esperando restablecer esas cuotas: persisten en PostgreSQL.
La cuota se libera al salir los registros de la ventana de 24 horas.
