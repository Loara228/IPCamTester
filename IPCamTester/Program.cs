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
            ExportToExcel();
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

static void ExportToExcel()
{
    const string OUTPUT_FILENAME = "export.xlsx";
    const int OFFSET = 28;
    const int IMG_HEIGHT_AS_ROWS = 23;
    const int IMG_WIDTH_AS_COLUMNS = 10;
    const int LOGS_COLUMN_COUNT = 14;
    
    var output_path = Path.Combine(Worker.WORK_DIR, OUTPUT_FILENAME);
    var ruCulture = CultureInfo.GetCultureInfo("ru-RU");

    Console.WriteLine("Создаём таблицу...");
    
    Database.Initialize().GetAwaiter().GetResult();
    var cameras = Database.GetCameras().GetAwaiter().GetResult();

    using (var workbook = new XLWorkbook())
    {
        var worksheet_prop = workbook.Worksheets.Add("properties");
        var worksheet_cameras = workbook.Worksheets.Add("cameras");

        Console.WriteLine("Создаём лист properties...");
        FormatPropertiesSheet(worksheet_prop, cameras);

        Console.WriteLine("Создаём лист cameras...");
        FormatCamerasSheet(
            worksheet_cameras, 
            cameras, 
            OFFSET, 
            IMG_HEIGHT_AS_ROWS, 
            IMG_WIDTH_AS_COLUMNS,
            LOGS_COLUMN_COUNT,
            ruCulture
        );

        Database.Close().GetAwaiter().GetResult();
        
        Console.WriteLine("Записываем результат...");
        workbook.SaveAs(output_path);
        Console.WriteLine($"Файл сохранён: {output_path}");
    }
}

static void FormatPropertiesSheet(IXLWorksheet worksheet, List<Camera> cameras)
{
    var headers = new[] { "ID", "IP Address", "Username", "Password", "RTSP port", "RTSP /play" };
    
    for (int col = 0; col < headers.Length; col++)
    {
        var headerCell = worksheet.Cell(1, col + 1);
        headerCell.Value = headers[col];
        headerCell.Style.Font.Bold = true;
        headerCell.Style.Font.FontSize = 12;
        headerCell.Style.Font.FontColor = XLColor.White;
        headerCell.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 78, 121);
        headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    for (int cam_idx = 0; cam_idx < cameras.Count; cam_idx++)
    {
        int row = cam_idx + 2;
        var camera = cameras[cam_idx];

        var values = new object[] 
        { 
            camera.Id.ToString(), 
            camera.IP, 
            camera.User, 
            camera.Password, 
            camera.Port.ToString(), 
            camera.Play 
        };
        
        for (int col = 0; col < values.Length; col++)
        {
            var cell = worksheet.Cell(row, col + 1);
            cell.Value = values[col]?.ToString() ?? string.Empty;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            
            if (cam_idx % 2 == 0)
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(217, 225, 242);
        }
    }

    var dataRange = worksheet.Range(1, 1, cameras.Count + 1, headers.Length);
    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    worksheet.Columns().AdjustToContents();
    
    for (int col = 1; col <= headers.Length; col++)
        if (worksheet.Column(col).Width < 15)
            worksheet.Column(col).Width = 15;
}

static void FormatCamerasSheet(
    IXLWorksheet worksheet,
    List<Camera> cameras,
    int offset,
    int imgHeight,
    int imgWidth,
    int logsColumnCount,
    CultureInfo culture)
{
    worksheet.Columns(imgWidth + 1, imgWidth + logsColumnCount).Width = 4;

    for (int cam_idx = 0; cam_idx < cameras.Count; cam_idx++)
    {
        var camera = cameras[cam_idx];
        int row = cam_idx * offset + 1;

        AddCameraImage(worksheet, camera, row, imgHeight, imgWidth);

        var idCell = worksheet.Cell(row + imgHeight + 1, 1);
        idCell.Value = $"ID: {camera.Id}";
        idCell.Style.Font.Bold = true;
        idCell.Style.Font.FontSize = 11;
        idCell.SetHyperlink(new XLHyperlink($"properties!A{cam_idx + 2}"));

        var titleRange = worksheet.Range(
            row + imgHeight + 1, 2, 
            row + imgHeight + 1, imgWidth);
        titleRange.Merge();
        titleRange.Value = camera.Description ?? "⚠️ Описание не задано в БД";
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 14;
        // titleRange.Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        var logs = Database.GetLastCameraLogs(camera.Id, logsColumnCount).GetAwaiter().GetResult();
        AddLogsSection(worksheet, logs, row, imgWidth, imgHeight, logsColumnCount, culture);
    }
}

