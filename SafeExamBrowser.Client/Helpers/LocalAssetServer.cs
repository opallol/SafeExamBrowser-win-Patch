/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using SafeExamBrowser.Logging.Contracts;

namespace SafeExamBrowser.Client.Helpers
{
	/// <summary>
	/// A minimal local HTTP file server (127.0.0.1:48080) that serves proctoring asset files to the exam page,
	/// restricting CORS to kemenkeu.go.id origins. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class LocalAssetServer
	{
		private const int DEFAULT_PORT = 48080;
		private const string ALLOWED_ORIGIN_HOST = "kemenkeu.go.id";
		private const string ALLOWED_ORIGIN_SUFFIX = ".kemenkeu.go.id";
		private const string ALLOWED_METHODS = "GET, HEAD, OPTIONS";
		private const string ALLOWED_HEADERS = "Content-Type";

		private readonly ILogger logger;
		private readonly string assetDirectory;

		private HttpListener listener;
		private Thread listenerThread;
		private bool isRunning;

		public bool IsRunning => isRunning;

		public LocalAssetServer(string assetDirectory, ILogger logger)
		{
			this.assetDirectory = assetDirectory;
			this.logger = logger;
		}

		public bool Start()
		{
			try
			{
				if (isRunning)
				{
					logger.Warn("Asset server already running");

					return true;
				}

				if (!TryStartListener())
				{
					logger.Warn($"Port {DEFAULT_PORT} is not available, attempting to terminate the program using it...");

					if (!TryFreePort() || !TryStartListener())
					{
						logger.Error($"Port {DEFAULT_PORT} is still not available, the asset server can't be started!");

						return false;
					}
				}

				listenerThread = new Thread(ListenForRequests)
				{
					IsBackground = true,
					Name = "AssetServerThread"
				};

				listenerThread.Start();

				return true;
			}
			catch (Exception e)
			{
				logger.Error("Failed to start asset server", e);
			}

			return false;
		}

		public void Stop()
		{
			try
			{
				if (isRunning)
				{
					isRunning = false;

					if (listener != null)
					{
						listener.Stop();
						listener.Close();
					}

					if (listenerThread != null && listenerThread.IsAlive)
					{
						listenerThread.Join(2000);
					}

					logger.Info("Asset server stopped");
				}
			}
			catch (Exception e)
			{
				logger.Error("Error stopping asset server", e);
			}
		}

		private bool TryStartListener()
		{
			try
			{
				listener = new HttpListener();
				listener.Prefixes.Add($"http://127.0.0.1:{DEFAULT_PORT}/");
				listener.Prefixes.Add($"http://localhost:{DEFAULT_PORT}/");
				listener.Start();
				isRunning = true;
				logger.Info($"Asset server started on http://127.0.0.1:{DEFAULT_PORT}/");

				return true;
			}
			catch (HttpListenerException e)
			{
				logger.Error($"Failed to start HTTP listener on port {DEFAULT_PORT}", e);

				try
				{
					listener?.Close();
				}
				catch
				{
				}

				listener = null;
			}

			return false;
		}

		private bool TryFreePort()
		{
			var freed = false;

			foreach (var pid in GetProcessesListeningOn(DEFAULT_PORT))
			{
				try
				{
					using (var process = Process.GetProcessById(pid))
					{
						logger.Warn($"Terminating process '{process.ProcessName}' (PID {pid}) which is using port {DEFAULT_PORT}...");
						process.Kill();

						if (process.WaitForExit(3000))
						{
							freed = true;
							logger.Info($"Successfully terminated process with PID {pid}.");
						}
						else
						{
							logger.Warn($"Process with PID {pid} did not terminate in time!");
						}
					}
				}
				catch (Exception e)
				{
					logger.Error($"Failed to terminate process with PID {pid} which is using port {DEFAULT_PORT}", e);
				}
			}

			return freed;
		}

		private IEnumerable<int> GetProcessesListeningOn(int port)
		{
			var pids = new List<int>();

			try
			{
				var ownId = Process.GetCurrentProcess().Id;
				string output;

				using (var process = new Process())
				{
					process.StartInfo.Arguments = "-ano";
					process.StartInfo.CreateNoWindow = true;
					process.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "netstat.exe");
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.UseShellExecute = false;
					process.Start();
					output = process.StandardOutput.ReadToEnd();
					process.WaitForExit(5000);
				}

				var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

				foreach (var line in lines)
				{
					var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

					if (parts.Length >= 5 && parts[0] == "TCP" && parts[1].EndsWith($":{port}") && parts[2].EndsWith(":0") && int.TryParse(parts[parts.Length - 1], out var pid))
					{
						if (pid <= 4 || pid == ownId)
						{
							logger.Warn($"Port {port} is used by process with PID {pid} which can't be terminated.");
						}
						else if (!pids.Contains(pid))
						{
							pids.Add(pid);
						}
					}
				}
			}
			catch (Exception e)
			{
				logger.Error($"Failed to determine which program is using port {port}", e);
			}

			return pids;
		}

