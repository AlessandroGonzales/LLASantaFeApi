using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Application.Interfaces;
using Domain.Exceptions;
using Microsoft.Extensions.Configuration;
namespace Infrastructure.ExternalServices;

// INSTREAM no envía nombres de archivo ni permite al cliente elegir host o ruta.
public sealed class ClamAvPdfScanner(IConfiguration configuration) : IPdfScanner
{
    private static readonly SemaphoreSlim Slots = new(2,2);
    public async Task<bool> EsLimpioAsync(byte[] contenido, CancellationToken ct)
    {
        var host = configuration["PdfAntivirus:Host"];
        if (string.IsNullOrWhiteSpace(host)) throw Unavailable();
        if (!await Slots.WaitAsync(0, ct)) throw new BusinessException("rate_limit", "El análisis está ocupado. Intentá más tarde.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var client = new TcpClient();
            var port = configuration["PdfAntivirus:Port"] is string value && int.TryParse(value, out var parsed) ? parsed : 3310;
            if (port is < 1 or > 65535) throw Unavailable();
            await client.ConnectAsync(host, port, timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
            var prefix = new byte[4];
            for (var offset=0; offset<contenido.Length; offset+=65536)
            {
                var count=Math.Min(65536,contenido.Length-offset);
                BinaryPrimitives.WriteInt32BigEndian(prefix,count);
                await stream.WriteAsync(prefix,timeout.Token);
                await stream.WriteAsync(contenido.AsMemory(offset,count),timeout.Token);
            }
            await stream.WriteAsync(new byte[4],timeout.Token);
            var response = new byte[1024]; var used=0;
            while (used<response.Length)
            {
                var read=await stream.ReadAsync(response.AsMemory(used),timeout.Token);
                if(read==0) break;
                used+=read;
                var end=Array.IndexOf(response,(byte)0,0,used);
                if(end>=0)
                {
                    var result=Encoding.ASCII.GetString(response,0,end);
                    if(result=="stream: OK") return true;
                    if(result.StartsWith("stream: ",StringComparison.Ordinal) && result.EndsWith(" FOUND",StringComparison.Ordinal)) return false;
                    throw Unavailable();
                }
            }
            throw Unavailable();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (SocketException) { throw Unavailable(); }
        catch (IOException) { throw Unavailable(); }
        finally { Slots.Release(); }
    }
    private static BusinessException Unavailable() => new("unavailable", "El análisis antivirus no está disponible. El archivo no fue guardado; la solicitud de texto sigue disponible.");
}
