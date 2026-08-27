using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SpendPulse.Client.Models;

public class MerchantNameExclusion
{
    [BsonId]
    [JsonIgnore]
    public ObjectId Id { get; set; }

    [BsonElement("word")]
    public string Word { get; set; } = string.Empty;

    [BsonElement("merchantName")]
    public string? MerchantName { get; set; }
}
