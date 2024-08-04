using MySql.Data.MySqlClient;
using System.Data;
using System.Text;
using System.Text.Json;

namespace DiscordNotifier
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Discord_BD_Notifier  Versão 1.0.0");
            var connectionString = "Server=192.168.100.84;Database=db-palworld-pvp-insiderhub;Uid=PalAdm;Pwd=sukelord;SslMode=none;";
            var notifierService = new NotifierService(connectionString);

            while (true)
            {
                try
                {
                    await notifierService.SendPendingNotifications();
                }
                catch (Exception ex)
                {
                    // Log the exception or handle it accordingly
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }

                // Delay for 60 seconds before checking for new notifications again
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
        }

    }
}

public class NotifierService
{
    private readonly string _connectionString;

    public NotifierService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task SendPendingNotifications()
    {
        var notifications = await GetPendingNotifications();

        foreach (var notification in notifications)
        {
            bool success = await SendNotification(notification);

            if (success)
            {
                await MarkAsSent(notification.Id);
            }
        }
    }

    private async Task<List<Notification>> GetPendingNotifications()
    {
        var notifications = new List<Notification>();

        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            string query = "SELECT id, chanel, msg, msg_type FROM discord_notifier WHERE sent IS NULL ORDER BY created ASC";
            using (var command = new MySqlCommand(query, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var notification = new Notification
                    {
                        Id = reader.GetInt64("id"),
                        Channel = reader.GetString("chanel"),
                        Message = reader.GetString("msg"),
                        MessageType = reader.GetString("msg_type")
                    };

                    notifications.Add(notification);
                }
            }
        }

        return notifications;
    }

    private async Task<bool> SendNotification(Notification notification)
    {
        using (var client = new HttpClient())
        {
            var request = new HttpRequestMessage(HttpMethod.Post, notification.Channel);

            var content = new
            {
                embeds = new[]
                {
                        new
                        {
                            description = notification.Message,
                            color = GetColor(notification.MessageType) // Aqui deve estar certo
                        }
                    }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");
            Console.WriteLine(DateTime.Now +" "+notification.Message);
            var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
    }

    private int GetColor(string messageType)
    {
        return messageType switch
        {
            "notify-on" => 3447003,
            "notify-off" => 10181046,
            "error" => 16711680,
            _ => 0
        };
    }

    private async Task MarkAsSent(long id)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            string query = "UPDATE discord_notifier SET sent = 1 WHERE id = @id";
            using (var command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@id", id);
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}


public class Notification
{
    public long Id { get; set; }
    public string Channel { get; set; }
    public string Message { get; set; }
    public string MessageType { get; set; }
}
