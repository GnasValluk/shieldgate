// ShieldGate - JSON cho --scan --json. Tach khoi Program de bo kiem tra
// (chi gom core + Strings + SelfTest) doc duoc ma khong can ca giao dien.
// Chi chua duong dan va ly do, khong co noi dung tep.
using System.Globalization;
using System.Text;

namespace ShieldGate
{
    internal static class ScanJson
    {
        public static string Text(string root, UsbScanResult result)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\r\n");
            sb.Append("  \"drive\": ").Append(Json.Quote(root)).Append(",\r\n");
            sb.Append("  \"files\": ")
              .Append(result.Files.ToString(CultureInfo.InvariantCulture)).Append(",\r\n");
            sb.Append("  \"items\": ")
              .Append(result.Items.Count.ToString(CultureInfo.InvariantCulture)).Append(",\r\n");
            sb.Append("  \"seconds\": ")
              .Append(result.Seconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(",\r\n");
            sb.Append("  \"truncated\": ").Append(result.Truncated ? "true" : "false").Append(",\r\n");
            sb.Append("  \"readOnly\": ").Append(result.ReadOnly ? "true" : "false").Append(",\r\n");
            sb.Append("  \"counts\": {\"shortcuts\":").Append(result.Shortcuts)
              .Append(",\"scripts\":").Append(result.Scripts)
              .Append(",\"autoruns\":").Append(result.Autoruns)
              .Append(",\"rootExes\":").Append(result.RootExes)
              .Append(",\"folders\":").Append(result.Folders)
              .Append(",\"other\":").Append(result.Other).Append("},\r\n");

            sb.Append("  \"warnings\": [");
            for (int i = 0; i < result.Warnings.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Json.Quote(result.Warnings[i]));
            }
            sb.Append("],\r\n");

            sb.Append("  \"findings\": [");
            for (int i = 0; i < result.Items.Count; i++)
            {
                UsbItem item = result.Items[i];
                if (i > 0) sb.Append(",");
                sb.Append("\r\n    {\"path\":").Append(Json.Quote(item.Path))
                  .Append(",\"relative\":").Append(Json.Quote(item.Rel))
                  .Append(",\"kind\":").Append(Json.Quote(item.Kind.ToString().ToLowerInvariant()))
                  .Append(",\"score\":").Append(item.Score.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"high\":").Append(item.High ? "true" : "false")
                  .Append(",\"flagOnly\":").Append(item.FlagOnly ? "true" : "false")
                  .Append(",\"size\":").Append(item.Size.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"codes\":[");
                for (int c = 0; c < item.Codes.Count; c++)
                {
                    if (c > 0) sb.Append(",");
                    sb.Append(Json.Quote(item.Codes[c]));
                }
                sb.Append("],\"reason\":").Append(Json.Quote(item.ReasonText)).Append("}");
            }
            sb.Append("\r\n  ]\r\n}\r\n");
            return sb.ToString();
        }
    }
}
