using Microsoft.AspNetCore.Mvc;

namespace CarTrace.Mcp.Controllers;

[ApiController]
[Route("privacy")]
public sealed class PrivacyController : ControllerBase
{
    [HttpGet]
    public ContentResult Get()
    {
        var html = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>CarTrace – Privacy Policy</title>
<style>
body { font-family: Arial, sans-serif; max-width: 800px; margin: 40px auto; line-height: 1.6; }
h1, h2 { color: #222; }
</style>
</head>
<body>
<h1>CarTrace – Privacy Policy</h1>

<p>CarTrace is a B2B vehicle intelligence service.</p>

<h2>Data Controller</h2>
<p>CarTrace (contact: privacy@cartrace.ai)</p>

<h2>Data processed</h2>
<ul>
  <li>Vehicle identifiers (license plate, make, model)</li>
  <li>Technical data (mileage, service events)</li>
  <li>Corporate user identifiers (user ID, role, organizational unit)</li>
</ul>
<p>CarTrace does not process names, emails or direct personal data.</p>

<h2>Purpose</h2>
<p>Data is processed to provide fleet management, service tracking and operational intelligence.</p>

<h2>Legal basis</h2>
<p>Processing is based on B2B contracts with customers that provide the data.</p>

<h2>Data retention</h2>
<p>Data is retained only as long as required to provide the service.</p>

<h2>Security</h2>
<p>Data is protected using authentication, access control and secure cloud infrastructure.</p>

<h2>Rights</h2>
<p>Data subjects may request access or deletion by contacting privacy@cartrace.ai.</p>

</body>
</html>
""";

        return new ContentResult
        {
            ContentType = "text/html",
            Content = html,
            StatusCode = 200
        };
    }
}
