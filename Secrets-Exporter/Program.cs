using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Secrets_Exporter;

internal static class Program
{
    private const string GoogleAuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string RedirectUri = "http://localhost:3000/oauth2callback";
    private const string ClientId = "174381242671-es5jf9sagndaerlmtkujd4nmk68qhm7j.apps.googleusercontent.com";
    private const string BackendUrl = "https://oauth2-worker.yuri-ratkevich85360.workers.dev/oauth2callback";
    private const string SecretFileName = "immortal-vault.pass";
    private const string GoogleDriveGetFilesApiUrl = "https://www.googleapis.com/drive/v3/files?spaces=appDataFolder";
    private const string GoogleDriveGetFileContentApiUrl = "https://www.googleapis.com/drive/v3/files";

    private const string Scopes =
        "https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/drive.appdata";

    private static async Task Main()
    {
        Console.Clear();
        Console.WriteLine("Welcome to the Immortal Vault Secrets-Exporter!");

        Console.WriteLine("Would you like to log in with Google? (y/n): ");
        var confirm = Console.ReadLine()?.Trim().ToLower() == "y";

        if (confirm)
        {
            try
            {
                var state = GenerateState();
                var codeVerifier = GenerateCodeVerifier();
                var codeChallenge = GenerateCodeChallenge(codeVerifier);
                var authUrl = GenerateAuthUrl(codeChallenge, state);

                Console.WriteLine("Opening browser for authentication...");
                BrowserUtils.OpenBrowser(authUrl);

                var authorizationCode = await BrowserUtils.StartLocalServer(state);

                if (!string.IsNullOrEmpty(authorizationCode))
                {
                    var accessToken = await ExchangeCodeForAccessToken(authorizationCode, codeVerifier);
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        Console.WriteLine("Access token not found");
                        ConsoleUtils.PauseProcess("Press Enter to exit...");
                        return;
                    }

                    Console.WriteLine("Authentication tokens received...");
                    var secretFileContent = await GetSecretFileContentAsync(accessToken);

                    if (secretFileContent == null)
                    {
                        Console.WriteLine("No secret file found.");
                        ConsoleUtils.PauseProcess("Press Enter to exit...");
                        return;
                    }

                    Console.Write("Please enter your secret master password: ");
                    var password = ConsoleUtils.ReadPassword();

                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        var decryptedSecret = CryptoUtils.Decrypt(secretFileContent, password);
                        var filePath = Path.GetFullPath("secrets.json");
                        var jsonDoc = JsonDocument.Parse(decryptedSecret);
                        var formattedJson = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
                        });
                        await File.WriteAllTextAsync(filePath, formattedJson);

                        Console.WriteLine($"File saved successfully at: {filePath}");
                    }
                    else
                    {
                        Console.WriteLine("Invalid password, goodbye!");
                    }

                    ConsoleUtils.PauseProcess("Press Enter to exit...");
                }
                else
                {
                    Console.WriteLine("Failed to get the authorization code.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication failed: {ex}");
                ConsoleUtils.PauseProcess("Press Enter to exit...");
            }
        }
        else
        {
            ConsoleUtils.PauseProcess("Goodbye! Press Enter to exit...");
        }
    }

    private static async Task<string?> GetSecretFileContentAsync(string accessToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var driveFilesResponse =
            await client.GetAsync(GoogleDriveGetFilesApiUrl);
        var jsonContent = await driveFilesResponse.Content.ReadAsStringAsync();

        var driveFilesData = JsonSerializer.Deserialize<JsonElement>(jsonContent, new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        });

        if (!driveFilesData.TryGetProperty("files", out var files) || files.GetArrayLength() == 0)
            return null;

        foreach (var file in files.EnumerateArray())
        {
            if (file.GetProperty("name").GetString() != SecretFileName)
            {
                continue;
            }

            var fileId = file.GetProperty("id").GetString();
            var fileContentResponse =
                await client.GetAsync($"{GoogleDriveGetFileContentApiUrl}/{fileId}?alt=media");

            if (fileContentResponse.IsSuccessStatusCode)
            {
                return await fileContentResponse.Content.ReadAsStringAsync();
            }
        }

        return null;
    }

    private static string GenerateState()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string GenerateCodeVerifier()
    {
        using var rng = RandomNumberGenerator.Create();
        var randomBytes = new byte[32];
        rng.GetBytes(randomBytes);

        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string GenerateAuthUrl(string codeChallenge, string state)
    {
        return $"{GoogleAuthUrl}?" +
               $"response_type=code&" +
               $"client_id={Uri.EscapeDataString(ClientId)}&" +
               $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
               $"scope={Uri.EscapeDataString(Scopes)}&" +
               $"state={Uri.EscapeDataString(state)}&" +
               $"code_challenge={Uri.EscapeDataString(codeChallenge)}&" +
               $"code_challenge_method=S256";
    }

    private static async Task<string?> ExchangeCodeForAccessToken(string code, string codeVerifier)
    {
        using var httpClient = new HttpClient();

        var queryParams = new List<KeyValuePair<string, string>>
        {
            new("code", code),
            new("code_verifier", codeVerifier)
        };

        var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={p.Value}"));

        var url = $"{BackendUrl}?{queryString}";

        try
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            if (jsonDoc.RootElement.TryGetProperty("access_token", out var accessTokenElement))
            {
                return accessTokenElement.GetString();
            }

            Console.WriteLine("Access token not found in response...");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error exchanging code for token: " + ex.Message);
            return null;
        }
    }
}