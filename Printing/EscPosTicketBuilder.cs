using System.Linq;
using System.Text;
using System.Text.Json;
using Android.Graphics;

namespace PrintAgentAndroid.Printing;

public static class EscPosTicketBuilder
{
    private const int Cols = 48;
    private const int MaxBitmapWidth = 384; // seguro para 58mm; en 80mm también funciona aunque no use todo el ancho.

    public static byte[] BuildRawText(string text)
    {
        var bytes = new List<byte>();
        Init(bytes);
        bytes.AddRange(Encoding.UTF8.GetBytes((text ?? string.Empty) + "\n\n"));
        Cut(bytes);
        return bytes.ToArray();
    }

    public static byte[] BuildQrText(string jsonBody)
    {
        using var json = JsonDocument.Parse(jsonBody);
        var root = json.RootElement;
        var text1 = root.TryGetProperty("text_1", out var t1) ? t1.GetString() ?? "" : "";
        var text2 = root.TryGetProperty("text_2", out var t2) ? t2.GetString() ?? "" : "";
        var qrBase64 = root.TryGetProperty("qr_base64", out var qr) ? qr.GetString() ?? "" : "";

        var bytes = new List<byte>();
        Init(bytes);
        Align(bytes, 1);
        bytes.AddRange(Encoding.UTF8.GetBytes(text1 + "\n\n"));
        AddBase64Image(bytes, qrBase64);
        bytes.AddRange(Encoding.UTF8.GetBytes("\n" + text2 + "\n\n"));
        Cut(bytes);
        return bytes.ToArray();
    }

