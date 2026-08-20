using System.IO.Compression;
using System.Text;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var package = Path.Combine(root, "artifacts", "EndGameStats");
Directory.CreateDirectory(package);

WriteIcon(Path.Combine(package, "icon.png"));
var zipPath = Path.Combine(root, "artifacts", "Chaun-EndGameStats-0.4.1.zip");
if (File.Exists(zipPath)) File.Delete(zipPath);
ZipFile.CreateFromDirectory(package, zipPath, CompressionLevel.Optimal, false);
Console.WriteLine(zipPath);

static void WriteIcon(string path)
{
    const int size = 256;
    var raw = new byte[(size * 4 + 1) * size];
    for (var y = 0; y < size; y++)
    {
        var row = y * (size * 4 + 1);
        raw[row] = 0;
        for (var x = 0; x < size; x++)
        {
            var i = row + 1 + x * 4;
            var border = x < 12 || y < 12 || x >= 244 || y >= 244;
            var panel = x is >= 35 and <= 220 && y is >= 35 and <= 220;
            var grid = panel && ((y is >= 72 and <= 79) || (y is >= 117 and <= 124) || (y is >= 162 and <= 169));
            var bars = (y is >= 88 and <= 105 && x is >= 56 and <= 185) ||
                       (y is >= 133 and <= 150 && x is >= 56 and <= 155) ||
                       (y is >= 178 and <= 195 && x is >= 56 and <= 205);
            (raw[i], raw[i + 1], raw[i + 2], raw[i + 3]) = border ? ((byte)35, (byte)231, (byte)199, (byte)255)
                : bars ? ((byte)255, (byte)190, (byte)74, (byte)255)
                : grid ? ((byte)35, (byte)231, (byte)199, (byte)255)
                : panel ? ((byte)25, (byte)37, (byte)48, (byte)255)
                : ((byte)9, (byte)15, (byte)22, (byte)255);
        }
    }

    using var file = File.Create(path);
    file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    Chunk(file, "IHDR", Bytes(size).Concat(Bytes(size)).Concat(new byte[] { 8, 6, 0, 0, 0 }).ToArray());
    using var compressed = new MemoryStream();
    using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, true)) z.Write(raw);
    Chunk(file, "IDAT", compressed.ToArray());
    Chunk(file, "IEND", Array.Empty<byte>());
}

static byte[] Bytes(int value) => new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

static void Chunk(Stream stream, string type, byte[] data)
{
    stream.Write(Bytes(data.Length));
    var name = Encoding.ASCII.GetBytes(type);
    stream.Write(name); stream.Write(data);
    stream.Write(Bytes(unchecked((int)Crc(name.Concat(data).ToArray()))));
}

static uint Crc(byte[] bytes)
{
    uint crc = 0xffffffff;
    foreach (var b in bytes)
    {
        crc ^= b;
        for (var k = 0; k < 8; k++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
    }
    return ~crc;
}
