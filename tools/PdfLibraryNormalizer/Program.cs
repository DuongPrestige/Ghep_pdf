using System.Text;
using PdfLibraryNormalizer;

Console.OutputEncoding = Encoding.UTF8;

switch (args.FirstOrDefault()?.ToLowerInvariant())
{
    case "scan" when args.Length == 3:
        Scanner.Run(args[1], args[2]);
        break;
    case "plan" when args.Length == 4:
        Planner.Run(args[1], args[2], args[3]);
        break;
    case "apply" when args.Length == 3:
        Applier.Run(args[1], args[2]);
        break;
    default:
        Console.WriteLine("""
            Chuẩn hóa tên kho PDF card: <IDOL> - <N> PAGE - <dd.MM>.pdf

              scan  <thư mục gốc> <scan.json>                 Chỉ đọc: số trang, dung lượng, ngày sửa
              plan  <scan.json> <rules.json> <thư mục đích>    Tạo <đích>\_KE_HOACH\plan.csv để duyệt (không copy)
              apply <plan.json> <thư mục đích>                 Copy theo kế hoạch đã duyệt (không ghi đè, không sửa gốc)
            """);
        Environment.ExitCode = 1;
        break;
}
