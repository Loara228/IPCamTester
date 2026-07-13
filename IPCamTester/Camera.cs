using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace IPCamTester
{
    public enum VideoSrc
    {
        RTSP,
        HTTP
    }

    public class Camera
    {
        private Camera()
        {
            throw new Exception("Unreachable");
        }

        private Camera(String description, String ip, String user, String password)
        {
            _ = IPAddress.Parse(ip); // FormatException

            this.Description = description;

            this.IP = ip;
            this.User = user;
            this.Password = password;

            this.HttpInfo = null!;
        }

        public Camera(String description, String ip, String user, String password, RtspInfo info) : this(description, ip, user, password)
        {
            this.RtspInfo = info;
        }

        public Camera(String description, String ip, String user, String password, HttpInfo info) : this(description, ip, user, password)
        {
            this.HttpInfo = info;
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
            catch(Exception exc)
            {
                return new Error(ErrorType.Capture, exc.ToString());
            }
        }

        private async Task<Error?> CheckPing()
        {
            try
            {
                Ping ping = new Ping();
                var reply = await ping.SendPingAsync(this.IP, 3000);
                if (reply.Status == IPStatus.Success)
                    return null;
                return new Error(ErrorType.Ping, $"{reply.Address}: {reply.Status.ToString()}");
            }
            catch (Exception exc)
            {
                return new Error(ErrorType.Ping, exc.ToString());
            }
        }


        private String as_url()
        {
            if (this.RtspInfo is null && this.HttpInfo is null)
            {
                return "RtspInfo and HttpInfo is null";
            }
            if (this.RtspInfo is not null)
                return $"rtsp://{this.User}:{this.Password}@{this.IP}:{this.RtspInfo.Port}{this.RtspInfo.Play}";
            else
                throw new NotImplementedException("http capture is not implemented yet");
        }

        // Main
        public bool IsEnabled { get; set; } = true;
        public String? Description { get; set; } = null;

        // Network
        public String IP { get; private set; }
        public String User { get; private set; }
        public String Password { get; private set; }

        public RtspInfo? RtspInfo { get; private set; }
        public HttpInfo HttpInfo { get; private set; }


        public String ScreenshotPath
        {
            get
            {
                return Path.Join(ScreenshotDirectory, $"{this.IP.Replace('.', '-')}.jpg");
            }
        }

        // Misc
        private static String ScreenshotDirectory
        {
            get
            {
                if (_dir is null)
                {
                    String path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                        "CameraChecker"
                    );

                    if (!Path.Exists(path))
                        Directory.CreateDirectory(path);
                    _dir = path;
                }
                return _dir;
            }
        }

        private static String? _dir;
    }

    public class RtspInfo
    {
        public RtspInfo(String play, UInt16 port)
        {
            this.Play = play;
            this.Port = port;
        }

        /// <summary>
        /// Завершающая часть RTSP-запроса "PLAY" - всё что после "rtsp://host:port", начиная с "/", адресующая конкретный медиа-поток камеры
        /// Для проверки можете воспользоваться плеером VLC, открыв в нём URL вида rtsp://{User}:{Password}@{IP}:{PORT}{Play}
        /// </summary>
        public String Play { get; private set; }
        public UInt16 Port { get; private set; } = 554;
    }

    public class HttpInfo
    {
        public UInt16 Port { get; private set; } = 80;
    }
}