		private void ListenForRequests()
		{
			logger.Debug("Asset server listener thread started");

			while (isRunning)
			{
				try
				{
					var context = listener.GetContext();
					ThreadPool.QueueUserWorkItem(HandleRequest, context);
				}
				catch (HttpListenerException)
				{
					break;
				}
				catch (Exception e)
				{
					logger.Error("Error in asset server listener", e);
				}
			}

			logger.Debug("Asset server listener thread stopped");
		}

		private void HandleRequest(object state)
		{
			if (!(state is HttpListenerContext context))
			{
				return;
			}

			try
			{
				var request = context.Request;
				var response = context.Response;
				var origin = request.Headers["Origin"];
				var isAllowedOrigin = IsAllowedOrigin(origin);

				if (request.HttpMethod == "OPTIONS")
				{
					ApplyCorsHeaders(response, origin, isAllowedOrigin);
					response.StatusCode = 200;
					response.ContentLength64 = 0L;
					response.OutputStream.Close();
					logger.Debug("Handled preflight request from origin: " + origin);

					return;
				}

				var relativePath = request.Url.AbsolutePath.TrimStart('/');

				if (string.IsNullOrEmpty(relativePath))
				{
					relativePath = "index.html";
				}

				var filePath = Path.Combine(assetDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
				var fullPath = Path.GetFullPath(filePath);
				var root = Path.GetFullPath(assetDirectory);

				if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
				{
					root += Path.DirectorySeparatorChar;
				}

				if (!fullPath.Equals(Path.GetFullPath(assetDirectory), StringComparison.OrdinalIgnoreCase) && !fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
				{
					logger.Warn("Access denied to path outside asset directory: " + relativePath);
					Send404(response, origin, isAllowedOrigin);
				}
				else if (File.Exists(filePath))
				{
					response.ContentType = GetContentType(filePath);
					response.StatusCode = 200;
					ApplyCorsHeaders(response, origin, isAllowedOrigin);

					if (request.HttpMethod != "HEAD")
					{
						var bytes = File.ReadAllBytes(filePath);
						response.ContentLength64 = bytes.Length;
						response.OutputStream.Write(bytes, 0, bytes.Length);
					}
					else
					{
						response.ContentLength64 = new FileInfo(filePath).Length;
					}

					response.OutputStream.Close();
					logger.Debug("Served asset: " + relativePath);
				}
				else
				{
					logger.Debug("Asset not found: " + relativePath);
					Send404(response, origin, isAllowedOrigin);
				}
			}
			catch (Exception e)
			{
				logger.Error("Error handling asset request", e);

				try
				{
					context.Response.StatusCode = 500;
					context.Response.Close();
				}
				catch
				{
				}
			}
		}

		private void Send404(HttpListenerResponse response, string requestOrigin, bool isAllowedOrigin)
		{
			try
			{
				ApplyCorsHeaders(response, requestOrigin, isAllowedOrigin);
				response.StatusCode = 404;
				response.ContentType = "text/html";
				var bytes = Encoding.UTF8.GetBytes("<html><body><h1>404 - Not Found</h1></body></html>");
				response.ContentLength64 = bytes.Length;
				response.OutputStream.Write(bytes, 0, bytes.Length);
				response.OutputStream.Close();
			}
			catch
			{
			}
		}

		private void ApplyCorsHeaders(HttpListenerResponse response, string requestOrigin, bool isAllowedOrigin)
		{
			if (isAllowedOrigin && !string.IsNullOrWhiteSpace(requestOrigin))
			{
				response.AddHeader("Access-Control-Allow-Origin", requestOrigin);
				response.AddHeader("Access-Control-Allow-Methods", ALLOWED_METHODS);
				response.AddHeader("Access-Control-Allow-Headers", ALLOWED_HEADERS);
				response.AddHeader("Vary", "Origin");
			}
		}

		private bool IsAllowedOrigin(string origin)
		{
			if (string.IsNullOrWhiteSpace(origin))
			{
				return false;
			}

			if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
			{
				return false;
			}

			var host = uri.Host;

			if (!string.Equals(host, ALLOWED_ORIGIN_HOST, StringComparison.OrdinalIgnoreCase))
			{
				return host.EndsWith(ALLOWED_ORIGIN_SUFFIX, StringComparison.OrdinalIgnoreCase);
			}

			return true;
		}

		private string GetContentType(string filePath)
		{
			switch (Path.GetExtension(filePath).ToLowerInvariant())
			{
				case ".html":
				case ".htm":
					return "text/html";
				case ".css":
					return "text/css";
				case ".mjs":
				case ".js":
					return "text/javascript";
				case ".wasm":
					return "application/wasm";
				case ".json":
					return "application/json";
				case ".png":
					return "image/png";
				case ".jpeg":
				case ".jpg":
					return "image/jpeg";
				case ".gif":
					return "image/gif";
				case ".svg":
					return "image/svg+xml";
				case ".ico":
					return "image/x-icon";
				case ".xml":
					return "text/xml";
				case ".txt":
					return "text/plain";
				default:
					return "application/octet-stream";
			}
		}
	}
}
