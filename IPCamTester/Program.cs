using System.Net;
using IPCamTester;

if (ParseArgs(args))
{
    Thread.Sleep(1000);
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
            foreach (var c in cameras)
            {
                Console.WriteLine($"{c.Id} | {c.IP} | {c.IsEnabled}");
            }
            return true;
        } 
        else if (args[i] == "--help" || args[i] == "-h")
        {
            Console.WriteLine("--insert (-i)\n --all (-a)\n");
        }
    }
    return false;
}