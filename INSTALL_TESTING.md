# Instalar y probar el APK en un celular/tablet (Linux)

Guía paso a paso para bajar el APK compilado por CI e instalarlo en un dispositivo
Android conectado por USB, usando `adb` desde Linux.

## Requisitos

- `adb` instalado (`sudo apt install android-tools-adb` o `android-sdk-platform-tools`).
- Cable USB-C con soporte de datos (no solo carga).
- Celular/tablet con "Opciones de desarrollador" habilitadas.

## 1. Habilitar depuración USB en el dispositivo

1. Ajustes → Acerca del teléfono → tocar 7 veces "Número de compilación" hasta que
   aparezca "Ahora eres desarrollador".
2. Ajustes → Sistema → Opciones de desarrollador → activar **"Depuración USB"**.
3. Conectar el cable USB-C a la compu.
4. En la notificación/diálogo "Usar USB para..." elegir **"Transferencia de archivos"**
   (no "Solo carga" — si no, `adb` no ve el dispositivo).

## 2. Autorizar la PC en el celular

```bash
adb devices -l
```

Si figura como `unauthorized`, mirá la pantalla del celu: debería aparecer un popup
"¿Permitir depuración USB?" con la huella RSA de la compu. Aceptalo (marcá "permitir
siempre" para no repetirlo cada vez).

**Si no aparece el popup o queda colgado en `unauthorized`:** la key de `adb` quedó
cacheada mal en el dispositivo. Solución:

1. Celu: Opciones de desarrollador → **"Revocar autorizaciones de depuración USB"**.
2. Desenchufar y reenchufar el cable.
3. Reintentar `adb devices -l` — ahora debería salir el popup limpio.

Confirmá que el estado sea `device` (no `unauthorized`, no `offline`):

```bash
adb devices -l
# R8MY101BYFP    device usb:1-3 product:a06ub model:SM_A065M
```

## 3. Bajar el APK

1. GitHub → repo → **Actions** → correr `Build PrintAgent Android APK` más reciente en `main`.
2. Job `build` → sección **Artifacts** (abajo del log) → descargar `printagent-debug` (zip).
3. Descomprimir. Adentro hay dos APKs:
   - `com.amaro.printagent.apk` — sin firmar.
   - `com.amaro.printagent-Signed.apk` — **usar este** (viene autofirmado por el build,
     instalable directo).

## 4. Instalar

```bash
adb install -r /ruta/a/com.amaro.printagent-Signed.apk
```

**Si tira `INSTALL_FAILED_UPDATE_INCOMPATIBLE` (signatures do not match):**
cada corrida de CI firma con una keystore de debug distinta (no persiste entre runs).
Desinstalar la versión vieja primero:

```bash
adb uninstall com.amaro.printagent
adb install -r /ruta/a/com.amaro.printagent-Signed.apk
```

## 5. Probar en el dispositivo

1. Conectar el hub USB-C (con passthrough de carga) + la impresora térmica USB.
2. Abrir la app **"PrintAgent Android"**.
3. Tocar **"Ver estado"** — debería listar la impresora detectada (VID/PID).
4. Tocar **"Pedir permiso USB"** — Android tira un popup de permiso para ese dispositivo
   USB específico (distinto del popup de adb), aceptarlo.
5. Tocar **"Imprimir prueba"** — debería salir un ticket de test por la impresora.

Con la app abierta, el servidor HTTP queda activo en `127.0.0.1:5000`. Probar un
endpoint real desde el mismo dispositivo (o desde la PC si se hace `adb reverse`):

```bash
adb reverse tcp:5000 tcp:5000
curl -X POST http://127.0.0.1:5000/print_ticket \
  -H "Content-Type: application/json" \
  -d '{"header_lines":["TEST"],"items":[{"description":"PRODUCTO","quantity":1,"unit_price":10}],"total_final":10}'
```

## 5.1 Verificar que el server responde (antes de tocar la impresora)

Con la app abierta (o ya en segundo plano con el foreground service activo):

```bash
adb reverse tcp:5000 tcp:5000

curl -i http://127.0.0.1:5000/
# debe devolver 200: {"status":"ok","service":"PrintAgent Android"}

curl -i http://127.0.0.1:5000/printers
# debe devolver 200: {"defaultPrinter":"USB 11575:33575","preferredPrinter":"USB 11575:33575","printers":["USB 11575:33575"],"printersDetailed":[...]}
```

Si `/` no responde 200, el problema es del server HTTP (bind/puerto/servicio), no de la
impresora — no tiene sentido seguir probando impresión hasta que esto ande. Si pasa justo
después de sacar la app de "Recientes" con un swipe, esperar unos segundos y reintentar —
el `AlarmManager` tarda ~1s en reiniciar el servicio, y si el celular tiene optimización de
batería agresiva puede tardar más (ver sección de limitaciones abajo, botón "Permitir
ejecución en segundo plano" en la app).

## 6. Ver logs / diagnosticar un crash

Si la app "abre y se cierra sola":

```bash
adb logcat -c
adb shell am start -n com.amaro.printagent/crc6430f708eebae88085.MainActivity
sleep 3
adb logcat -d > /tmp/log.txt
grep -n "com.amaro.printagent" /tmp/log.txt | grep -iE "Process:|died|Start proc|monodroid|abort|FATAL"
```

Buscar la primera línea de error real cerca de `Start proc` / `has died` — el resto del
log es ruido del sistema.

### Errores ya vistos y resueltos

| Síntoma en logcat | Causa | Fix |
|---|---|---|
| `No assemblies found in '.../.__override__/...' ... Fast Deployment` | Debug APK sin ensamblados .NET embebidos (Fast Deployment espera que el IDE los empuje) | `EmbedAssembliesIntoApk=true` en el csproj |
| `RECEIVER_EXPORTED or RECEIVER_NOT_EXPORTED should be specified` | Android 13+ exige flag explícito al registrar `BroadcastReceiver` dinámico | Pasar `ReceiverFlags.NotExported` en `RegisterReceiver` (o `Exported` si el sender es otra app) |
| `'LinearLayout' does not contain a definition for 'Padding'` | Propiedad no existe en binding .NET Android | Usar `SetPadding(l, t, r, b)` |
| `'Build'`/`'BuildVersionCodes'` not found | Falta `using Android.OS;` | Agregar el using |
| `error CS8417 ... TcpClient ... IAsyncDisposable` | `TcpClient` no implementa `IAsyncDisposable` | Usar `using` en vez de `await using` |
| CI: `workload 'net8.0-android' is out of support` | SDK del runner (10.x) marca net8-android EOL | Migrar a `net9.0-android` + pinnear SDK con `global.json` |

## Limitaciones conocidas (prototipo)

- El foreground service usa tipo `dataSync` — en Android 15+ el sistema lo corta a las
  6hs corridas por día, sin importar si sigue usándose. Para uso 24/7 real hay que
  resolver esto (reinicio periódico del servicio, o migrar a otro modelo tipo companion
  device). Se eligió `dataSync` a propósito para evitar el tipo `connectedDevice`, que
  exige tener ya un permiso "compañero" (Bluetooth/NFC/WiFi/USB) concedido en el momento
  exacto de arrancar el servicio — con USB eso choca con el flujo real (el permiso USB se
  pide recién cuando el usuario toca el botón, después de que el servicio ya arrancó).
- El permiso USB se pide por dispositivo — si se desconecta el cable, hay que volver a tocar "Pedir permiso USB".
- **Persistencia en background:** sacar la app de "Recientes" con swipe mata el proceso
  (con foreground service y todo) por defecto en Android — el atributo `[Service]` de
  .NET Android no expone `stopWithTask="false"` para evitarlo. Mitigado con
  `OnTaskRemoved` + `AlarmManager.SetAndAllowWhileIdle` (reinicia el servicio ~1s
  después, resistente a Doze). En Samsung One UI y otros OEM con optimización de batería
  agresiva, esto puede no alcanzar — tocar **"Permitir ejecución en segundo plano"** en
  la app para pedir la exención real (abre el diálogo del sistema). Sin esa exención, no
  hay garantía 100% de persistencia — es una limitación de la plataforma, no del código.
- Sin cola de reintentos, sin ajuste de ancho de papel configurable.
