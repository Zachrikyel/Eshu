using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Eshu.Services
{
    // Enriquece los juegos con datos que ningún escaneo local puede darnos — por
    // ahora solo el género, vía IGDB (que corre sobre autenticación de Twitch).
    // Verifiqué el flujo de autenticación (OAuth2 client credentials) antes de
    // escribir esto; lo que NO verifiqué con suficiente confianza fue el campo de
    // "tiempo para completar" de IGDB, así que EstimatedHoursToBeat se queda fuera
    // de este servicio por ahora — mejor eso en su propia parte, revisado con calma.
    public class IgdbMetadataService
    {
        private readonly HttpClient _http = new();
        private readonly string _clientId;
        private readonly string _clientSecret;
        private string? _accessToken;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_clientSecret);

        public IgdbMetadataService()
        {
            (_clientId, _clientSecret) = LoadCredentials();
        }

        public async Task<string?> FetchGenreAsync(string title)
        {
            if (!IsConfigured) return null;

            try
            {
                await EnsureAccessTokenAsync();
                if (_accessToken == null) return null;

                var safeTitle = title.Replace("\"", "");
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games")
                {
                    Content = new StringContent($"search \"{safeTitle}\"; fields genres.name; limit 1;")
                };
                request.Headers.Add("Client-ID", _clientId);
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var results = await response.Content.ReadFromJsonAsync<List<IgdbGame>>();
                return results?.FirstOrDefault()?.Genres?.FirstOrDefault()?.Name;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IgdbMetadataService] Error consultando '{title}': {ex.Message}");
                return null;
            }
        }

        private async Task EnsureAccessTokenAsync()
        {
            if (_accessToken != null) return; // dura semanas — con una vez por sesión de la app alcanza

            var tokenUrl = $"https://id.twitch.tv/oauth2/token?client_id={_clientId}&client_secret={_clientSecret}&grant_type=client_credentials";
            var response = await _http.PostAsync(tokenUrl, null);
            if (!response.IsSuccessStatusCode) return;

            var token = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>();
            _accessToken = token?.AccessToken;
        }

        // Sin cuenta de Twitch/IGDB configurada, esto no puede funcionar — dejamos
        // un archivo de ejemplo en vez de fallar en silencio sin explicación.
        private static (string clientId, string clientSecret) LoadCredentials()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "igdb-credentials.json");

            if (!File.Exists(configPath))
            {
                var example = new IgdbCredentials();
                File.WriteAllText(configPath, JsonSerializer.Serialize(example, new JsonSerializerOptions { WriteIndented = true }));
                return (string.Empty, string.Empty);
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var creds = JsonSerializer.Deserialize<IgdbCredentials>(json);
                return (creds?.ClientId ?? string.Empty, creds?.ClientSecret ?? string.Empty);
            }
            catch
            {
                return (string.Empty, string.Empty);
            }
        }

        private class IgdbCredentials
        {
            public string ClientId { get; set; } = string.Empty;
            public string ClientSecret { get; set; } = string.Empty;
        }

        private class TwitchTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;
        }

        private class IgdbGame
        {
            [JsonPropertyName("genres")]
            public List<IgdbGenre>? Genres { get; set; }
        }

        private class IgdbGenre
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }
    }
}