static void AddCameraImage(
    IXLWorksheet worksheet, 
    Camera camera, 
    int row, 
    int imgHeight, 
    int imgWidth)
{
    if (File.Exists(camera.ScreenshotPath))
    {
        try
        {
            var picture = worksheet.AddPicture(camera.ScreenshotPath);
            picture.MoveTo(worksheet.Cell(row, 1));
            picture.Width = 640;
            picture.Height = 480;
        }
        catch
        {
            AddNoImagePlaceholder(worksheet, row, imgHeight, imgWidth);
        }
    }
    else
    {
        AddNoImagePlaceholder(worksheet, row, imgHeight, imgWidth);
    }
}

static void AddNoImagePlaceholder(
    IXLWorksheet worksheet, 
    int row, 
    int imgHeight, 
    int imgWidth)
{
    var range = worksheet.Range(row, 1, row + imgHeight, imgWidth);
    range.Merge();
    range.Style.Fill.BackgroundColor = XLColor.Black;
    range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    range.Value = "❌ Изображение не найдено";
    range.Style.Font.FontColor = XLColor.Red;
    range.Style.Font.Bold = true;
    range.Style.Font.FontSize = 14;
}

static void AddLogsSection(
    IXLWorksheet worksheet,
    List<CameraLog> logs,
    int row,
    int imgWidth,
    int imgHeight,
    int logsColumnCount,
    CultureInfo culture)
{
    var logTitleRange = worksheet.Range(
        row, imgWidth + 1,
        row, imgWidth + logsColumnCount);
    logTitleRange.Merge();
    logTitleRange.Style.Font.Bold = true;
    logTitleRange.Style.Font.FontSize = 12;
    logTitleRange.Style.Fill.BackgroundColor = XLColor.FromArgb(79, 129, 189);
    logTitleRange.Style.Font.FontColor = XLColor.White;
    logTitleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    if (logs.Count == 0)
    {
        logTitleRange.Value = "📭 Нет ни одного лога";
    }
    else
    {
        var dateFrom = logs.Last().CheckTime.ToString("d", culture);
        var dateTo = logs[0].CheckTime.ToString("d", culture);
        logTitleRange.Value = $"{dateFrom} - {dateTo}";

        for (int logIdx = 0; logIdx < logs.Count; logIdx++)
        {
            var log = logs[logIdx];
            int colOffset = imgWidth + 1 + logIdx;

            var dateRange = worksheet.Range(row + 1, colOffset, row + 9, colOffset);
            dateRange.Merge();
            dateRange.Value = log.CheckTime.ToString("dd/MM/yyyy\nHH:mm:ss");
            dateRange.Style.Alignment.SetTextRotation(90);
            dateRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            dateRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
            dateRange.Style.Font.FontSize = 9;
            dateRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            var pingCell = worksheet.Cell(row + 10, colOffset);
            FormatStatusCell(pingCell, log.ping, "P");

            var captureCell = worksheet.Cell(row + 11, colOffset);
            FormatStatusCell(captureCell, log.Capture, "C");
        }
    }
}

static void FormatStatusCell(IXLCell cell, bool status, string label)
{
    cell.Value = label;
    cell.Style.Font.Bold = true;
    cell.Style.Font.FontSize = 10;
    cell.Style.Font.FontColor = XLColor.White;
    cell.Style.Fill.BackgroundColor = status ? XLColor.DarkGreen : XLColor.DarkRed;
    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
}