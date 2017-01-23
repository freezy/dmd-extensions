using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MimeTypes;
using NLog;
using WebSocketSharp.Server;

namespace LibDmd.Output.Network
{
	public class BrowserStream : WebSocketBehavior, IGray2Destination
	{
		public string Name { get; } = "Browser Stream";
		public bool IsAvailable { get; } = true;

		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
		private readonly Dictionary<string, string> _www = new Dictionary<string, string>(); 
		private readonly CancellationToken _token = new CancellationToken();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public BrowserStream()
		{
			// LibDmd.Output.Network.www.index.html
			const string prefix = "LibDmd.Output.Network.www.";
			Logger.Info("Resource names = {0}", string.Join(", ", _assembly.GetManifestResourceNames()));
			_assembly.GetManifestResourceNames()
				.Where(res => res.StartsWith(prefix))
				.ToList()
				.ForEach(res => _www["/" + res.Substring(0, prefix.Length)] = res);
			_www["/"] = prefix + "index.html";
				
			Listen("http://*:9090/", 4, _token);
			
		}

		public async Task Listen(string prefix, int maxConcurrentRequests, CancellationToken token)
		{
			HttpListener listener = new HttpListener();
			listener.Prefixes.Add(prefix);
			listener.Start();

			var requests = new HashSet<Task>();
			for (var i = 0; i < maxConcurrentRequests; i++)
				requests.Add(listener.GetContextAsync());

			while (!token.IsCancellationRequested) {
				var t = await Task.WhenAny(requests);
				requests.Remove(t);

				if (t is Task<HttpListenerContext>) {
					var context = (t as Task<HttpListenerContext>).Result;
					requests.Add(ProcessRequestAsync(context));
					requests.Add(listener.GetContextAsync());
				}
			}
		}

		public async Task ProcessRequestAsync(HttpListenerContext context)
		{
			var request = context.Request;
			var response = context.Response;
			var output = response.OutputStream;
			
			if (request.HttpMethod == HttpMethod.Get.Method && _www.ContainsKey(request.Url.AbsolutePath)) {
				response.StatusCode = 200;
				response.ContentType = GetMimeType(Path.GetExtension(request.Url.AbsolutePath));
				using (var input = _assembly.GetManifestResourceStream(_www[request.Url.AbsolutePath])) {
					response.ContentLength64 = input.Length;
					CopyStream(input, output);
				}
			} else {
				response.StatusCode = 404;
			}
			output.Close();
		}

		
		private static string GetMimeType(string ext)
		{
			return string.IsNullOrEmpty(ext) ? "text/html" : MimeTypeMap.GetMimeType(ext);
		}

		private static void CopyStream(Stream input, Stream output)
		{
			// Insert null checking here for production
			var buffer = new byte[8192];

			int bytesRead;
			while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0) {
				output.Write(buffer, 0, bytesRead);
			}
		}

		public void RenderGray2(byte[] frame)
		{
		}

		public void Dispose()
		{
		}
		
		public void Init()
		{
			// no init
		}
	}
}
