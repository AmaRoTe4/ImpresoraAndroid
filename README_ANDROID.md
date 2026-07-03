# PrintAgent Android - prototipo USB ESC/POS

Prototipo para mantener el mismo estándar del PrintAgent Windows, pero corriendo en una tablet Android.

## Flujo

```txt
PWA en Chrome Android
  -> POST http://127.0.0.1:5000/print_ticket
  -> APK PrintAgent Android
  -> USB Host Android
  -> Impresora térmica ESC/POS
```

## Endpoints incluidos

- `GET /status`
- `GET /printers`
- `POST /config`
- `POST /test`
- `POST /print` o `/print_text` con `{ "text": "..." }`
- `POST /print_ticket` con el mismo payload base del agente Windows
- `POST /print_qrtext` con `{ "text_1", "text_2", "qr_base64" }`
- `POST /print_zpl` devuelve `not-supported-on-android-ticket-printer`

## Build APK

Desde la raíz del repo:

```bash
dotnet workload install android
dotnet publish PrintAgent.Android/PrintAgent.Android.csproj -f net8.0-android -c Release
```

El APK queda dentro de:

```txt
PrintAgent.Android/bin/Release/net8.0-android/publish/
```

## Prueba en la tablet

1. Instalar APK.
2. Conectar hub USB-C con carga + impresora USB.
3. Abrir PrintAgent Android.
4. Tocar `Pedir permiso USB`.
5. Tocar `Imprimir prueba`.
6. Desde la PWA llamar a `http://127.0.0.1:5000/print_ticket`.

## Build automático (CI/CD)

El repo incluye un workflow de GitHub Actions en `.github/workflows/build-printagent.yml`.

| Trigger | Resultado |
|---------|-----------|
| Push a `main` (con cambios en el proyecto) | Compila + genera APK debug |
| Pull Request | Solo compila (feedback rápido) |
| Manual desde Actions → "Build PrintAgent Android APK" | Elige entre debug o release firmado |

### CI — Debug APK

Se genera automáticamente al pushear a `main`. El APK queda como artefacto descargable por 7 días en la run de Actions.

### CI — Release APK firmado

Para generar un APK firmado listo para producción:

```bash
# 1. Generar keystore local (una sola vez)
chmod +x scripts/setup-keystore.sh
./scripts/setup-keystore.sh
```

El script imprimirá los valores para agregar como **GitHub Secrets**:

| Secret | Valor |
|--------|-------|
| `ANDROID_KEYSTORE_BASE64` | Base64 del keystore (lo imprime el script) |
| `ANDROID_KEYSTORE_PASSWORD` | Password del keystore |
| `ANDROID_KEY_ALIAS` | Alias de la key (default: `printagent`) |
| `ANDROID_KEY_PASSWORD` | Password de la key |

Una vez configurado, desde GitHub:
1. Actions → "Build PrintAgent Android APK" → "Run workflow"
2. Marcar **"Build signed release APK"**
3. Opcional: escribir versión (ej: `v0.2.0`)
4. Run → descargar `printagent-release-v0.2.0` con el APK firmado

### Build manual (local)

```bash
dotnet workload install android
dotnet publish -c Debug -f net8.0-android -p:AndroidPackageFormat=apk -o apk-output
```

## Nota importante

Este es un prototipo funcional. Antes de producción conviene agregar:

- foreground service real para que no se cierre en segundo plano;
- token local para seguridad;
- logs persistentes;
- selección fija por VendorId/ProductId;
- cola de impresión y reintentos;
- ajuste de ancho 58mm/80mm configurable.
