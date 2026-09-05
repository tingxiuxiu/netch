using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests;

[TestClass]
public class Global
{
    [TestMethod]
    public void Test()
    {
        Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
    }

    [TestMethod]
    public void VLESS_UUID5()
    {
        // https://github.com/XTLS/Xray-core/discussions/715
        var bytes = new byte[16];
        var str = "example";
        var strBytes = Encoding.UTF8.GetBytes(str);

        var byteSource = new List<byte>();
        byteSource.AddRange(bytes);
        byteSource.AddRange(strBytes);

        var sha1Bytes = SHA1.HashData(byteSource.ToArray()).Take(16).ToArray();

        // UUIDv5: [254 181 68 49 48 27 82 187 166 221 225 233 62 129 187 158]
        sha1Bytes[6] = (byte)((sha1Bytes[6] & 0x0f) | (5 << 4));
        sha1Bytes[8] = (byte)((sha1Bytes[8] & (0xff >> 2)) | (0x02 << 6));

        var result = BitConverter.ToString(sha1Bytes).Replace("-", "").Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-").ToLower();
        Console.WriteLine(result);
        Assert.AreEqual("feb54431-301b-52bb-a6dd-e1e93e81bb9e", result);
    }
}
