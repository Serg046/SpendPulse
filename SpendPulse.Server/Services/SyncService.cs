using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SpendPulse.Client.Models;
using SpendPulse.Server.Repositories;

namespace SpendPulse.Server.Services;

public class SyncService(IConfiguration configuration, ISettingsRepository settingsRepository, TransactionRepository transactionRepository, ISyncHistoryRepository syncHistoryRepository)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task Sync()
    {
        var startedAt = DateTime.UtcNow;
        var newTransactionCount = 0;

        try
        {
            var settings = await settingsRepository.Get();
            using var http = CreateHttpClient(settings.BankSession.ApplicationId);

            string? continuationKey = null;
            var isFirstPage = true;
            do
            {
                var (transactions, nextContinuationKey) = await FetchPage(http, settings.BankSession.AccountId, continuationKey);

                if (isFirstPage)
                {
                    if (transactions.Any())
                    {
                        await transactionRepository.DeleteWithoutEntryReference();
                    }

                    isFirstPage = false;
                }

                var entryReferences = transactions
                    .Where(t => t.EntryReference is not null)
                    .Select(t => t.EntryReference!)
                    .ToList();
                var existingEntryReferences = await transactionRepository.GetExistingEntryReferences(entryReferences);

                var newTransactions = transactions
                    .Where(t => t.EntryReference is null || !existingEntryReferences.Contains(t.EntryReference))
                    .ToList();
                await transactionRepository.Save(newTransactions);
                newTransactionCount += newTransactions.Count;

                continuationKey = existingEntryReferences.Count == 0 ? nextContinuationKey : null;
            } while (continuationKey is not null);

            await settingsRepository.UpdateLastDataUpdate(DateTime.UtcNow);

            await syncHistoryRepository.Add(new SyncHistoryEntry
            {
                StartedAt = startedAt,
                FinishedAt = DateTime.UtcNow,
                Success = true,
                NewTransactionCount = newTransactionCount
            });
        }
        catch (Exception ex)
        {
            await syncHistoryRepository.Add(new SyncHistoryEntry
            {
                StartedAt = startedAt,
                FinishedAt = DateTime.UtcNow,
                Success = false,
                NewTransactionCount = newTransactionCount,
                ErrorMessage = ex.Message
            });
            throw;
        }
    }

    public async Task<string?> RefreshToken(string? code = null)
    {
        var settings = await settingsRepository.Get();
        using var http = CreateHttpClient(settings.BankSession.ApplicationId);

        if (code is null)
        {
            var aspsp = await FindAspsp(http, configuration["EnableBanking:AspspCountry"]!, configuration["EnableBanking:AspspNameContains"]!);
            return await StartAuthorization(http, aspsp.Name, aspsp.Country, Guid.NewGuid().ToString());
        }

        var account = await AuthorizeSession(http, code);
        await settingsRepository.UpdateAccount(account, DateTime.UtcNow);
        return null;
    }
    
    async Task<string> StartAuthorization(HttpClient http, string aspspName, string aspspCountry, string stateValue)
    {
        var payload = new
        {
            access = new { valid_until = DateTimeOffset.UtcNow.AddDays(configuration.GetValue<int>("EnableBanking:TokenLifetimeDays")).ToString("yyyy-MM-ddTHH:mm:ss.ffffffzzz") },
            aspsp = new { name = aspspName, country = aspspCountry },
            state = stateValue,
            redirect_url = configuration["EnableBanking:RedirectUrl"]!,
            psu_type = "personal"
        };
        var resp = await http.PostAsync("auth", CreateJsonContent(payload));
        var json = await ParseOrThrow(resp);
        return json.GetProperty("url").GetString()
            ?? throw new Exception("Authorization failed");
    }

    private HttpClient CreateHttpClient(string applicationId)
    {
        var rsa = LoadPrivateKey(configuration["EnableBanking:PrivateKey"]!);
        var jwt = BuildJwt(applicationId, rsa);

        var http = new HttpClient { BaseAddress = new Uri("https://api.enablebanking.com/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return http;
    }
    
    async Task<string> AuthorizeSession(HttpClient http, string code)
    {
        var payload = new { code };
        var resp = await http.PostAsync("sessions", CreateJsonContent(payload));
        var json = await ParseOrThrow(resp);
        var account = json.GetProperty("accounts").EnumerateArray().First();
        return account.GetProperty("uid").GetString()
               ?? throw new Exception("Account not found");
    }
    
    async Task<JsonElement> ParseOrThrow(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"Request failed ({resp.StatusCode}): {body}");
        }
        return JsonDocument.Parse(body).RootElement;
    }
    
    StringContent CreateJsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static async Task<(List<TransactionDocument> Transactions, string? ContinuationKey)> FetchPage(HttpClient http, string accountUid, string? continuationKey)
    {
        var url = continuationKey is null
            ? $"accounts/{accountUid}/transactions"
            : $"accounts/{accountUid}/transactions?continuation_key={Uri.EscapeDataString(continuationKey)}";

        var resp = await http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"Request failed ({resp.StatusCode}): {body}");
        }

        var page = JsonSerializer.Deserialize<TransactionsPage>(body, JsonOptions)!;
        return (page.Transactions, page.ContinuationKey);
    }

    private static RSA LoadPrivateKey(string path)
    {
        var pem = File.ReadAllText(path);
        var key = RSA.Create();
        key.ImportFromPem(pem);
        return key;
    }

    private static string BuildJwt(string appId, RSA key)
    {
        var header = new { typ = "JWT", alg = "RS256", kid = appId };
        var now = DateTimeOffset.UtcNow;
        var body = new
        {
            iss = "enablebanking.com",
            aud = "api.enablebanking.com",
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddHours(1).ToUnixTimeSeconds()
        };

        var headerJson = JsonSerializer.Serialize(header);
        var bodyJson = JsonSerializer.Serialize(body);
        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var bodyB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(bodyJson));
        var signingInput = $"{headerB64}.{bodyB64}";

        var signature = key.SignData(Encoding.UTF8.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureB64 = Base64UrlEncode(signature);

        return $"{signingInput}.{signatureB64}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    
    async Task<(string Name, string Country)> FindAspsp(HttpClient http, string country, string nameContains)
    {
        var resp = await http.GetAsync($"aspsps?country={country}");
        var json = await ParseOrThrow(resp);
        foreach (var a in json.GetProperty("aspsps").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString() ?? "";
            if (name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            {
                return (name, a.GetProperty("country").GetString()!);
            }
        }
        
        throw new Exception("Could not find DSK Bank automatically. Check GET /aspsps?country=BG manually.");
    }

    private class TransactionsPage
    {
        public List<TransactionDocument> Transactions { get; set; } = [];
        public string? ContinuationKey { get; set; }
    }
}
