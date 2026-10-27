# Serves the web app files on this PC so the app can be tried in a browser without the real server.
#   powershell -ExecutionPolicy Bypass -File tools\serve-web.ps1            (then open http://localhost:8099/app/?demo)
# "?demo" shows made-up data and never contacts the live server. Press Ctrl+C to stop.
param([int]$Port = 8099)

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\backend\RhythmFlow.Api\wwwroot')).Path
$types = @{ '.html' = 'text/html; charset=utf-8'; '.js' = 'text/javascript; charset=utf-8'; '.css' = 'text/css; charset=utf-8'; '.png' = 'image/png'; '.ttf' = 'font/ttf';
            '.webmanifest' = 'application/manifest+json'; '.json' = 'application/json'; '.svg' = 'image/svg+xml'; '.ico' = 'image/x-icon' }
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
Write-Host "Serving $root at http://localhost:$Port/app/?demo   (Ctrl+C to stop)"
try {
  while ($listener.IsListening) {
    $ctx = $listener.GetContext(); $res = $ctx.Response
    try {
      $path = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath)
      if ($path -eq '/app') { $res.StatusCode = 301; $res.RedirectLocation = '/app/' + $ctx.Request.Url.Query; $res.Close(); continue }
      if ($path.EndsWith('/')) { $path += 'index.html' }
      $file = [IO.Path]::GetFullPath((Join-Path $root $path.TrimStart('/').Replace('/', '\')))
      if (-not $file.StartsWith($root) -or -not (Test-Path $file -PathType Leaf)) { $res.StatusCode = 404; $res.Close(); continue }
      $ext = [IO.Path]::GetExtension($file).ToLower()
      $res.ContentType = if ($types.ContainsKey($ext)) { $types[$ext] } else { 'application/octet-stream' }
      $res.Headers.Add('Cache-Control', 'no-store')
      $bytes = [IO.File]::ReadAllBytes($file); $res.ContentLength64 = $bytes.Length; $res.OutputStream.Write($bytes, 0, $bytes.Length); $res.Close()
    } catch { try { $res.StatusCode = 500; $res.Close() } catch { } }
  }
} finally { $listener.Stop() }
