using Microsoft.Data.Sqlite;

namespace IPCamTester
{
    public static class Database
    {
        static Database()
        {
            _connection = null!;
        }

        public static async Task Initialize()
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dbPath = Path.Combine(documentsPath, "datafile.db");
            Directory.CreateDirectory(documentsPath);

            _connection = new SqliteConnection($"Data Source={dbPath}");
            await _connection.OpenAsync();

            using (var pragmaCmd = new SqliteCommand("PRAGMA foreign_keys = ON;", _connection))
            {
                await pragmaCmd.ExecuteNonQueryAsync();
            }

            string createCamerasTable =
@"CREATE TABLE IF NOT EXISTS IpCameras (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Ip TEXT NOT NULL UNIQUE,
    User TEXT NOT NULL,
    Password TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL CHECK (IsEnabled IN (0, 1)),
    Description TEXT NULL,
    Play TEXT NOT NULL,
    Port INTEGER NOT NULL CHECK (Port >= 0 AND Port <= 65535)
);";

            using (var command = new SqliteCommand(createCamerasTable, _connection))
            {
                await command.ExecuteNonQueryAsync();
            }

            string createLogsTable =
@"CREATE TABLE IF NOT EXISTS CameraLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CameraId INTEGER NOT NULL,
    CheckTime TEXT NOT NULL,
    Ping INTEGER NOT NULL CHECK (Ping IN (0, 1)),
    Capture INTEGER NOT NULL CHECK (Capture IN (0, 1)),
    FOREIGN KEY (CameraId) REFERENCES IpCameras (Id) ON DELETE CASCADE
);";

            using (var command = new SqliteCommand(createLogsTable, _connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        public static async Task AddLog(Camera camera, bool ping, bool capture)
        {
            string insertLogReq =
@"INSERT INTO CameraLogs (CameraId, CheckTime, Ping, Capture)
VALUES ($cameraId, $checkTime, $ping, $capture);";

            using (var command = new SqliteCommand(insertLogReq, _connection))
            {
                command.Parameters.AddWithValue("$cameraId", camera.Id);
                command.Parameters.AddWithValue("$checkTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("$ping", ping ? 1 : 0);
                command.Parameters.AddWithValue("$capture", capture ? 1 : 0);

                await command.ExecuteNonQueryAsync();
            }
        }
        /// <summary>
        /// В записи камеры в бд ID создаётся автоматически, и сразу же присваивается в объект camera
        /// </summary>
        /// <returns>В случае неудачи возвращает null. Стоит проверить уникальные поля</returns>
        public static async Task<Int32?> AddCamera(Camera camera)
        {
            string insertSql = @"
                INSERT INTO IpCameras (Ip, User, Password, IsEnabled, Description, Play, Port) 
                VALUES ($ip, $user, $password, $isEnabled, $description, $play, $port);
                SELECT last_insert_rowid();";

            using (var command = new SqliteCommand(insertSql, _connection))
            {
                command.Parameters.AddWithValue("$ip", camera.IP);
                command.Parameters.AddWithValue("$user", camera.User);
                command.Parameters.AddWithValue("$password", camera.Password);
                command.Parameters.AddWithValue("$isEnabled", camera.IsEnabled ? 1 : 0);

                command.Parameters.AddWithValue("$description", string.IsNullOrWhiteSpace(camera.Description) ? DBNull.Value : camera.Description);

                command.Parameters.AddWithValue("$play", camera.Play);
                command.Parameters.AddWithValue("$port", camera.Port);

                try
                {
                    camera.Id = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return camera.Id;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLite_CONSTRAINT
                {
                    return null;
                }
            }
        }

        public static async Task<List<Camera>> GetCameras()
        {
            List<Camera> cameras = new();
            string selectSql = "SELECT Id, Ip, User, Password, IsEnabled, Description, Play, Port FROM IpCameras WHERE IsEnabled = 1;";

            using (var command = new SqliteCommand(selectSql, _connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        string ip = reader.GetString(1);
                        string user = reader.GetString(2);
                        string password = reader.GetString(3);
                        bool isEnabled = reader.GetInt32(4) == 1;
                        string description = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
                        string play = reader.GetString(6);
                        ushort port = (ushort)reader.GetInt32(7);

                        var camera = new Camera(description, ip, user, password, port, play)
                        {
                            Id = id,
                            IsEnabled = isEnabled
                        };
                        cameras.Add(camera);
                    }
                }
            }
            return cameras;
        }
        public static async Task<bool> SetCameraStatus(int id, bool isEnabled)
        {
            string updateSql = "UPDATE IpCameras SET IsEnabled = $IsEnabled WHERE Id = $Id;";

            using (var command = new SqliteCommand(updateSql, _connection))
            {
                command.Parameters.AddWithValue("$IsEnabled", isEnabled ? 1 : 0);
                command.Parameters.AddWithValue("$Id", id);

                int rowsAffected = await command.ExecuteNonQueryAsync();

                return rowsAffected > 0;
            }
        }

        public static async Task Open()
        {
            await _connection.OpenAsync();
        }

        public static async Task Close()
        {
            await _connection.CloseAsync();
        }

        private static SqliteConnection _connection;
    }
}
