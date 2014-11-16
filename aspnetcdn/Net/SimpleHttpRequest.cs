// Copyright (c) homehttp.com.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace aspnetcdn
{
	class SimpleHttpRequest
	{
		class SocketInfo
		{
			public Socket Socket;
			public DateTime AddTime = DateTime.Now;
		}
		static Dictionary<string, Queue<SocketInfo>> sockmap = new Dictionary<string, Queue<SocketInfo>>();
		static Queue<SocketInfo> GetQueue(string endpointstr)
		{
			lock (sockmap)
			{
				Queue<SocketInfo> queue;
				if (sockmap.TryGetValue(endpointstr, out queue))
					return queue;
				queue = new Queue<SocketInfo>();
				sockmap[endpointstr] = queue;
				return queue;
			}
		}
		private static void ReleaseSocket(Socket socket)
		{
			Queue<SocketInfo> queue=GetQueue(socket.LocalEndPoint.ToString());
			SocketInfo info = new SocketInfo();
			info.Socket = socket;
			lock (queue)
			{
				queue.Enqueue(info);
			}
		}
		private static Socket AllocateSocket(IPEndPoint ipep, string method, string pathquery)
		{
			StringBuilder strb = new StringBuilder();
			strb.Append(method).Append(" ").Append(pathquery).Append(" HTTP/1.1\r\n");
			byte[] buff = Encoding.UTF8.GetBytes(strb.ToString());
			string key = ipep.ToString();

			Queue<SocketInfo> queue = GetQueue(key);
			for (int i = 0; i < 5; i++)
			{
				Socket sock=null;
				if (i < 3)
				{
					lock (queue)
					{
						if (queue.Count > 0)
							sock = queue.Dequeue().Socket;
					}
				}
				if (sock == null)
				{
					sock = new Socket(ipep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
					sock.Connect(ipep);
				}
				sock.Send(buff);
				return sock;
			}
			throw (new Exception("Unable Connect Server"));
		}

		Socket _sock;

		List<string> _reqheaders = new List<string>();
		List<KeyValuePair<string, string>> _resheaders = new List<KeyValuePair<string, string>>();

		public void Connect(IPEndPoint ipep, string method,Uri uri)
		{
			_sock = AllocateSocket(ipep, method, uri.PathAndQuery);
			_reqheaders.Add("Host: " + uri.Host);
			_reqheaders.Add("Connection: keep-alive");
			_reqheaders.Add("ASPNETCDN-PROXYBY: HOMEHTTP");
		}

		public void AddHeader(string name, string value)
		{
			switch (name)
			{
				case "ASPNETCDN-PROXYBY":
					throw (new Exception("ASPNETCDN-PROXYBY HEADER RECURSIVE"));
				case "ACCEPT-ENCODING":
				case "CONNECTION":
					return;
				default:
					_reqheaders.Add(name + ": " + value);
					break;
			}
		}

		private void SendHeaders()
		{
			if (_reqheaders != null)
			{
				StringBuilder strb = new StringBuilder();
				foreach (string header in _reqheaders)
				{
					strb.Append(header).Append("\r\n");
				}
				strb.Append("\r\n");
				byte[] headerbuff = Encoding.UTF8.GetBytes(strb.ToString());
				_sock.Send(headerbuff, 0, headerbuff.Length, SocketFlags.None);
				_reqheaders = null;
			}
		}

		public void WriteRequest(byte[] data, int index, int count)
		{
			SendHeaders();

			_sock.Send(data, index, count, SocketFlags.None);
		}


		bool _keepalive;
		bool _chunked = false;
		long _contentlen = -1;

		public void InitResponse()
		{
			SendHeaders();

			byte[] databuffer = new byte[65536];

			int readlen = 0;

			int dclrf = -1;
			while (dclrf == -1 && readlen < databuffer.Length)
			{
				int rc = _sock.Receive(databuffer, readlen, databuffer.Length - readlen, SocketFlags.None);
				if (rc == 0)
					throw (new Exception("ErrorFindDoubleCLRF"));
				readlen += rc;
				dclrf = IndexOfDoubleCRLF(databuffer, 0, readlen);
			}
			if (dclrf == -1)
				throw (new Exception("ErrorFindDoubleCLRF"));

			string[] lines = Encoding.UTF8.GetString(databuffer, 0, dclrf).Split(new string[] { "\r\n" }, StringSplitOptions.None);
			string[] firstlineparts = lines[0].Split(new char[] { ' ' }, 3);
			if (firstlineparts[0] != "HTTP/1.1" && firstlineparts[0] != "HTTP/1.0")
				throw (new Exception("InvalidHttpVersion:" + firstlineparts[0]));

			StatusCode = int.Parse(firstlineparts[1]);
			if (firstlineparts.Length > 2)
			{
				StatusDesc = firstlineparts[2];
			}

			for (int li = 1; li < lines.Length; li++)
			{
				string header = lines[li];
				int pos = header.IndexOf(':');
				if (pos == -1)
					throw (new Exception("ErrorParsingHeaders"));
				string name = header.Substring(0, pos).Trim();
				string value = header.Substring(pos + 1).Trim();
				_resheaders.Add(new KeyValuePair<string, string>(name, value));
				if (name == "Content-Encoding" && value == "chunked")
				{
					_chunked = true;
				}
				if (name == "Content-Length")
				{
					_contentlen = long.Parse(value);
				}
				if (name == "Connection" && value.Equals("keep-alive", StringComparison.OrdinalIgnoreCase))
				{
					_keepalive = true;
				}
			}

			if (_chunked)
				_reader = new ChunkedReader();
			else
				_reader = new ContentReader(_contentlen);

			_reader._sock = _sock;
			_reader._buffer = databuffer;
			_reader._bufferindex = dclrf + 4;
			_reader._bufferlength = readlen - dclrf - 4;
		}


		public int StatusCode { get; set; }
		public string StatusDesc { get; set; }

		public List<KeyValuePair<string, string>> GetResponseHeaders()
		{
			return _resheaders;
		}

		ResponseReader _reader;

		public int ReadResponse(byte[] buff)
		{
			if (_reader._completed)
				return 0;
			return _reader.Read(buff, 0, buff.Length);
		}

		public void Release()
		{
			if (_sock != null)
			{
				if (_keepalive && _reader != null && _reader._completed)
				{
					ReleaseSocket(_sock);
				}
				else
				{
					_sock.Close();
				}
				_sock = null;
			}
		}

		static public int IndexOfDoubleCRLF(byte[] bytes, int index, int len)
		{
			int endpos = index + len;
			for (; index + 4 <= endpos; index++)
			{
				if (bytes[index] == 13 && bytes[index + 1] == 10 && bytes[index+2] == 13 && bytes[index + 3] == 10)
					return index;
			}

			return -1;
		}
	}
}
