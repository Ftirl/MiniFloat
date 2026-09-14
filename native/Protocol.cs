using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace MiniFloat {
internal static class Protocol {
    internal const int MaxLength = 64 * 1024;
    internal static Dictionary<string, object> Read(Stream stream) {
        var header = new byte[4];
        if (!ReadExact(stream, header, true)) return null;
        uint length = BitConverter.ToUInt32(header, 0);
        if (length == 0 || length > MaxLength) throw new InvalidDataException("Invalid native message size.");
        var body = new byte[(int)length]; ReadExact(stream, body, false);
        var serializer = new JavaScriptSerializer { MaxJsonLength=MaxLength, RecursionLimit=16 };
        var result = serializer.DeserializeObject(new UTF8Encoding(false, true).GetString(body)) as Dictionary<string, object>;
        if (result==null) throw new InvalidDataException("Expected a JSON object.");
        return result;
    }
    static bool ReadExact(Stream stream, byte[] bytes, bool allowEof) {
        int offset=0;
        while (offset<bytes.Length) {
            int n=stream.Read(bytes, offset, bytes.Length-offset);
            if (n==0) {
                if (allowEof && offset==0) return false;
                throw new EndOfStreamException("Incomplete native message.");
            }
            offset+=n;
        }
        return true;
    }
    internal static void Write(Stream stream, object value) {
        byte[] body=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(value));
        byte[] header=BitConverter.GetBytes(body.Length);
        stream.Write(header,0,4); stream.Write(body,0,body.Length); stream.Flush();
    }
    internal static string Text(Dictionary<string, object> value, string key) {
        object item; return value.TryGetValue(key,out item) ? item as string : null;
    }
    internal static double Number(Dictionary<string, object> value, string key, double fallback) {
        object item;
        if (!value.TryGetValue(key,out item) || !(item is int || item is long || item is double || item is decimal)) return fallback;
        double result=Convert.ToDouble(item);
        return Double.IsNaN(result) || Double.IsInfinity(result) ? fallback : result;
    }
}
}
