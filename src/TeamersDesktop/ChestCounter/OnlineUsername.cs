using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChestCounter;

internal static class OnlineUsername
{
	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(10L)
	};

	public static async Task<string?> SetAsync(string username)
	{
		_ = 1;
		try
		{
			OnlineSettings onlineSettings = OnlineSync.Load();
			if (string.IsNullOrEmpty(onlineSettings.Token))
			{
				return L.T("Vincula este PC primero.", "Link this PC first.");
			}
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Put, onlineSettings.ApiBase.TrimEnd('/') + "/api/pc/username")
			{
				Content = JsonContent.Create(new
				{
					username = username.Trim()
				})
			};
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", onlineSettings.Token);
			using HttpResponseMessage resp = await Http.SendAsync(req);
			if (resp.IsSuccessStatusCode)
			{
				return null;
			}
			JsonElement value;
			return (await resp.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("error", out value) ? value.GetString() : $"HTTP {(int)resp.StatusCode}";
		}
		catch (Exception ex)
		{
			return ex.Message;
		}
	}
}
