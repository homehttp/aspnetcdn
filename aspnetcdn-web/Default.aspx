<%@ Page Language="C#" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
	<title></title>
	<style>
		.poweredby {
			opacity:0.3;
		}
		.poweredby,.poweredby * {
			text-decoration:none;
		}
	</style>
</head>
<body>
	<form id="form1" runat="server">
		<h3>SAMPLES - aspnetcdn v0.1</h3>
		<p>
			<ul>
				<li><a href="?cdnredirect=example1">Example 1</a></li>
			</ul>
			<ul>
				<li><a href="?cdnredirect=example2">Example 2</a></li>
			</ul>
		</p>
		<div style="height: 200px;">&nbsp;</div>
		<p class="poweredby">
			<img src="http://homehttp.com/geticon.aspx?type=aspnetcdn" style="vertical-align:text-bottom;" />
			Powered by <a href="http://homehttp.com/" target="_blank">homehttp.com</a>
		</p>
	</form>
</body>
</html>
