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

        [Function("HealthCheck")]
        public async Task<HttpResponseData> HealthCheck([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            return _connection.State != System.Data.ConnectionState.Open
                ? await CreateResponse(req, HttpStatusCode.InternalServerError, "Database is not healthy")
                : await CreateResponse(req, HttpStatusCode.OK, "Database is healthy");

            async Task<HttpResponseData> CreateResponse(HttpRequestData request, HttpStatusCode statusCode, string status)
            {
                var response = request.CreateResponse(statusCode);
                var healthCheckResponse = new { status };
                await response.WriteStringAsync(JsonSerializer.Serialize(healthCheckResponse));
                return response;
            }
        }

        [Function("PostEvents")]
        public async Task<HttpResponseData> PostEvents([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var body = await req.ReadAsStringAsync();
            if (body != null)
            {
                var jsonDocument = JsonDocument.Parse(body);
                var evt = JsonSerializer.Deserialize<Event>(body);

                var command = _connection.CreateCommand();
                command.CommandText = "INSERT INTO events (data, created_at) VALUES (@data, @created_at)";
                command.Parameters.AddWithValue("@data", jsonDocument.RootElement.GetRawText().ToString());
                command.Parameters.AddWithValue("@created_at", System.DateTime.Now);
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation($"Inserted into database {jsonDocument.RootElement.GetRawText().ToString()}");
            }
            await _connection.CloseAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Event created");
            return response;
        }

        [Function("GetEvents")]
        public async Task<HttpResponseData> GetEvents([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting latest events");

            await _connection.OpenAsync();
            var selectCommand = _connection.CreateCommand();

            selectCommand.CommandText = "SELECT * FROM events ORDER BY created_at DESC LIMIT 10";
            var reader = await selectCommand.ExecuteReaderAsync();

            var events = new List<Event>();
            while (await reader.ReadAsync())
            {
                var eventId = reader.GetInt32(0);
                var eventData = reader.GetString(1);
                var jsonEventData = JsonDocument.Parse(eventData).RootElement;
                var createdAt = reader.GetDateTime(2);
                events.Add(new Event(eventId, jsonEventData, createdAt));
            }
            await _connection.CloseAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString(events.Any() ? JsonSerializer.Serialize(events) : "[]");
            return response;
        }

        [Function("GetOffsetEvents")]
        public async Task<HttpResponseData> GetOffsetEvents([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "page/{offset}")] HttpRequestData req,
            int offset = 1)
        {
            const int pageSize = 10;

            _logger.LogInformation("Getting paginated events {offset} {pageSize}", offset, pageSize);

            await _connection.OpenAsync();

            var selectCommand = _connection.CreateCommand();
            selectCommand.CommandText = $"SELECT * FROM events ORDER BY created_at DESC LIMIT @pageSize OFFSET @offset";
            selectCommand.Parameters.AddWithValue("@pageSize", pageSize);
            selectCommand.Parameters.AddWithValue("@offset", (offset - 1) * pageSize);

            var reader = await selectCommand.ExecuteReaderAsync();
            var events = new List<Event>();
            while (await reader.ReadAsync())
            {
                var eventId = reader.GetInt32(0);
                var eventData = reader.GetString(1);
                var jsonEventData = JsonDocument.Parse(eventData).RootElement;
                var createdAt = reader.GetDateTime(2);
                events.Add(new Event(eventId, jsonEventData, createdAt));
            }
            await _connection.CloseAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString(events.Any() ? JsonSerializer.Serialize(events) : "[]");
            return response;
        }


        public record Event(int Id, JsonElement Data, DateTime CreatedAt);
    }
}
