using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SpendPulse.Client.Models;

public class SyncLogEntry
{
    [BsonId]
    [JsonIgnore]
    public ObjectId Id { get; set; }

    [BsonElement("startedAt")]
    public DateTime StartedAt { get; set; }

    [BsonElement("finishedAt")]
    public DateTime FinishedAt { get; set; }

    [BsonElement("success")]
    public bool Success { get; set; }

    [BsonElement("newTransactionCount")]
    public int NewTransactionCount { get; set; }

    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }
}
