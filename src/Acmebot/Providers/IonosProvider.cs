using System.Net.Http.Headers;
using System.Text.Json.Serialization;

using Acmebot.Internal;
using Acmebot.Options;

namespace Acmebot.Providers;

public class IonosProvider(IonosOptions options) : IDnsProvider
{
    private readonly IonosClient _client = new(options.ApiKey);

    public string Name => "IONOS";

    public int PropagationSeconds => 60;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = await _client.ListZonesAsync();

        var dnsZones = new List<DnsZone>();

        foreach (var zone in zones)
        {
            var zoneDetails = await _client.GetZoneAsync(zone.Id);

            dnsZones.Add(new DnsZone(this)
            {
                Id = zoneDetails.Id,
                Name = zoneDetails.Name,
                NameServers = zoneDetails.NameServers
            });
        }

        return dnsZones;
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var records = values.Select(value => new DnsRecord
        {
            Name = recordName,
            Type = "TXT",
            Content = value,
            Ttl = 60,
            Disabled = false
        }).ToArray();

        await _client.CreateRecordsAsync(zone.Id, records);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var zoneDetails = await _client.GetZoneAsync(zone.Id, recordName, "TXT");

        foreach (var record in zoneDetails.Records)
        {
            await _client.DeleteRecordAsync(zone.Id, record.Id);
        }
    }

    private class IonosClient
    {
        public IonosClient(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(apiKey);

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.hosting.ionos.com/dns/v1/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<Zone>> ListZonesAsync()
        {
            var response = await _httpClient.GetAsync("zones");

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsAsync<Zone[]>();
        }

        public async Task<ZoneDetails> GetZoneAsync(string zoneId, string recordName = null, string recordType = null)
        {
            var url = recordName != null && recordType != null
                ? $"zones/{zoneId}?recordName={Uri.EscapeDataString(recordName)}&recordType={Uri.EscapeDataString(recordType)}"
                : $"zones/{zoneId}";

            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsAsync<ZoneDetails>();
        }

        public async Task CreateRecordsAsync(string zoneId, IReadOnlyList<DnsRecord> records)
        {
            var response = await _httpClient.PostAsync($"zones/{zoneId}/records", records);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteRecordAsync(string zoneId, string recordId)
        {
            var response = await _httpClient.DeleteAsync($"zones/{zoneId}/records/{recordId}");

            response.EnsureSuccessStatusCode();
        }
    }

    private class Zone
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    private class ZoneDetails
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("nameServers")]
        public string[] NameServers { get; set; } = [];

        [JsonPropertyName("records")]
        public DnsRecord[] Records { get; set; } = [];
    }

    private class DnsRecord
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("disabled")]
        public bool Disabled { get; set; }
    }
}
