# Simulador de protocolo exclusivo de pruebas. NO es un antivirus ni debe usarse en producción.
param([int]$Port=13310)
$ErrorActionPreference='Stop'
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$Port)
$listener.Start()
try {
 while($true) {
  $client=$listener.AcceptTcpClient()
  try {
   $stream=$client.GetStream();$stream.ReadTimeout=10000
   $cmd=[byte[]]::new(10);$stream.ReadExactly($cmd)
   if([Text.Encoding]::ASCII.GetString($cmd) -ne "zINSTREAM`0"){throw 'Unexpected command'}
   $memory=[IO.MemoryStream]::new();$prefix=[byte[]]::new(4)
   while($true){
    $stream.ReadExactly($prefix)
    [Array]::Reverse($prefix);$size=[BitConverter]::ToInt32($prefix,0)
    if($size -eq 0){break};if($size -lt 0 -or $size -gt 65536){throw 'Invalid chunk'}
    $chunk=[byte[]]::new($size);$stream.ReadExactly($chunk);$memory.Write($chunk)
    if($memory.Length -gt 2097152){throw 'Oversize stream'}
   }
   $text=[Text.Encoding]::ASCII.GetString($memory.ToArray());$memory.Dispose()
   $result=if($text.Contains('SCANNER_FOUND')){'stream: Test.Signature FOUND'}else{'stream: OK'}
   $reply=[Text.Encoding]::ASCII.GetBytes($result+"`0")
   $stream.Write($reply);$stream.Flush()
  } finally {$client.Dispose()}
 }
} finally {$listener.Stop()}
