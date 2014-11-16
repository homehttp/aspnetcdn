// Copyright (c) homehttp.com.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Web;

namespace aspnetcdn
{
	public abstract class AspNetApplication : HttpApplication
	{
		public override void Init()
		{
			this.BeginRequest += GlobalApplication_BeginRequest;

			base.Init();
		}

		static public string[] DefaultDocuments = new string[] { "default.aspx", "default.htm", "default.html", "index.aspx", "index.htm", "index.html" };

		private void MapToDefaultDocument()
		{
			if (DefaultDocuments == null || DefaultDocuments.Length == 0)
				return;

			string path = Context.Request.PhysicalPath;
			if (System.IO.Directory.Exists(path))
			{
				foreach (string eachdoc in DefaultDocuments)
				{
					string filename = System.IO.Path.Combine(path, eachdoc);
					if (System.IO.File.Exists(filename))
					{
						Context.RewritePath(Request.Path.TrimEnd('/') + "/" + eachdoc);
						return;
					}
				}
			}
		}

		bool _requestcompleted = false;
		public new void CompleteRequest()
		{
			_requestcompleted = true;
			base.CompleteRequest();
		}

		protected abstract WebsiteRequest CreateWebsiteRequest();

		void GlobalApplication_BeginRequest(object sender, EventArgs e)
		{
			_requestcompleted = false;

			WebsiteRequest wt = CreateWebsiteRequest();

			if (_requestcompleted || Response.IsRequestBeingRedirected)
				return;

			if (wt == null)
			{
				MapToDefaultDocument();
				return;
			}

			try
			{
				WebsiteInstance wi = WebsiteInstance.GetOrCreate(wt.UniqueID);

				wt.OnInitialize(wi, this);

				SendWebsiteRequest(wt);

				wt.InitResponse();

				SendWebsiteResponse(wt);

				this.Response.Flush();

				this.CompleteRequest();
			}
			finally
			{
				wt.Dispose();
			}

		}

		private void SendWebsiteRequest(WebsiteRequest wt)
		{
			
			wt.InitRequest(Request.HttpMethod);

			wt.AddRequestHeader("X-Forwarded-For", this.Request.UserHostAddress);

			for (int ki = 0; ki < Request.Headers.Keys.Count; ki++)
			{
				string key = Request.Headers.Keys[ki];
				wt.AddRequestHeader(key, Request.Headers[ki]);
			}

			if (Request.HttpMethod == "POST")
			{
				wt.WriteRequestStream(Request.InputStream);
			}

		}

		private void SendWebsiteResponse(WebsiteRequest wt)
		{
			this.Response.StatusCode = wt.GetResponseStatus();

			string statusdesc = wt.GetResponseStatusDesc();
			if (!string.IsNullOrEmpty(statusdesc))
				this.Response.StatusDescription = statusdesc;

			NameValueCollection headers = wt.GetResponseHeaders();

			for (int ki = 0; ki < headers.Keys.Count; ki++)
			{
				string key = headers.Keys[ki];
				switch (key.ToUpper())
				{
					case "CONTENT-TYPE":
						this.Response.ContentType = headers[ki];
						continue;
					case "LOCATION":
					default:
						this.Response.Headers.Add(key, headers[ki]);
						break;
				}
			}

			wt.WriteResonseStream(Response.OutputStream);
		}

	}

}