// Quick test to verify Gemini API URL fix
// Compile and run: dotnet run test-gemini-fix.cs
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Testing Gemini API URL construction fix...\n");

        // Get API key from command line or use TEST_KEY
        string apiKey = args.Length > 0 ? args[0] : "TEST_KEY";
        string model = "gemini-2.5-flash";

        // This is the URL format from GeminiProvider.cs line 75 (after fix)
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        Console.WriteLine($"URL: {url}\n");

        // Build minimal request body
        string body = """
        {
          "contents": [{ "parts": [{ "text": "Say 'hello' in one word" }] }],
          "generationConfig": { "temperature": 0.1, "maxOutputTokens": 10 }
        }
        """;

        using var client = new HttpClient();
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"HTTP Status: {(int)response.StatusCode} {response.StatusCode}");
            Console.WriteLine($"Response: {responseBody.Substring(0, Math.Min(500, responseBody.Length))}...\n");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ FAIL: 404 Not Found - URL construction is BROKEN");
                Console.ResetColor();
                return;
            }

            if ((int)response.StatusCode == 400 && responseBody.Contains("API key not valid"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("✓ PASS: URL is correct (400 means invalid API key, not 404 bad URL)");
                Console.WriteLine("To test with real key: dotnet run test-gemini-fix.cs YOUR_API_KEY");
                Console.ResetColor();
                return;
            }

            if (response.IsSuccessStatusCode)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ SUCCESS: Gemini API is working!");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Unexpected status: {response.StatusCode}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.ResetColor();
        }
    }
}
