using System.Net;
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
            Console.WriteLine($"{"ID",-6} | {"IP-Address",-16} | {"Active",-8}");
            Console.WriteLine(new string('-', 38)); // Разделительная линия
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

            Database.Close().GetAwaiter().GetResult();
            Console.WriteLine($"Camera with id: {id} is disabled or does not exists");
            return true;
        }
        else if (args[i] == "--help" || args[i] == "-h")
        {
            Console.WriteLine("--insert (-i)\n--all (-a)\n--enable {ID} (-e)\n--disable {ID} (-d)\n--check {id} (-c)\n\n");
            Console.WriteLine($"Directory: {Worker.WORK_DIR}");
            return true;
        }
    }
    return false;
}