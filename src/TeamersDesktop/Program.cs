using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ChestCounter;

internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		if (args.Contains("--standby"))
		{
			int num = Array.IndexOf(args, "--data");
			if (!OperatingSystem.IsWindows())
			{
				return 1;
			}
			return FortniteWatch.RunStandby((num >= 0 && num + 1 < args.Length) ? args[num + 1] : Store.DefaultFolder());
		}
		// The dashboard and dialogs draw using fixed WinForms coordinates at 96 DPI.
		Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		if (OperatingSystem.IsWindows())
		{
			Fonts.Load();
		}
		bool cliMode = args.Any((string a) =>
		{
			switch (a)
			{
			case "--once":
			case "--help":
			case "-h":
			case "--overlay-preview":
				return true;
			default:
				return false;
			}
		});
		if (OperatingSystem.IsWindows())
		{
			NativeConsole.Ensure(cliMode);
			if (!cliMode)
			{
				NativeConsole.Hide();
			}
		}
		Console.OutputEncoding = Encoding.UTF8;
		AppDomain.CurrentDomain.UnhandledException += (object _, UnhandledExceptionEventArgs e) =>
		{
			ErrorLog.Write("Unexpected error", e.ExceptionObject);
			if (cliMode)
			{
				Console.WriteLine(L.T("Error inesperado: ", "Unexpected error: ") + e.ExceptionObject);
			}
			else
			{
				MessageBox.Show(L.T("Error inesperado. Los detalles están en errors.log, en la carpeta de datos.", "Unexpected error. The details are in errors.log, in the data folder.") + "\n\n" + e.ExceptionObject, "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		};
		if (args.Length > 0 && args[0] == "--teamers-app")
		{
			if (args.Length != 3 || !Path.IsPathFullyQualified(args[1]) ||
				!TeamersBrowserLink.TryParse(args[2], out string code))
			{
				return 1;
			}
			var teamersStore = new Store(args[1]);
			var teamersConfig = teamersStore.LoadConfig() ?? new Config();
			L.Use(teamersConfig.Language);
			ErrorLog.FilePath = teamersStore.ErrorLogPath;
			using var authorization = new TeamersConnectionForm(new TeamersClient(teamersStore, teamersConfig), code);
			return authorization.ShowDialog() == DialogResult.OK ? 0 : 1;
		}
		string text = null;
		string text2 = null;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		string text3 = null;
		int num3;
		for (int num2 = 0; num2 < args.Length; num2++)
		{
			string text4 = args[num2];
			if (text4 == null)
			{
				continue;
			}
			num3 = text4.Length;
			if (num3 <= 6)
			{
				if (num3 != 2)
				{
					if (num3 != 6)
					{
						continue;
					}
					char c = text4[2];
					if ((uint)c <= 100u)
					{
						switch (c)
						{
						case 'd':
							if (text4 == "--data")
							{
								text = args[++num2];
							}
							break;
						case 'a':
							if (text4 == "--auto")
							{
								flag3 = true;
							}
							break;
						}
						continue;
					}
					if (c != 'h')
					{
						switch (c)
						{
						case 'l':
							if (text4 == "--lang")
							{
								num2++;
							}
							break;
						case 'o':
							if (text4 == "--once")
							{
								flag = true;
							}
							break;
						}
						continue;
					}
					if (!(text4 == "--help"))
					{
						continue;
					}
				}
				else if (!(text4 == "-h"))
				{
					continue;
				}
				Console.WriteLine(L.Big("help", "Contador de cofres de Fortnite\n\nOpciones:\n  --demos <carpeta>   carpeta de los replays (por defecto %LOCALAPPDATA%\\FortniteGame\\Saved\\Demos)\n  --data <carpeta>    carpeta de los datos (por defecto %APPDATA%\\ChestCounter)\n  --once              analiza los replays terminados, muestra el resumen y sale (sin ventana)\n  --reset-config      vuelve a mostrar las preguntas iniciales\n  --overlay-preview <archivo.png>   guarda vistas previas del overlay y sale", "Fortnite Chest Counter\n\nOptions:\n  --demos <folder>    replays folder (default %LOCALAPPDATA%\\FortniteGame\\Saved\\Demos)\n  --data <folder>     data folder (default %APPDATA%\\ChestCounter)\n  --once              analyze the finished replays, show the summary and quit (no window)\n  --reset-config      show the first-run questions again\n  --overlay-preview <file.png>   save previews of the overlay and quit"));
				return 0;
			}
			switch (num3)
			{
			case 14:
				switch (text4[2])
				{
				case 'r':
					if (text4 == "--reset-config")
					{
						flag2 = true;
					}
					break;
				case 'a':
					if (text4 == "--after-update")
					{
						flag4 = true;
					}
					break;
				}
				break;
			case 7:
				if (text4 == "--demos")
				{
					text2 = args[++num2];
				}
				break;
			case 17:
				if (text4 == "--overlay-preview")
				{
					text3 = args[++num2];
				}
				break;
			}
		}
		if (text == null)
		{
			text = Store.DefaultFolder();
		}
		Store store = new Store(text);
		ErrorLog.FilePath = store.ErrorLogPath;
		using (Mutex mutex = new Mutex(initiallyOwned: false, AppInfo.InstanceMutexFor(text)))
		{
			if (text3 == null && !AcquireInstance(mutex, flag4 ? TimeSpan.FromSeconds(20L) : TimeSpan.Zero))
			{
				if (flag3)
				{
					num3 = 0;
				}
				else
				{
					string text5 = L.T("El contador de cofres ya está abierto: mira en la barra de tareas.", "The chest counter is already open: look in the taskbar.");
					if (cliMode)
					{
						Console.WriteLine("  " + text5);
					}
					else
					{
						MessageBox.Show(text5, "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					}
					num3 = 0;
				}
			}
			else
			{
				Config config = (flag2 ? null : store.LoadConfig());
				bool flag5 = config == null;
				if (config == null)
				{
					config = new Config();
				}
				L.Use(config.Language);
				if (args.Contains("--show-overlay"))
				{
					config.OverlayVisible = true;
					config.OverlayLocked = true;
				}
				if (text2 != null)
				{
					config.DemosFolder = text2;
				}
				string demosFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");
				if (string.IsNullOrWhiteSpace(config.DemosFolder))
				{
					config.DemosFolder = demosFolder;
				}
				if (text3 != null)
				{
					store.LoadPrizeNames();
					TrackerData trackerData = Profiles.View(store.LoadData(), config);
					foreach (MatchRecord item2 in trackerData.Matches.OrderByDescending((MatchRecord m) => m.MatchStartUtc ?? m.AnalyzedAtUtc).Take(3))
					{
						item2.MatchStartUtc = DateTime.UtcNow;
					}
					if (!OperatingSystem.IsWindows())
					{
						num3 = 1;
					}
					else
					{
						NativeOverlay.Start(-20000, -20000, locked: true, (int _, int _) =>
						{
						}, (bool _) =>
						{
						});
						MatchRecord item = new MatchRecord
						{
							ChestsOpened = 12,
							TreasureFinds =
							{
								new TreasureFind
								{
									Item = "Treasure"
								}
							}
						};
						(string, MatchRecord, bool)[] array = new (string, MatchRecord, bool)[3]
						{
							("-en-vivo", item, true),
							("-en-curso", null, true),
							("-espera", null, false)
						};
						for (int num4 = 0; num4 < array.Length; num4++)
						{
							var (text6, live, inProgress) = array[num4];
							NativeOverlay.Update(OverlayModel.Build(trackerData, live, inProgress, Profiles.InitialChests(config, trackerData.Matches.Select(Profiles.Key).FirstOrDefault())) with
							{
								TokenChance = PirateShopChances.HudPercent(store, config, live)
							});
							Thread.Sleep(600);
							string text7 = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(text3)) ?? ".", Path.GetFileNameWithoutExtension(text3) + text6 + ".png");
							Console.WriteLine(NativeOverlay.Snapshot(text7) ? L.F("Vista previa: {0}", "Preview: {0}", text7) : L.F("Error al capturar: {0}", "Capture failed: {0}", text7));
						}
						num3 = 0;
					}
				}
				else
				{
					LowerPriority();
					store.LoadPrizeNames();
					TrackerData data = store.LoadData();
					Tracker tracker = new Tracker(store, config, data);
					bool flag6 = tracker.UpgradeIfNeeded();
					if (flag)
					{
						tracker.Scan(ignoreQuietPeriod: true);
						ReportWriter.WriteAll(store, config, data);
						tracker.PrintSummary();
						Console.WriteLine(L.F("  Informe: {0}", "  Report: {0}", store.ReportPath));
						num3 = 0;
					}
					else
					{
						if (flag5)
						{
							List<FileInfo> list = (from f in tracker.UnprocessedFiles()
								where ReplayHeaderProbe.IsLive(f.FullName) != true
								select f).ToList();
							List<InfoPoint> list2 = new List<InfoPoint>
							{
								new InfoPoint("!", Theme.Amber, L.T("Activa «Grabar replays» en Fortnite", "Turn on \"Record Replays\" in Fortnite"), L.T("Ajustes → Juego → Replays → Grabar replays. Sin eso, Fortnite no guarda replays y no hay nada que contar.", "Settings → Game → Replays → Record Replays. Without it, Fortnite saves no replays and there is nothing to count."))
							};
							if (list.Count > 0)
							{
								list2.Add(new InfoPoint("↺", Theme.Cyan, L.F("Ya hay {0} en tu PC", "There are already {0} on your PC", L.Count(list.Count, "replay", "replays", "replay", "replays")), L.T("Puedes contarlos ahora o empezar solo con tus próximas partidas.", "You can count them now or start with your next matches only.")));
							}
							bool flag7 = InfoDialog.Confirm(null, L.T("Bienvenido a ChestCounter", "Welcome to 2xFlaw's Chest Counter"), L.T("Cuenta tus cofres de Fortnite a partir de tus replays.", "Counts your Fortnite chests from your replays."), list2, (list.Count > 0) ? L.T("Contarlos", "Count them") : L.T("Empezar", "Start"), (list.Count > 0) ? L.T("Solo las próximas", "Next matches only") : null);
							if (list.Count > 0 && !flag7)
							{
								tracker.MarkSkipped(list);
							}
						}
						store.SaveConfig(config);
						if (OperatingSystem.IsWindows())
						{
							Updater.CleanupOld();
							if (config.StartWithFortnite)
							{
								FortniteWatch.Apply(enabled: true, text);
							}
							Process[] processesByName = Process.GetProcessesByName("ChestTracker");
							if (processesByName != null && processesByName.Length > 0)
							{
								MessageBox.Show(L.T("La versión antigua (ChestTracker.exe) sigue abierta: ciérrala, o las dos contarían a la vez.", "The old version (ChestTracker.exe) is still open: close it, otherwise both count at the same time."), "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
								Process[] array2 = processesByName;
								for (int num4 = 0; num4 < array2.Length; num4++)
								{
									array2[num4].Dispose();
								}
							}
						}
						ChestCounter.Monitor monitor = new ChestCounter.Monitor(store, config, data, tracker, text)
						{
							AutoMode = flag3
						};
						if (flag6)
						{
							monitor.SetNotice(L.T("Nueva versión del análisis: se vuelven a leer los replays (sin duplicados).", "New analysis version: the replays are read again (no duplicates)."));
						}
						MainForm form = new MainForm(monitor, store);
						TeamersBrowserLink.Register(store);
						monitor.CloseRequested += () =>
						{
							try
							{
								form.BeginInvoke(form.Close);
							}
							catch (InvalidOperationException)
							{
							}
						};
						monitor.UpdateOffered += (UpdateInfo u) =>
						{
							try
							{
								form.BeginInvoke(() =>
								{
									if (!form.IsDisposed && MessageBox.Show(form, L.F("Versión {0} disponible (tienes la {1}). ¿Instalarla ahora? El contador se reinicia solo y no pierdes nada.", "Version {0} is available (you have {1}). Install it now? The counter restarts by itself and nothing is lost.", u.Version.ToString(3), AppInfo.VersionText), "Chest Counter", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
									{
										monitor.InstallUpdate();
									}
								});
							}
							catch (InvalidOperationException)
							{
							}
						};
						monitor.Start();
						Application.Run(form);
						monitor.Stop();
						if (monitor.RestartAfterUpdate || monitor.RestartRequested)
						{
							string exePath = AppInfo.ExePath;
							if (exePath != null)
							{
								try
								{
									mutex.ReleaseMutex();
								}
								catch
								{
								}
								IEnumerable<string> enumerable = from a in args
									where a != "--after-update"
									select (!a.Contains(' ')) ? a : ("\"" + a + "\"");
								string arguments = string.Join(" ", monitor.RestartAfterUpdate ? enumerable.Append("--after-update") : enumerable);
								Process.Start(new ProcessStartInfo(exePath, arguments)
								{
									UseShellExecute = true
								})?.Dispose();
							}
						}
						num3 = 0;
					}
				}
			}
		}
		return num3;
		static bool AcquireInstance(Mutex m, TimeSpan wait)
		{
			try
			{
				return m.WaitOne(wait);
			}
			catch (AbandonedMutexException)
			{
				return true;
			}
		}
		static void LowerPriority()
		{
			if (!OperatingSystem.IsWindows())
			{
				return;
			}
			try
			{
				Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
			}
			catch
			{
			}
			try
			{
				EcoQos.Enable();
			}
			catch
			{
			}
		}
	}
}
