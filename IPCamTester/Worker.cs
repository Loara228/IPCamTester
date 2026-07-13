namespace IPCamTester
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //while (!stoppingToken.IsCancellationRequested)
            //{
            //    if (logger.IsEnabled(LogLevel.Information))
            //    {
            //        logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            //    }
            //    await Task.Delay(1000, stoppingToken);
            //}

            logger.LogInformation("Starting");
            RtspInfo rtsp = new RtspInfo("/h264_2", 554);
            Camera cam = new("", "192.168.8.100", "usr", "password", rtsp);
            var er = await cam.Check();
            if (er is not null)
            {
                logger.LogCritical(er.ToString());
            }
            logger.LogInformation("end");
        }
    }
}
