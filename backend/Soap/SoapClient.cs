using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml.Linq;

namespace Mekashron.Soap
{
    public class SoapClient
    {
        private static HttpClient CreateClient()
        {
            var h = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false,
                UseProxy = false
            };
            var http = new HttpClient(h)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            http.DefaultRequestVersion = HttpVersion.Version11;
            http.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return http;
        }

        private readonly HttpClient _http = CreateClient();
        private readonly string _endpoint;

        public SoapClient(string endpoint) => _endpoint = endpoint.TrimEnd('/');

        public async Task<(bool ok, string msg)> ProbeAsync()
        {
            try
            {
                using var r = await _http.GetAsync(_endpoint + "?wsdl");
                var txt = await r.Content.ReadAsStringAsync();
                return (r.IsSuccessStatusCode, $"HTTP {(int)r.StatusCode}; len={txt.Length}");
            }
            catch (Exception ex) { return (false, ex.ToString()); }
        }


        public async Task<(bool ok, string message, object? payload)>
            CallAsync(string actionName, string xmlBody)
            => await CallManyAsync(actionName, new[] { xmlBody });

        public async Task<(bool ok, string message, object? payload)>
            CallManyAsync(string actionName, IEnumerable<string> xmlBodies)
        {
            var probe = await ProbeAsync();
            if (!probe.ok)
                return (false, $"PROBE FAIL: {probe.msg}", null);

            var primary = $"urn:ICUTech.Intf-IICUTech#{actionName}";
            var soap11Actions = new[]
            {
                primary,
                $"\"{primary}\"",
                actionName, 
                $"urn:ICUTech#{actionName}",
                $"urn:ICUTech-ICUTech#{actionName}",
                $"urn:ICUTech-IICUTech#{actionName}"
            };

            foreach (var body in xmlBodies)
            {
                foreach (var act in soap11Actions)
                {
                    var r = await TryCall(
                        endpoint: _endpoint,
                        contentType: "text/xml; charset=utf-8",
                        soapActionHeader: act,
                        xmlBody: body,
                        protocol: "SOAP11"
                    );

                    if (r.ok || r.message.StartsWith("SOAP Fault"))
                        return (r.ok, $"{r.message} (SOAP11 action: {act})", r.payload);

                    if (r.message.Contains("SocketException")
                        || r.message.Contains("while sending the request")
                        || r.message.Contains("InvalidOperationException"))
                        return r;
                }
            }

            var first = xmlBodies.First();
            var fullAction = $"urn:ICUTech.Intf-IICUTech#{actionName}";
            var r12 = await TryCall(
                endpoint: _endpoint,
                contentType: $"application/soap+xml; charset=utf-8; action=\"{fullAction}\"",
                soapActionHeader: null,
                xmlBody: SoapEnvelopeTemplates.ToSoap12(first),
                protocol: "SOAP12"
            );
            return (r12.ok, r12.message + " (SOAP12)", r12.payload);
        }


        private async Task<(bool ok, string message, object? payload)> TryCall(
            string endpoint, string contentType, string? soapActionHeader, string xmlBody, string protocol)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(xmlBody);
            using var content = new ByteArrayContent(bytes);

            var parts = contentType.Split(';');
            var mediaType = parts[0].Trim();
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            foreach (var part in parts.Skip(1).Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)))
            {
                var kv = part.Split('=', 2);
                content.Headers.ContentType.Parameters.Add(
                    kv.Length == 2
                        ? new NameValueHeaderValue(kv[0], kv[1].Trim('"'))
                        : new NameValueHeaderValue(kv[0]));
            }

            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Content = content
            };

            req.Headers.ConnectionClose = true;

            if (!string.IsNullOrEmpty(soapActionHeader))
                req.Headers.TryAddWithoutValidation("SOAPAction", soapActionHeader);

            req.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36");
            req.Headers.TryAddWithoutValidation("Accept", "text/xml, application/soap+xml, */*;q=0.1");
            req.Headers.ExpectContinue = false;

            try
            {
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                var text = await resp.Content.ReadAsStringAsync();
                var ct = resp.Content.Headers.ContentType?.MediaType ?? "";

                if (!resp.IsSuccessStatusCode)
                    return (false, $"{protocol}: HTTP {(int)resp.StatusCode} body: {Slice(text, 800)}", null);

                var looksSoap = text.Contains("<Envelope") || text.Contains(":Envelope");
                var looksHtml = ct.Contains("html")
                                || text.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase)
                                || text.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
                if (looksHtml || !looksSoap)
                    return (false, $"{protocol}: получил не SOAP (ct={ct}) body: {Slice(text, 800)}", null);

                if (text.Contains(":Fault") || text.Contains("<faultcode>"))
                    return (false, "SOAP Fault: " + ExtractFault(text), Slice(text, 1000));

                var x = XDocument.Parse(text);
                var body = x.Root?.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName.EndsWith("Response"));
                var retNode = body?.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "return");
                var retText = retNode?.Value?.Trim();

                if (!string.IsNullOrEmpty(retText) &&
                    retText.StartsWith("{") &&
                    retText.EndsWith("}"))
                {
                    JsonElement json;
                    int resultCode = int.MinValue;
                    string? resultMsg = null;

                    using (var doc = JsonDocument.Parse(retText))
                    {
                        json = doc.RootElement.Clone();

                        if (doc.RootElement.TryGetProperty("ResultCode", out var rcEl) &&
                            rcEl.TryGetInt32(out var rc))
                            resultCode = rc;

                        if (doc.RootElement.TryGetProperty("ResultMessage", out var rmEl))
                            resultMsg = rmEl.GetString();
                    } 

                    var payload = new
                    {
                        json,
                        xml = body?.ToString(),
                        rawReturn = retText
                    };

                    return resultCode >= 0
                        ? (true, $"Success (ResultCode={resultCode})", payload)
                        : (false, $"Error (ResultCode={resultCode}): {resultMsg}", payload);
                }

                return (true, "Success (no JSON in <return>)",
                    new { xml = body?.ToString() ?? text });
            }
            catch (Exception ex)
            {
                return (false, $"{protocol}: {ex}", null);
            }
        }

        private static string ExtractFault(string xml)
        {
            try
            {
                var x = XDocument.Parse(xml);
                var fault = x.Descendants().FirstOrDefault(n => n.Name.LocalName == "Fault");
                var reason = fault?.Descendants()
                    .FirstOrDefault(n => n.Name.LocalName is "faultstring" or "Reason")?.Value;
                return reason ?? "SOAP Fault";
            }
            catch { return "SOAP Fault"; }
        }

        private static string Slice(string s, int max)
            => string.IsNullOrEmpty(s) ? s : (s.Length > max ? s[..max] : s);
    }
}
