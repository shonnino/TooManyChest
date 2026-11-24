using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChestCounter;

public static class ObsServer
{
	public const int DefaultPort = 7373;

	private static TcpListener? _listener;

	private static volatile string _view = "";

	public static string? Url { get; private set; }

	public static void Start(int port)
	{
		if (_listener != null)
		{
			return;
		}
		for (int i = port; i < port + 10; i++)
		{
			try
			{
				TcpListener listener = new TcpListener(IPAddress.Loopback, i);
				listener.Start();
				_listener = listener;
				Url = $"http://localhost:{i}/";
				Thread thread = new Thread(() =>
				{
					AcceptLoop(listener);
				});
				thread.IsBackground = true;
				thread.Name = "OBS";
				thread.Start();
				return;
			}
			catch (SocketException)
			{
			}
		}
		ErrorLog.Write($"OBS: no free port from {port}", new InvalidOperationException("no free port"));
	}

	public static void Stop()
	{
		try
		{
			_listener?.Stop();
		}
		catch
		{
		}
		_listener = null;
		Url = null;
	}

	public static void Update(OverlayView view)
	{
		_view = ReportWriter.OverlayBox(view);
	}

	private static bool IsLocalHost(string host, int port)
	{
		if (!host.Equals($"localhost:{port}", StringComparison.OrdinalIgnoreCase))
		{
			return host.Equals($"127.0.0.1:{port}", StringComparison.Ordinal);
		}
		return true;
	}

	private static void AcceptLoop(TcpListener listener)
	{
		while (true)
		{
			TcpClient client;
			try
			{
				client = listener.AcceptTcpClient();
			}
			catch
			{
				break;
			}
			ThreadPool.QueueUserWorkItem((object? _) =>
			{
				Serve(client);
			});
		}
	}

	private static void Serve(TcpClient client)
	{
		using (client)
		{
			try
			{
				client.ReceiveTimeout = 3000;
				client.SendTimeout = 3000;
				NetworkStream stream = client.GetStream();
				byte[] array = new byte[4096];
				int num = stream.Read(array, 0, array.Length);
				if (num <= 0)
				{
					return;
				}
				string[] array2 = Encoding.ASCII.GetString(array, 0, num).Split('\n');
				string[] array3 = array2[0].Split(' ');
				string text = ((array3.Length > 1) ? array3[1].Split('?')[0] : "/");
				string host = (from l in array2.Skip(1)
					select l.Trim()).FirstOrDefault((string l) => l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))?.Substring(5).Trim() ?? "";
				string value = "200 OK";
				string s;
				if (!IsLocalHost(host, ((IPEndPoint)client.Client.LocalEndPoint).Port))
				{
					value = "403 Forbidden";
					s = "forbidden";
				}
				else
				{
					switch (text)
					{
					case "/":
					case "/overlay":
						s = ReportWriter.OverlayLivePage(_view);
						break;
					case "/view":
						s = _view;
						break;
					default:
						value = "404 Not Found";
						s = "not found";
						break;
					}
				}
				byte[] bytes = Encoding.UTF8.GetBytes(s);
				string s2 = $"HTTP/1.1 {value}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bytes.Length}\r\n" + "Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n";
				stream.Write(Encoding.ASCII.GetBytes(s2));
				stream.Write(bytes);
			}
			catch
			{
			}
		}
	}
}
