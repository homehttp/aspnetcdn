// Copyright (c) homehttp.com.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Net;
using System.IO;

namespace aspnetcdn
{
	public class WebsiteRequest
	{

		private static IPEndPoint[] GetEndPoints(string ipaddr, int port)
		{
			IPAddress ipa;
			IPEndPoint[] ipeps;
			if (!IPAddress.TryParse(ipaddr, out ipa))
			{
				IPAddress[] addrs = Dns.GetHostAddresses(ipaddr);
				ipeps = new IPEndPoint[addrs.Length];
				for (int i = 0; i < addrs.Length; i++)
					ipeps[i] = new IPEndPoint(addrs[i], port);
			}
			else
			{
				ipeps = new IPEndPoint[1];
				ipeps[0] = new IPEndPoint(ipa, port);
			}
			return ipeps;
		}


		public WebsiteRequest(long uniqueid, Uri url)
		{
			_uid = uniqueid;
			_url = url;
		}
		public WebsiteRequest(long uniqueid, Uri url, IPEndPoint ipep)
		{
			_uid = uniqueid;
			_url = url;
			if (ipep != null)
			{
				_ipeps = new IPEndPoint[] { ipep };
			}
		}
		public WebsiteRequest(long uniqueid, Uri url, IPEndPoint[] ipeps)
		{
			_uid = uniqueid;
			_url = url;
			_ipeps = ipeps;
		}
		public WebsiteRequest(long uniqueid, Uri url, string ipaddr, int port)
		{
			_uid = uniqueid;
			_url = url;

			_ipeps = GetEndPoints(ipaddr, port);
		}

		long _uid;
		Uri _url;
		IPEndPoint[] _ipeps;
		SimpleHttpRequest _shr;

		public long UniqueID
		{
			get
			{
				return _uid;
			}
		}

		public Uri Url
		{
			get
			{
				return _url;
			}
		}

		WebsiteInstance _wi;
		AspNetApplication _app;

		public WebsiteInstance Website
		{
			get
			{
				return _wi;
			}
		}
		public AspNetApplication Application
		{
			get
			{
				return _app;
			}
		}
		public HttpContext Context
		{
			get
			{
				return _app.Context;
			}
		}

		public virtual void OnInitialize(WebsiteInstance wi, AspNetApplication app)
		{
			_wi = wi;
			_app = app;
		}


		public virtual void InitRequest(string method)
		{
			IPEndPoint[] ipeps=_ipeps;
			if (ipeps == null || ipeps.Length == 0)
				ipeps = GetEndPoints(_url.Host, _url.Port);

			IPEndPoint ipep = _ipeps[Math.Abs(Guid.NewGuid().GetHashCode()) % _ipeps.Length];

			_shr = new SimpleHttpRequest();
			_shr.Connect(ipep, method, _url);

		}

		public virtual void AddRequestHeader(string name, string value)
		{
			switch(name.ToUpper())
			{
				case "HOST":
				case "CONNECTION":
				case "ACCEPT-ENCODING":
					return;
				default:
					_shr.AddHeader(name, value);
					break;
			}
		}

		public virtual void WriteRequestStream(Stream src)
		{
			byte[] buffer = new byte[65536];
			while (true)
			{
				int rc = src.Read(buffer, 0, buffer.Length);
				if (rc == 0)
					break;
				WriteRequestBuffer(buffer, rc);
			}
		}

		protected virtual void WriteRequestBuffer(byte[] buffer, int rc)
		{
			_shr.WriteRequest(buffer, 0, rc);
		}

		public virtual void InitResponse()
		{
			_shr.InitResponse();
		}

		public virtual int GetResponseStatus()
		{
			return _shr.StatusCode;
		}
		public virtual string GetResponseStatusDesc()
		{
			return _shr.StatusDesc;
		}

		public virtual NameValueCollection GetResponseHeaders()
		{
			NameValueCollection headers = new NameValueCollection();
			foreach (KeyValuePair<string, string> kvp in _shr.GetResponseHeaders())
			{
				switch (kvp.Key.ToUpper())
				{
					case "CONNECTION":
						continue;
				}
				headers.Add(kvp.Key, kvp.Value);
			}

			return headers;
		}


		public virtual void WriteResonseStream(Stream dst)
		{
			byte[] buffer = new byte[65536];

			while (true)
			{
				int rc = _shr.ReadResponse(buffer);
				if (rc == 0)
					break;
				dst.Write(buffer, 0, rc);
			}
		}

		public virtual void Dispose()
		{
			_shr.Release();
		}

	}
}
