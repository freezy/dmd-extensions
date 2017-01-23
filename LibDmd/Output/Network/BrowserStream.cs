using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace LibDmd.Output.Network
{
	public class BrowserStream : IGray2Destination
	{
		public string Name { get; } = "Browser Stream";
		public bool IsAvailable { get; } = true;

		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
		private Dictionary<string, string> _www = new Dictionary<string, string>(); 

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public BrowserStream()
		{
			// LibDmd.Output.Network.www.index.html
			var prefix = "LibDmd.Output.Network.www.";
			Logger.Info("Resource names = {0}", string.Join(", ", _assembly.GetManifestResourceNames()));
			_assembly.GetManifestResourceNames()
				.Where(res => res.StartsWith(prefix))
				.ToList()
				.ForEach(res => _www["/" + res.Substring(0, prefix.Length)] = res);
			_www["/"] = prefix + "index.html";
				
			var token = new CancellationToken();
			Listen("http://*:9090/", 4, token);
			
		}

		public async Task Listen(string prefix, int maxConcurrentRequests, CancellationToken token)
		{
			HttpListener listener = new HttpListener();
			listener.Prefixes.Add(prefix);
			listener.Start();

			var requests = new HashSet<Task>();
			for (int i = 0; i < maxConcurrentRequests; i++)
				requests.Add(listener.GetContextAsync());

			while (!token.IsCancellationRequested) {
				Task t = await Task.WhenAny(requests);
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
				using (var input = _assembly.GetManifestResourceStream(_www[request.Url.AbsolutePath])) {
					response.ContentLength64 = input.Length;
					CopyStream(input, output);
				}
			} else {
				response.StatusCode = 404;
			}
			output.Close();
		}

		public static void CopyStream(Stream input, Stream output)
		{
			// Insert null checking here for production
			var buffer = new byte[8192];

			int bytesRead;
			while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0) {
				output.Write(buffer, 0, bytesRead);
			}
		}

		public void Init()
		{
		}

		public void RenderGray2(byte[] frame)
		{
		}

		public void Dispose()
		{
		}
	}
}
