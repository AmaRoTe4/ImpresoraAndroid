# PrintAgent HTTP API

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

## POST /config
Updates server configuration and restarts the HTTP server.

**Payload:** `{ "host": "127.0.0.1", "port": 5000, "listen_all": false }`

- `host` — IP address to bind (ignored if `listen_all: true`)
- `port` — TCP port (default: 5000)
- `listen_all` — if `true`, binds to `0.0.0.0` (all interfaces)

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

## CORS
All endpoints include `Access-Control-Allow-Origin: *` for cross-origin requests from PWAs.
