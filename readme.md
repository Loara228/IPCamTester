
dotnet --version: 10.0.109

install:

Native libs:

```
sudo apt update && sudo apt install aptitude -y
dotnet add package OpenCvSharp4.runtime.linux-arm64 # or linux-64
```

`dotnet publish -c Release -r linux-x64 --self-contained false`
or
`dotnet publish -c Release -r linux-arm64 --self-contained false`

`sudo mkdir -p /opt/ipcamtester`

copy files from publish/ to `/opt/ipcamtester`

create file `/etc/systemd/system/ipcamtester.service` with contents:

```bash
[Unit]
Description=IPCamTester
After=network.target

[Service]
WorkingDirectory=/opt/ipcamtester
ExecStart=/usr/bin/dotnet /opt/ipcamtester/IPCamTester.dll
Restart=always
RestartSec=5
User=youruser
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```


```bash
sudo systemctl daemon-reload
sudo systemctl enable ipcamtester
sudo systemctl start ipcamtester
sudo systemctl status ipcamtester
```

todo:

- Проверка времени (ONIF или API Dahua), автоматическая синхронизация времени

- excel отчеты
