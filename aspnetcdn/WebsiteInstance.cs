// Copyright (c) homehttp.com.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace aspnetcdn
{
	public sealed class WebsiteInstance
	{
		static Dictionary<long, WebsiteInstance> __instmap = new Dictionary<long, WebsiteInstance>();

		static public WebsiteInstance GetOrCreate(long uid)
		{
			lock (__instmap)
			{
				WebsiteInstance inst;
				if (__instmap.TryGetValue(uid, out inst))
					return inst;
				inst = new WebsiteInstance(uid);
				__instmap[uid] = inst;
				return inst;
			}
		}

		long _uid;
		private WebsiteInstance(long uid)
		{
			_uid = uid;
		}

		public long UniqueID
		{
			get
			{
				return _uid;
			}
		}

		object _lock=new object();
		public object Lock
		{
			get
			{
				return _lock;
			}
		}
	}
}
