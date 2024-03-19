using System.Net;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace MyFunction
{
    public class HttpApi
    {
        private readonly ILogger _logger;
        private readonly SqliteConnection _connection;

        public HttpApi(ILoggerFactory loggerFactory, SqliteConnection connection)
        {
            _logger = loggerFactory.CreateLogger<HttpApi>();
            _connection = connection;

            try
            {
                _connection.Open();
                var command = _connection.CreateCommand();
                command.CommandText = @"CREATE TABLE IF NOT EXISTS events
                (
                    id INTEGER PRIMARY KEY,
                    data TEXT NOT NULL,
                    created_at timestamp default current_timestamp
                )";
                command.ExecuteNonQuery();
            }
            catch (SqliteException e)
            {
                _logger.LogError(e, "Failed to open connection to database");
            }
        }

        [Function("PostEvents")]
        public HttpResponseData PostEvents([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var body = req.ReadAsStringAsync().Result;
            if (body != null)
            {
                var jsonDocument = JsonDocument.Parse(body);
                var evt = JsonSerializer.Deserialize<Event>(body);

                var command = _connection.CreateCommand();
                command.CommandText = "INSERT INTO events (data, created_at) VALUES (@data, @created_at)";
                command.Parameters.AddWithValue("@data", jsonDocument.RootElement.GetRawText().ToString());
                command.Parameters.AddWithValue("@created_at", System.DateTime.Now);
                command.ExecuteNonQuery();
                _logger.LogInformation($"Inserted into database {jsonDocument.RootElement.GetRawText().ToString()}");
            }
            _connection.Close();
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString("Event created");
            return response;
        }

        [Function("GetEvents")]
        public HttpResponseData GetEvents([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _connection.Open();
            var selectCommand = _connection.CreateCommand();
            selectCommand.CommandText = "SELECT * FROM events ORDER BY created_at DESC LIMIT 10";
            var reader = selectCommand.ExecuteReader();

            var events = new List<Event>();
            while (reader.Read())
            {
                var eventId = reader.GetInt32(0);
                var eventData = reader.GetString(1);
                var jsonEventData = JsonDocument.Parse(eventData).RootElement;
                var createdAt = reader.GetDateTime(2);
                events.Add(new Event(eventId, jsonEventData, createdAt));
            }
            _connection.Close();
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString(events.Any() ? JsonSerializer.Serialize(events) : "[]");
            return response;
        }

        public record Event(int Id, JsonElement Data, DateTime CreatedAt);
    }
}
