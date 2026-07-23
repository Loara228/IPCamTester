using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace IPCamTester
{
    // public enum VideoSrc
    // {
    //     RTSP,
    //     HTTP
    // }

    public class Camera
    {
        public Camera(String? description, String ip, String user, String password, UInt16 port, String play)
        {
            _ = IPAddress.Parse(ip); // FormatException

            this.Description = description;

            this.IP = ip;
            this.User = user;
            this.Password = password;

            this.Port = port;
            this.Play = play;
        }

        public async Task<Error?> Check(Logger<Worker>? logger = null)
        {
            Error? e = await CheckPing();
            if (e is not null)
            {
                logger?.LogError("Ping check failed: {PingError}", e);
                return e;
            }

            VideoCapture capture = null!;
            try
            {
                capture = new VideoCapture(this.as_url());

                if (!capture.IsOpened())
                {
                    var error = new Error(ErrorType.Capture, "Failed to open video stream");
                    logger?.LogError("Video capture failed to open");
                    return error;
                }

                Mat frame = new Mat();
                bool frameRead = false;
                int attempts = 0;
                const int maxAttempts = 5;

                while (!frameRead && attempts < maxAttempts)
                {
                    frameRead = capture.Read(frame);
                    if (!frameRead)
                    {
                        attempts++;
                        logger?.LogWarning("Frame read failed on attempt {Attempt}/{MaxAttempts}", attempts, maxAttempts);
                        await Task.Delay(100);
                        frame = new Mat();
                    }
                }

                if (!frameRead)
                {
                    var error = new Error(ErrorType.Capture, "Failed to read frame from camera");
                    logger?.LogError("Camera did not provide any valid frames after {MaxAttempts} attempts", maxAttempts);
                    return error;
                }

                if (frame.Empty())
                {
                    var error = new Error(ErrorType.Capture, "Frame is empty");
                    logger?.LogError("Read frame is empty or invalid");
                    return error;
                }

                if (frame.Width <= 0 || frame.Height <= 0)
                {
                    var error = new Error(ErrorType.Capture, $"Invalid frame dimensions: {frame.Width}x{frame.Height}");
                    logger?.LogError("Frame has invalid dimensions - Width: {Width}, Height: {Height}", frame.Width, frame.Height);
                    return error;
                }

                String screenshot_path = this.ScreenshotPath;
                bool written = Cv2.ImWrite(screenshot_path, frame);

                if (!written)
                {
                    var error = new Error(ErrorType.Capture, "Failed to write screenshot to disk");
                    logger?.LogError("Failed to write screenshot to file: {ScreenshotPath}", screenshot_path);
                    return error;
                }

                return null;
            }
            catch (Exception exc)
            {
                var error = new Error(ErrorType.Capture, $"Exception: {exc.GetType().Name} - {exc.Message}");
                logger?.LogError(exc, "Unexpected exception occurred during video capture check. Type: {ExceptionType}, Message: {ExceptionMessage}",
                    exc.GetType().Name, exc.Message);
                return error;
            }
            finally
            {
                if (capture != null)
                {
                    capture.Dispose();
                }
            }
        }

        public override string ToString()
        {
            return $"ID: '{this.Id}', IP: '{this.IP}, Description: '{this.Description}''";
        }

        private async Task<Error?> CheckPing()
        {
            try
            {
                Ping ping = new Ping();
                var reply = await ping.SendPingAsync(this.IP, 3000);
                if (reply.Status == IPStatus.Success)
                    return null;
                return new Error(ErrorType.Ping, $"{reply.Status}");
            }
            catch (Exception exc)
            {
                return new Error(ErrorType.Ping, exc.ToString());
            }
        }


        private String as_url()
        {
            return $"rtsp://{this.User}:{this.Password}@{this.IP}:{this.Port}{this.Play}";
        }


        /*
         *  Main
         */

        public Int32 Id { get; set; }
        public bool IsEnabled { get; set; } = true;
        public String? Description { get; set; } = null;


        /*
         *  Network
         */

        public String IP { get; private set; }
        public String User { get; private set; }
        public String Password { get; private set; }


        /*
         *  RTSP
         */

        /// <summary>
        /// Завершающая часть RTSP-запроса "PLAY" - всё что после "rtsp://host:port", начиная с "/", адресующая конкретный медиа-поток камеры
        /// Для проверки можете воспользоваться плеером VLC, открыв в нём URL вида rtsp://{User}:{Password}@{IP}:{PORT}{Play}
        /// </summary>
        public String Play { get; private set; }
        public UInt16 Port { get; private set; } = 554;

        /*
         *  Misc
         */

        public string ScreenshotPath =>
            Path.Combine(ScreenshotDirectory, $"{IP.Replace('.', '-')}.jpg");

        public static string ScreenshotDirectory
        {
            get
            {
                if (_dir is null)
                {
                    var path = Path.Combine(Worker.WORK_DIR, "screenshots");
                    Directory.CreateDirectory(path);
                    _dir = path;
                }

                return _dir;
            }
        }

        private static string? _dir;
    }

    public record struct CameraLog(Int32 Id, Int32 CameraId, DateTime CheckTime, Boolean ping, Boolean Capture);
}
