namespace IPCamTester
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {

        private static readonly TimeSpan interval = TimeSpan.FromMinutes(15);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await this.Initialize();
            
            while (!stoppingToken.IsCancellationRequested)
            {
                await MainTask();
                await Task.Delay(interval, stoppingToken); // 15 minutes
            }
            logger.LogInformation("cancellation requested");
        }

        private async Task MainTask()
        {
            logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Database.Open();
            try
            {
                foreach (var cam in _cameras)
                {
                    Error? err = await cam.Check();
                    if (err is not null)
                    {
                        logger.LogWarning($"Error: {err}, Camera: {cam}");
                        var err_type = err.GetErrorType();
                        await Database.AddLog(cam, err_type == ErrorType.Capture ? true : false, false);

                    }
                    else
                    {
                        await Database.AddLog(cam, true, true);
                        await Task.Delay(100);
                    }
                }
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
