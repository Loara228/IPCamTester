namespace IPCamTester
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {

        public static Boolean SINGLE_ITERATION_FLAG = false;

        public static readonly String WORK_DIR =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IPCamTester"
            );

        private static readonly TimeSpan interval = TimeSpan.FromHours(12);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await this.Initialize();
            _ = Camera.ScreenshotDirectory;

            while (!stoppingToken.IsCancellationRequested)
            {
                await MainTask(stoppingToken);
                await Task.Delay(interval, stoppingToken); // 12 hours
                
                if (SINGLE_ITERATION_FLAG)
                    break;
            }
        }

        private async Task MainTask(CancellationToken stoppingToken)
        {
            logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Database.Open();
            try
            {
                logger.LogInformation("Check started");
                foreach (var cam in _cameras)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;
                        
                    Error? err = await cam.Check((Logger<Worker>?)logger);
                    if (err is not null)
                    {
                        logger.LogWarning($"Error: {err}, Camera: {cam}");
                        var err_type = err.GetErrorType();
                        await Database.AddLog(cam, err_type == ErrorType.Capture ? true : false, false);

                    }
                    else
                    {
                        await Database.AddLog(cam, true, true);
                        logger.LogInformation("(Camera: {Id}) Log added to the database", cam.Id);
                    }
                }
                logger.LogInformation("Check completed");
            }
            catch (Exception exc)
            {
                logger.LogCritical(exc.ToString());
            }
            finally
            {
                await Database.Close();
            }
        }

        private async Task Initialize()
        {
            await Database.Initialize();
            this._cameras = await Database.GetCameras();
            logger.LogInformation("{} cameras loaded", _cameras.Count);
            await Database.Close();
        }

        private List<Camera> _cameras = new List<Camera>();
    }
}