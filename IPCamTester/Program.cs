using System.Globalization;
using System.Net;
using ClosedXML.Excel;
using IPCamTester;

if (ParseArgs(args))
{
    Environment.Exit(0);
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();


bool ParseArgs(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--insert" || args[i] == "-i")
        {
            Console.Write("IP Address: ");
            string ip = Console.ReadLine()!;
            _ = IPAddress.Parse(ip);

            Console.Write("User: ");
            string user = Console.ReadLine()!;

            Console.Write("Password: ");
            string password = Console.ReadLine()!;


            Console.Write("Port [554]: ");
            string port = Console.ReadLine()!;

            Console.Write("Play: ");
            string play = Console.ReadLine()!;

            Console.Write("Description (can be null): ");
            string? desc = Console.ReadLine()!;

            if (desc.Length == 0 || desc.IsWhiteSpace())
                desc = null;

            if (port.Length == 0 || port.IsWhiteSpace())
                port = "554";

            Database.Initialize().GetAwaiter().GetResult();
            Int32? id = Database.AddCamera(new Camera(desc, ip, user, password, UInt16.Parse(port), play)).GetAwaiter().GetResult();
            Database.Close().GetAwaiter().GetResult();

            if (id is null)
                Console.WriteLine("Failed to add camera");
            else
                Console.WriteLine($"Camera ID: {id}");

            return true;
        }
        else if (args[i] == "--all" || args[i] == "-a")
        {
            Database.Initialize().GetAwaiter().GetResult();
            var cameras = Database.GetCameras().GetAwaiter().GetResult();
            Database.Close().GetAwaiter().GetResult();
            Console.WriteLine($"{"ID",-6} | {"IP-Address",-16}");
            Console.WriteLine(new string('-', 25));
            foreach (var c in cameras)
            {
                Console.WriteLine($"{c.Id,-6} | {c.IP,-16} | {(c.IsEnabled ? "Yes" : "No"),-6}");
            }
            return true;
        }
        else if (args[i] == "--enable" || args[i] == "-e")
        {
            if (args.Length < i + 2)
            {
                Console.WriteLine("Argument missed: Camera ID");
                return true;
            }
            if (!Int32.TryParse(args[i + 1], out Int32 id))
            {
                Console.WriteLine($"Failed to parse {args[i + 1]} (Int32)");
            }
            Database.Initialize().GetAwaiter().GetResult();
            Database.SetCameraStatus(id, true).GetAwaiter().GetResult();
            Database.Close().GetAwaiter().GetResult();
            return true;

        }
        else if (args[i] == "--disable" || args[i] == "-d")
        {
            if (args.Length < i + 2)
            {
                Console.WriteLine("Argument missed: Camera ID");
                return true;
            }
            if (!Int32.TryParse(args[i + 1], out Int32 id))
            {
                Console.WriteLine($"Failed to parse {args[i + 1]} (Int32)");
            }
            Database.Initialize().GetAwaiter().GetResult();
            Database.SetCameraStatus(id, false).GetAwaiter().GetResult();
            Database.Close().GetAwaiter().GetResult();
            return true;

        }
        else if (args[i] == "--check" || args[i] == "-c")
        {
            if (args.Length < i + 2)
            {
                Console.WriteLine("Argument missed: Camera ID");
                return true;
            }
            if (!Int32.TryParse(args[i + 1], out Int32 id))
            {
                Console.WriteLine($"Failed to parse {args[i + 1]} (Int32)");
            }
            Database.Initialize().GetAwaiter().GetResult();
            var cameras = Database.GetCameras().GetAwaiter().GetResult();
            foreach (Camera c in cameras)
            {
                if (c.Id == id)
                {
                    var err = c.Check().GetAwaiter().GetResult();

                    if (err is null)
                        Console.WriteLine("Ping: true, capture: true");
                    else
                        Console.WriteLine($"Error: {err}");

                    Database.Close().GetAwaiter().GetResult();
                    return true;
                }
            }

            Console.WriteLine($"Camera with id: {id} is disabled or does not exists");
            return true;
        }
        else if (args[i] == "--check-all" || args[i] == "-ca")
        {
            Worker.SINGLE_ITERATION_FLAG = true;
            return false;
        }
        else if (args[i] == "--save-sheet" || args[i] == "-s")
        {
            Console.WriteLine("Создаём таблицу");
            String output_path = Path.Combine(Worker.WORK_DIR, "export.xlsx");
            XLWorkbook workbook = new XLWorkbook();
            var worksheet_cameras = workbook.Worksheets.Add("cameras");
            var worksheet_prop = workbook.Worksheets.Add("properties");

            var ruCulture = CultureInfo.GetCultureInfo("ru-RU");

            Database.Initialize().GetAwaiter().GetResult();
            var cameras = Database.GetCameras().GetAwaiter().GetResult();

            Console.WriteLine("Создаём лист properties");
            // SHEET 2
            {
                worksheet_prop.Cell(1, 1).Value = "ID";
                worksheet_prop.Cell(1, 2).Value = "IP Address";
                worksheet_prop.Cell(1, 3).Value = "Username";
                worksheet_prop.Cell(1, 4).Value = "Password";
                worksheet_prop.Cell(1, 5).Value = "RTSP port";
                worksheet_prop.Cell(1, 6).Value = "RTSP /play";

                for (Int32 cam_idx = 0; cam_idx < cameras.Count; ++cam_idx)
                {
                    Int32 j = cam_idx + 2;
                    Camera c = cameras[cam_idx];
                    worksheet_prop.Cell(j, 1).Value = c.Id;
                    worksheet_prop.Cell(j, 2).Value = c.IP;
                    worksheet_prop.Cell(j, 3).Value = c.User;
                    worksheet_prop.Cell(j, 4).Value = c.Password;
                    worksheet_prop.Cell(j, 5).Value = c.Port;
                    worksheet_prop.Cell(j, 6).Value = c.Play;
                }

                worksheet_prop.Columns().AdjustToContents();
                var range = worksheet_prop.Range(1, 1, cameras.Count + 1, 6);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            const Int32 OFFSET = 32;
            const Int32 IMG_HEIGHT_AS_ROWS = 23, IMG_WIDTH_AS_COLUMNS = 10;

            Console.WriteLine("Создаём лист cameras");
            // SHEET 1
            worksheet_cameras.Columns(IMG_WIDTH_AS_COLUMNS + 1, IMG_WIDTH_AS_COLUMNS + 14).Width = 4;
            for (Int32 cam_idx = 0; cam_idx < cameras.Count; ++cam_idx)
            {
                Camera c = cameras[cam_idx];

                Int32 row = cam_idx * OFFSET + 1;

                if (File.Exists(c.ScreenshotPath))
                {
                    var pic = worksheet_cameras.AddPicture(c.ScreenshotPath);
                    pic.MoveTo(worksheet_cameras.Cell(row, 1));
                    pic.Width = 640;
                    pic.Height = 480;
                }
                else
                {
                    var range = worksheet_cameras.Range(row, 1, row + IMG_HEIGHT_AS_ROWS, IMG_WIDTH_AS_COLUMNS);
                    range.Style.Fill.BackgroundColor = XLColor.Black;

                    var img_cell = worksheet_cameras.Cell(row, 1);
                    img_cell.Value = "No image";
                    img_cell.Style.Font.FontColor = XLColor.Red;
                    img_cell.Style.Font.Bold = true;
                }

                var ref_cell = worksheet_cameras.Cell(row + IMG_HEIGHT_AS_ROWS + 1, 1);
                ref_cell.Value = "ID: " + c.Id;
                ref_cell.SetHyperlink(new XLHyperlink(worksheet_prop.Cell(cam_idx + 2, 1)));

                var title_cell = worksheet_cameras.Range(row + IMG_HEIGHT_AS_ROWS + 1, 2, row + IMG_HEIGHT_AS_ROWS + 1, IMG_WIDTH_AS_COLUMNS);
                title_cell.Merge();
                title_cell.Value = c.Description == null ? "Описание не задано в БД" : c.Description;

                var logs = Database.GetLastCameraLogs(c.Id, 14).GetAwaiter().GetResult();

                var log_title_cell = worksheet_cameras.Range(row, IMG_WIDTH_AS_COLUMNS + 1, row, IMG_WIDTH_AS_COLUMNS + 14);
                log_title_cell.Merge();
                if (logs.Count == 0)
                {
                    log_title_cell.Value = "Нет ни одного лога";
                }
                else
                {
                    // total 14 logs
                    log_title_cell.Value = $"Проверки за период с {logs.Last().CheckTime.ToString("d", ruCulture)} до {logs[0].CheckTime.ToString("d", ruCulture)}";
                }
            }

            Database.Close().GetAwaiter().GetResult();
            Console.WriteLine("Записываем результат");
            workbook.SaveAs(output_path);
            Console.WriteLine($"Output: {output_path}");
            return true;
        }
        else if (args[i] == "--help" || args[i] == "-h")
        {
            Console.WriteLine(
@"--insert (-i)           Добавить новую камеру в БД
--all (-a)              Вывод всех камер (ID, IP)

--enable {ID} (-e)      Использовать проверку для этой камеры (default: enabled)
--disable {ID} (-d)     Игнорировать проверку для этой камеры

--check {id} (-c)       Проверить камеру
--check-all {id} (-ca)  Проверить все камеры

--save-sheet (-s)       Сохранить камеры и отчет в excel таблицу
");
            Console.WriteLine($"Directory: {Worker.WORK_DIR}");
            return true;
        }
    }
    return false;
}