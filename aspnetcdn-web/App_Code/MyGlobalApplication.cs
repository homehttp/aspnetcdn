using System;
using System.Collections.Generic;
using System.Web;

public class MyGlobalApplication : aspnetcdn.AspNetApplication
{

	/// override the CreateWebsiteRequest for executing custom website redirecting logic
	protected override aspnetcdn.WebsiteRequest CreateWebsiteRequest()
	{
		HttpCookie cookie;

		string cdnr=Request.QueryString["cdnredirect"];
		if (!string.IsNullOrEmpty(cdnr))
		{
			cookie = new HttpCookie("cdnredirect", cdnr);
			cookie.Path = "/";
			cookie.HttpOnly = true;
			Response.Cookies.Add(cookie);
		}
		else
		{
			//Always show default.aspx of this website.
			if (Request.Path == "/" || Request.Path.Equals("/default.aspx", StringComparison.OrdinalIgnoreCase))
				return null;

			cookie = Request.Cookies["cdnredirect"];
			if (cookie != null)
				cdnr = cookie.Value;
		}

		if (cdnr == "example1")
		{
			//different server shall use different id, for different cache/logic slots
			long websiteid = 1;

			//you can rewrite the path and querystring
			string newpathquery = this.Request.Url.PathAndQuery;

			//what the http header HOST the server will recieve
			string newhostheader = "homehttp.com";

			//which server do you want to connect?
			//specify IP address if the server is not pointed by domain name
			string servername = "homehttp.com";
			int serverport = 80;

			Uri uri = new Uri("http://" + newhostheader + newpathquery);

			return new aspnetcdn.WebsiteRequest(websiteid, uri, servername, serverport);
		}

		if (cdnr == "example2")
		{
			//different server shall use different id, for different cache/logic slots
			long websiteid = 2;

			//you can rewrite the path and querystring
			string newpathquery = this.Request.Url.PathAndQuery;

			//what the http header HOST the server will recieve
			string newhostheader = "forums.asp.net";

			//which server do you want to connect?
			//specify IP address if the server is not pointed by domain name
			string servername = "forums.asp.net";
			int serverport = 80;

			Uri uri = new Uri("http://" + newhostheader + newpathquery);

			return new aspnetcdn.WebsiteRequest(websiteid, uri, servername, serverport);
		}

		//return null to execute the current website files
		return null;
	}



}