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

        public async Task<Error?> Check()
        {
            Error? e = await CheckPing();
            if (e is not null)
                return e;

            VideoCapture capture;
            try
            {
                capture = new VideoCapture(this.as_url());

                if (!capture.IsOpened())
                    return new Error(ErrorType.Capture, "Failed to open video stream");

                Mat frame = new Mat();
                capture.Read(frame);

                if (frame.Empty())
                    return new Error(ErrorType.Capture, "Frame is empty");

                String screenshot_path = this.ScreenshotPath;
                Cv2.ImWrite(screenshot_path, frame);
                return null;
            }
            catch (Exception exc)
            {
                return new Error(ErrorType.Capture, exc.ToString());
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

        public Int32? Id { get; set; }
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
}
