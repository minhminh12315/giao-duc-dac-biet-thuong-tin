using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var http = new HttpClient { BaseAddress = new Uri("http://localhost:5080/api/") };
var login = await http.PostAsJsonAsync("auth/login", new { userName = "admin", password = "Admin@123" });
var tokenDoc = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
var token = tokenDoc.RootElement.GetProperty("accessToken").GetString();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

var studentId = Guid.Parse("d68dd263-3e35-4422-b199-0161a509baed");
var now = DateTime.Now;
var invRes = await http.PostAsJsonAsync("invoices/generate", new { studentId, year = now.Year, month = now.Month, regenerate = true });
Console.WriteLine($"Invoice HTTP {(int)invRes.StatusCode}: {await invRes.Content.ReadAsStringAsync()}");

var prog = await http.GetAsync($"students/{studentId}/progress");
Console.WriteLine($"Progress HTTP {(int)prog.StatusCode}: {await prog.Content.ReadAsStringAsync()}");
