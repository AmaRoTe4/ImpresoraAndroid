# PrintAgent HTTP API

## GET /
Liveness check. Always returns 200 if the HTTP server is running, before checking any printer state.

**Response:**
```json
{ "status": "ok", "service": "PrintAgent Android" }
```

## GET /status
Returns current server status and detected printer info.

**Response:**
```json
{
  "status": "ok",
  "server": "running",
  "port": 5000,
  "printerConnected": true,
  "printer": {
    "name": "...",
    "vendorId": 0,
    "productId": 0,
    "hasPermission": true
  }
}
```

## GET /printers
Returns information about default printer, preferred printer, and list of installed USB devices.

**Android-specific:** each entry in `printers` includes `vendorId`/`productId`
(USB doesn't expose a friendly driver name like Windows does). Response also
includes `selectedVendorId`/`selectedProductId`, reflecting the currently
preferred printer set via `/config` (`null` if none set — falls back to the
first USB device with a BULK OUT endpoint).

## POST /config
Updates server configuration and restarts the HTTP server.

**Payload:** `{ "host": "127.0.0.1", "port": 5000, "listen_all": false }`

- `host` — IP address to bind (ignored if `listen_all: true`)
- `port` — TCP port (default: 5000)
- `listen_all` — if `true`, binds to `0.0.0.0` (all interfaces). **Android default is
  already `0.0.0.0`** so LAN clients can reach it without calling `/config` first.
- `vendor_id` / `product_id` — **(Android only)** sets the preferred USB printer when
  more than one is connected. Must be sent together.

**Response:**
```json
{ "status": "ok", "port": 5000, "host": "127.0.0.1", "listenAll": false }
```

## POST /test
Prints a hardcoded test page to verify printer connectivity.

## POST /print_text
Prints raw text using the preferred printer.

**Payload:** `{ "text": "Hello World" }`

> Note: `/print` and `/print_text` are treated identically by the router.

## POST /print
Alias for `/print_text`.

## POST /print_ticket
Prints a structured ticket (header, items, totals, QR).

**Payload:**
```json
{
  "header_lines": ["TIENDA", "DIRECCIÓN"],
  "date": "2025-01-01",
  "ticket_number": "001",
  "client": "CLIENTE",
  "items": [
    { "description": "PRODUCTO", "quantity": 2, "unit_price": 10.5 }
  ],
  "total_final": 21.0,
  "footer_lines": ["GRACIAS"],
  "qr_base64": "data:image/png;base64,..."
}
```

## POST /print_qrtext
Prints two text lines and a QR image.

**Payload:** `{ "text_1": "...", "text_2": "...", "qr_base64": "data:image/png;base64,..." }`

> Note: `/print_with_qr` is also accepted as an alias.

## POST /print_zpl
Prints ZPL commands received under the `"valores"` key. The raw ZPL data is sent to the printer as-is (the printer must support ZPL over USB).

**Payload:** `{ "valores": "^XA^FO50,50^ADN,36,20^FDHELLO^FS^XZ" }`

## Selecting a printer per request (Android only)
`/print`, `/print_text`, `/print_ticket`, `/print_qrtext`, `/print_with_qr` and
`/print_zpl` all accept optional `vendor_id`/`product_id` fields in the JSON body
to target a specific USB device for that single request, overriding whatever was
set globally via `/config`. Omit both to use the preferred printer (or the first
one found).

**Example:** `{ "text": "hola", "vendor_id": 11575, "product_id": 33575 }`

## CORS
All endpoints include `Access-Control-Allow-Origin: *` for cross-origin requests from PWAs.