    public static byte[] BuildTicket(string jsonBody)
    {
        using var json = JsonDocument.Parse(jsonBody);
        var root = json.RootElement;
        var bytes = new List<byte>();
        Init(bytes);

        var headerLines = GetStringArray(root, "header_lines").ToList();
        if (headerLines.Count > 0)
        {
            Align(bytes, 1);
            Bold(bytes, true);
            foreach (var h in headerLines)
                bytes.AddRange(Encoding.UTF8.GetBytes(Center(h) + "\n"));
            Bold(bytes, false);
            bytes.AddRange(Encoding.UTF8.GetBytes(Line('=') + "\n"));
        }

        Align(bytes, 0);
        bytes.AddRange(Encoding.UTF8.GetBytes("FECHA: " + GetString(root, "date") + "\n"));
        bytes.AddRange(Encoding.UTF8.GetBytes("TICKET NUM: " + GetString(root, "ticket_number") + "\n"));
        bytes.AddRange(Encoding.UTF8.GetBytes("CLIENTE: " + GetString(root, "client") + "\n"));
        bytes.AddRange(Encoding.UTF8.GetBytes(Line() + "\n"));

        decimal subtotal = 0m;
        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var it in items.EnumerateArray())
            {
                var desc = GetString(it, "description");
                var qty = GetInt(it, "quantity", 1);
                var unit = GetDecimal(it, "unit_price", 0m);
                var total = it.TryGetProperty("total", out _) ? GetDecimal(it, "total", qty * unit) : qty * unit;
                var discount = GetDecimal(it, "discount_percent", 0m);
                if (discount > 0 && !it.TryGetProperty("total", out _))
                    total -= total * (discount / 100m);
                subtotal += total;

                bytes.AddRange(Encoding.UTF8.GetBytes(Trunc(desc, Cols) + "\n"));
                var left = $"{qty}x ${unit:F2}" + (discount > 0 ? $" -{discount:F0}%" : "");
                var right = $"${total:F2}";
                bytes.AddRange(Encoding.UTF8.GetBytes(TwoCols(left, right) + "\n"));
            }
        }

        bytes.AddRange(Encoding.UTF8.GetBytes(Line() + "\n"));

        var discountRate = GetDecimal(root, "discount_rate", 0m);
        var discountAmount = root.TryGetProperty("discount_amount", out _) ? GetDecimal(root, "discount_amount", 0m) : subtotal * (discountRate / 100m);
        var totalFinal = root.TryGetProperty("total_final", out _) ? GetDecimal(root, "total_final", subtotal - discountAmount) : subtotal - discountAmount;

        bytes.AddRange(Encoding.UTF8.GetBytes(Right("SUBTOTAL:", $"${subtotal:F2}") + "\n"));
        if (discountRate > 0 || discountAmount > 0)
        {
            bytes.AddRange(Encoding.UTF8.GetBytes(Right($"DESCUENTO {discountRate:F0}%:", $"${discountAmount:F2}") + "\n"));
            bytes.AddRange(Encoding.UTF8.GetBytes(Right("TOTAL C/DESCUENTO:", $"${totalFinal:F2}") + "\n"));
        }

        Bold(bytes, true);
        bytes.AddRange(Encoding.UTF8.GetBytes(Right("TOTAL FINAL:", $"${totalFinal:F2}") + "\n"));
        Bold(bytes, false);
        bytes.AddRange(Encoding.UTF8.GetBytes(Line() + "\n\n"));

        if (root.TryGetProperty("qr_base64", out var qrEl))
        {
            Align(bytes, 1);
            AddBase64Image(bytes, qrEl.GetString() ?? "");
            Align(bytes, 0);
            bytes.AddRange(Encoding.UTF8.GetBytes("\n"));
        }

        Align(bytes, 1);
        Bold(bytes, true);
        foreach (var f in GetStringArray(root, "footer_lines"))
            bytes.AddRange(Encoding.UTF8.GetBytes(Center(f) + "\n"));
        Bold(bytes, false);
        Align(bytes, 0);

        bytes.AddRange(Encoding.UTF8.GetBytes("\n"));
        Cut(bytes);
        return bytes.ToArray();
    }

    private static void AddBase64Image(List<byte> bytes, string base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return;
        var comma = base64.IndexOf(',');
        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > -1)
            base64 = base64[(comma + 1)..];

        var raw = Convert.FromBase64String(base64);
        using var bmp = BitmapFactory.DecodeByteArray(raw, 0, raw.Length);
        if (bmp == null) return;

        using var scaled = ScaleToWidth(bmp, Math.Min(MaxBitmapWidth, bmp.Width));
        bytes.AddRange(BitmapToRaster(scaled ?? bmp));
    }

    private static Bitmap? ScaleToWidth(Bitmap bmp, int maxWidth)
    {
        if (bmp.Width <= maxWidth) return null;
        var ratio = (double)maxWidth / bmp.Width;
        var height = Math.Max(1, (int)(bmp.Height * ratio));
        return Bitmap.CreateScaledBitmap(bmp, maxWidth, height, true);
    }

    private static byte[] BitmapToRaster(Bitmap bmp)
    {
        var width = bmp.Width;
        var height = bmp.Height;
        var widthBytes = (width + 7) / 8;
        var image = new byte[widthBytes * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = new Color(bmp.GetPixel(x, y));
                var luminance = (color.R + color.G + color.B) / 3;
                if (luminance < 128)
                    image[y * widthBytes + (x / 8)] |= (byte)(0x80 >> (x % 8));
            }
        }

        var result = new List<byte>();
        result.Add(0x1D); result.Add(0x76); result.Add(0x30); result.Add(0x00);
        result.Add((byte)(widthBytes % 256));
        result.Add((byte)(widthBytes / 256));
        result.Add((byte)(height % 256));
        result.Add((byte)(height / 256));
        result.AddRange(image);
        result.Add(0x0A);
        return result.ToArray();
    }

    private static void Init(List<byte> b) { b.Add(0x1B); b.Add(0x40); }
    private static void Cut(List<byte> b) { b.AddRange(new byte[] { 0x1D, 0x56, 0x41, 0x00 }); }
    private static void Align(List<byte> b, byte mode) { b.Add(0x1B); b.Add(0x61); b.Add(mode); }
    private static void Bold(List<byte> b, bool on) { b.Add(0x1B); b.Add(0x45); b.Add((byte)(on ? 1 : 0)); }

    private static string Line(char c = '-') => new(c, Cols);
    private static string Center(string text) => text.Length >= Cols ? text[..Cols] : new string(' ', (Cols - text.Length) / 2) + text;
    private static string Right(string label, string value)
    {
        var txt = $"{label} {value}";
        return txt.Length >= Cols ? txt[..Cols] : new string(' ', Cols - txt.Length) + txt;
    }
    private static string TwoCols(string left, string right)
    {
        left = Trunc(left, Cols);
        right = Trunc(right, Cols);
        var spaces = Math.Max(1, Cols - left.Length - right.Length);
        return left + new string(' ', spaces) + right;
    }
    private static string Trunc(string text, int max) => text.Length <= max ? text : text[..Math.Max(0, max - 3)] + "...";

    private static string GetString(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";
    private static int GetInt(JsonElement e, string name, int fallback) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var r) ? r : fallback;
    private static decimal GetDecimal(JsonElement e, string name, decimal fallback) => e.TryGetProperty(name, out var v) && v.TryGetDecimal(out var r) ? r : fallback;
    private static IEnumerable<string> GetStringArray(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in arr.EnumerateArray()) yield return item.GetString() ?? "";
    }
}
