namespace IPCamTester
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Initializing");
            await this.Initialize();
            //while (!stoppingToken.IsCancellationRequested)
            //{
            //    if (logger.IsEnabled(LogLevel.Information))
            //    {
            //        logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            //    }
            //    await Task.Delay(1000, stoppingToken);
            //}

            logger.LogInformation("Starting");
            Camera cam = new("Test", "192.168.8.100", "usr", "password", 554, "/h264_2");
            var er = await cam.Check();
            if (er is not null)
            {
                logger.LogCritical(er.ToString());
            }
            logger.LogInformation("end");
        }

        private async Task Initialize()
        {
            await Database.Initialize();
            this._cameras = await Database.GetCameras();
            logger.LogInformation("{} loaded", _cameras.Count);
        }

        private List<Camera> _cameras = new List<Camera>();
    }
}
