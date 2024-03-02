using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

using Microsoft.Data.Sqlite;
using System.Text.Json;

// curl -s --request POST http://localhost:7071/api/HttpApi --data '{"name":"Azure Rocks"}'
// curl -s -H "Accept: application/json" --request GET http://localhost:7071/api/HttpApi

namespace MyFunction
{
    public class HttpApi
    {
        private readonly ILogger _logger;
        private readonly SqliteConnection _connection;

        public HttpApi(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HttpApi>();

            _connection = new SqliteConnection("Data Source=events.db");
            _connection.Open();

            var command = _connection.CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS events (event text, created_at timestamp default current_timestamp)";
            command.ExecuteNonQuery();
        }

        [Function("HttpApi")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.Headers.Add("Cache-Control", "no-cache");

            if (req.Method == "POST")
            {
                var body = req.ReadAsStringAsync().Result;
                if (body != null)
                {
                    var jsonDocument = JsonDocument.Parse(body);
                    var name = jsonDocument.RootElement.GetProperty("name").GetString();
                    var command = _connection.CreateCommand();
                    command.CommandText = "INSERT INTO events (event, created_at) VALUES (@name, @created_at)";
                    command.Parameters.AddWithValue("@name", name);
                    command.Parameters.AddWithValue("@created_at", System.DateTime.Now);
                    command.ExecuteNonQuery();
                    _logger.LogInformation($"Inserted {name} into database");
                }
            }

            var selectCommand = _connection.CreateCommand();
            selectCommand.CommandText = "SELECT * FROM events ORDER BY created_at DESC";
            var reader = selectCommand.ExecuteReader();

            var events = new List<object>();

            while (reader.Read())
            {
                events.Add(new { event_name = reader.GetString(0), created_at = reader.GetDateTime(1) });
            }

            response.WriteString(events.Any() ? JsonSerializer.Serialize(events) : "[]");
            _connection.Close();

            return response;
        }
    }
}
